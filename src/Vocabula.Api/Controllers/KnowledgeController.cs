using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vocabula.Api.Authorization;
using Vocabula.Core.AI;
using Vocabula.Core.Models;
using Vocabula.Infrastructure.Persistence;
using Domain = Vocabula.Core.Domain;

namespace Vocabula.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
[Authorize]
public sealed class KnowledgeController : ControllerBase
{
    private const string TestLanguageCode = "sv";

    private readonly INotebookKnowledgeExtractor _extractor;
    private readonly VocabulaDbContext _dbContext;
    private readonly ICurrentAppUser _currentAppUser;

    public KnowledgeController(
        INotebookKnowledgeExtractor extractor,
        VocabulaDbContext dbContext,
        ICurrentAppUser currentAppUser)
    {
        _extractor = extractor;
        _dbContext = dbContext;
        _currentAppUser = currentAppUser;
    }

    private const int MaxImagesPerUpload = 10;

    [HttpPost("extract")]
    [RequestSizeLimit(100_000_000)] // 10 MB per image, up to 10 images
    public async Task<ActionResult<NotebookKnowledge>> ExtractAsync(
        [FromForm] ImageUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Images.Count == 0 || request.Images.Any(image => image.Length == 0))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing or empty file",
                Detail = "Please upload at least one non-empty notebook image."
            });
        }

        if (request.Images.Count > MaxImagesPerUpload)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Too many images",
                Detail = $"At most {MaxImagesPerUpload} images can be uploaded at once."
            });
        }

        foreach (var image in request.Images)
        {
            if (!IsSupportedMediaType(image.ContentType))
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Unsupported media type",
                    Detail = $"Unsupported image type: {image.ContentType}. Use image/jpeg, image/png, or image/webp."
                });
            }
        }

        try
        {
            var userId = _currentAppUser.UserId;

            // Find or create learning profile for authenticated user
            var learningProfile = await _dbContext.LearningProfiles
                .FirstOrDefaultAsync(lp => lp.UserId == userId && lp.LanguageCode == TestLanguageCode, cancellationToken);

            if (learningProfile == null)
            {
                learningProfile = new Domain.LearningProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    LanguageCode = TestLanguageCode,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.LearningProfiles.Add(learningProfile);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Daily guard before any transcription cost: adding resources
            // always leads to writing a chapter, so the shared chapter quota
            // applies here too - reported before the pictures are read.
            var (chaptersToday, resetsAtUtc) = await StoryQuota.GetChaptersTodayAsync(
                _dbContext, learningProfile.Id, cancellationToken);
            if (StoryQuota.IsExceeded(chaptersToday))
            {
                Response.Headers["Retry-After"] = Convert.ToInt32((resetsAtUtc - DateTimeOffset.UtcNow).TotalSeconds).ToString();

                return StatusCode(429, new ProblemDetails
                {
                    Title = "Daily story limit reached",
                    Detail = $"You can write up to {StoryQuota.MaxChaptersPerDay} chapters per day. The next chapter unlocks at {resetsAtUtc:HH:mm} UTC.",
                    Status = 429
                });
            }

            // Extract every page in parallel, then merge into one notebook
            // knowledge object (deduplicated across pages).
            var extractions = await Task.WhenAll(request.Images.Select(async image =>
            {
                await using var stream = image.OpenReadStream();
                return await _extractor.ExtractAsync(stream, image.ContentType, cancellationToken);
            }));
            var knowledge = NotebookKnowledgeMerger.Merge(extractions);

            // One learning source per uploaded page.
            var now = DateTimeOffset.UtcNow;
            foreach (var _ in request.Images)
            {
                _dbContext.LearningSources.Add(new Domain.LearningSource
                {
                    Id = Guid.NewGuid(),
                    LearningProfileId = learningProfile.Id,
                    ProcessingStatus = "processed",
                    CreatedAt = now
                });
            }

            // Skip vocabulary and grammar the profile already has, so repeat
            // or overlapping pages do not duplicate stored knowledge.
            var knownTerms = await _dbContext.VocabularyItems
                .Where(v => v.LearningProfileId == learningProfile.Id)
                .Select(v => v.NormalizedTerm)
                .ToListAsync(cancellationToken);
            var knownTermSet = knownTerms.ToHashSet(StringComparer.Ordinal);
            foreach (var vocab in knowledge.Vocabulary)
            {
                var normalized = vocab.BaseForm.ToLowerInvariant();
                if (knownTermSet.Add(normalized))
                {
                    _dbContext.VocabularyItems.Add(new Domain.VocabularyItem
                    {
                        Id = Guid.NewGuid(),
                        LearningProfileId = learningProfile.Id,
                        Term = vocab.Term,
                        NormalizedTerm = normalized,
                        Translation = vocab.Meaning,
                        FirstSeenAt = now,
                        TimesUsed = 0
                    });
                }
            }

            var knownGrammar = await _dbContext.GrammarPoints
                .Where(g => g.LearningProfileId == learningProfile.Id)
                .Select(g => g.Name)
                .ToListAsync(cancellationToken);
            var knownGrammarSet = knownGrammar.ToHashSet(StringComparer.Ordinal);
            foreach (var grammar in knowledge.GrammarPoints)
            {
                if (knownGrammarSet.Add(grammar.Name))
                {
                    _dbContext.GrammarPoints.Add(new Domain.GrammarPoint
                    {
                        Id = Guid.NewGuid(),
                        LearningProfileId = learningProfile.Id,
                        Name = grammar.Name,
                        Explanation = grammar.Explanation,
                        FirstSeenAt = now,
                        TimesUsed = 0
                    });
                }
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(knowledge);
        }
        catch (HttpRequestException ex) when (ex.StatusCode != null && IsTransientError(ex.StatusCode.Value))
        {
            return StatusCode(503, new ProblemDetails
            {
                Title = "Service unavailable",
                Detail = ex.Message,
                Status = 503
            });
        }
        catch (HttpRequestException ex)
        {
            var statusCode = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError);
            return StatusCode(statusCode, new ProblemDetails
            {
                Title = "Mistral API error",
                Detail = ex.Message,
                Status = statusCode
            });
        }
    }

    private static bool IsSupportedMediaType(string mediaType)
    {
        return mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("image/webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientError(HttpStatusCode? statusCode)
    {
        if (statusCode == null) return false;
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            _ => false
        };
    }
}
