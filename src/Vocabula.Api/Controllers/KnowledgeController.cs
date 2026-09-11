using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vocabula.Core.AI;
using Vocabula.Core.Models;
using Vocabula.Infrastructure.Persistence;
using Domain = Vocabula.Core.Domain;

namespace Vocabula.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController : ControllerBase
{
    private static readonly Guid TestUserId = Guid.Parse("a7fa0f0d-30bc-4aaf-822d-6f7c6c9ebbe7");
    private const string TestLanguageCode = "sv";

    private readonly INotebookKnowledgeExtractor _extractor;
    private readonly VocabulaDbContext _dbContext;

    public KnowledgeController(INotebookKnowledgeExtractor extractor, VocabulaDbContext dbContext)
    {
        _extractor = extractor;
        _dbContext = dbContext;
    }

    [HttpPost("extract")]
    [RequestSizeLimit(10_000_000)] // 10 MB
    public async Task<ActionResult<NotebookKnowledge>> ExtractAsync(
        [FromForm] ImageUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Image == null || request.Image.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing or empty file",
                Detail = "Please upload a non-empty notebook image."
            });
        }

        if (!IsSupportedMediaType(request.Image.ContentType))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported media type",
                Detail = $"Unsupported image type: {request.Image.ContentType}. Use image/jpeg, image/png, or image/webp."
            });
        }

        try
        {
            await using var stream = request.Image.OpenReadStream();
            var knowledge = await _extractor.ExtractAsync(stream, request.Image.ContentType, cancellationToken);

            // Find or create learning profile for test user
            var learningProfile = await _dbContext.LearningProfiles
                .FirstOrDefaultAsync(lp => lp.UserId == TestUserId && lp.LanguageCode == TestLanguageCode, cancellationToken);

            if (learningProfile == null)
            {
                learningProfile = new Domain.LearningProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = TestUserId,
                    LanguageCode = TestLanguageCode,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.LearningProfiles.Add(learningProfile);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Create learning source
            var learningSource = new Domain.LearningSource
            {
                Id = Guid.NewGuid(),
                LearningProfileId = learningProfile.Id,
                ProcessingStatus = "processed",
                CreatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.LearningSources.Add(learningSource);

            // Map and save vocabulary items
            var now = DateTimeOffset.UtcNow;
            foreach (var vocab in knowledge.Vocabulary)
            {
                _dbContext.VocabularyItems.Add(new Domain.VocabularyItem
                {
                    Id = Guid.NewGuid(),
                    LearningProfileId = learningProfile.Id,
                    Term = vocab.Term,
                    NormalizedTerm = vocab.BaseForm.ToLowerInvariant(),
                    Translation = vocab.Meaning,
                    FirstSeenAt = now,
                    TimesUsed = 0
                });
            }

            // Map and save grammar points
            foreach (var grammar in knowledge.GrammarPoints)
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
