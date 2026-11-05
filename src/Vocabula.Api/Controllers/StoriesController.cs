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
[Route("api/stories")]
[Authorize]
public sealed class StoriesController : ControllerBase
{
    private const string TestLanguageCode = "sv";

    private readonly IStoryGenerator _generator;
    private readonly VocabulaDbContext _dbContext;
    private readonly ICurrentAppUser _currentAppUser;

    public StoriesController(
        IStoryGenerator generator,
        VocabulaDbContext dbContext,
        ICurrentAppUser currentAppUser)
    {
        _generator = generator;
        _dbContext = dbContext;
        _currentAppUser = currentAppUser;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<GeneratedStory>> GenerateAsync(
        [FromBody] NotebookKnowledge knowledge,
        CancellationToken cancellationToken)
    {
        if (knowledge == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing request body",
                Detail = "Please provide NotebookKnowledge in the request body."
            });
        }

        try
        {
            var userId = _currentAppUser.UserId;

            // Find learning profile for authenticated user
            var learningProfile = await _dbContext.LearningProfiles
                .FirstOrDefaultAsync(lp => lp.UserId == userId && lp.LanguageCode == TestLanguageCode, cancellationToken);

            if (learningProfile == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Learning profile not found",
                    Detail = "No learning profile found for authenticated user with language code 'sv'. Extract knowledge first."
                });
            }

            // Daily guard: checked before any generation cost is incurred.
            // Shared with the extraction endpoint via StoryQuota, so the
            // limit is also reported before pictures are transcribed.
            var (chaptersToday, resetsAtUtc) = await StoryQuota.GetChaptersTodayAsync(
                _dbContext, learningProfile.Id, cancellationToken);

            if (StoryQuota.IsExceeded(chaptersToday))
            {
                var nowUtc = DateTimeOffset.UtcNow;
                Response.Headers["Retry-After"] = Convert.ToInt32((resetsAtUtc - nowUtc).TotalSeconds).ToString();

                return StatusCode(429, new ProblemDetails
                {
                    Title = "Daily story limit reached",
                    Detail = $"You can write up to {StoryQuota.MaxChaptersPerDay} chapters per day. The next chapter unlocks at {resetsAtUtc:HH:mm} UTC.",
                    Status = 429
                });
            }

            // Find or create story, and pass its summary so the new chapter
            // continues where the previous one left off.
            var storyEntity = await _dbContext.Stories
                .FirstOrDefaultAsync(s => s.LearningProfileId == learningProfile.Id, cancellationToken);

            // Everything the learner has acquired so far, so the story stays
            // close to their actual level (not just the latest extraction).
            var acquiredVocabulary = await _dbContext.VocabularyItems
                .Where(v => v.LearningProfileId == learningProfile.Id)
                .Select(v => v.Term)
                .ToListAsync(cancellationToken);
            var acquiredGrammar = await _dbContext.GrammarPoints
                .Where(g => g.LearningProfileId == learningProfile.Id)
                .Select(g => g.Name)
                .ToListAsync(cancellationToken);

            var story = await _generator.GenerateAsync(
                knowledge,
                storyEntity?.CurrentSummary,
                acquiredVocabulary,
                acquiredGrammar,
                cancellationToken);

            if (storyEntity == null)
            {
                storyEntity = new Domain.Story
                {
                    Id = Guid.NewGuid(),
                    LearningProfileId = learningProfile.Id,
                    Title = story.Title,
                    CurrentSummary = null
                };
                _dbContext.Stories.Add(storyEntity);
            }

            // Calculate next chapter number
            var maxChapter = await _dbContext.StoryChapters
                .Where(sc => sc.StoryId == storyEntity.Id)
                .Select(sc => sc.ChapterNumber)
                .ToListAsync(cancellationToken);
            int nextChapterNumber = (maxChapter.Count == 0 ? 0 : maxChapter.Max()) + 1;

            // Create chapter
            var chapter = new Domain.StoryChapter
            {
                Id = Guid.NewGuid(),
                StoryId = storyEntity.Id,
                ChapterNumber = nextChapterNumber,
                Content = story.Story,
                ResultingSummary = story.Story.Length > 200 ? story.Story[..200] + "..." : story.Story,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.StoryChapters.Add(chapter);

            // Update story current summary
            storyEntity.CurrentSummary = story.Story.Length > 200 ? story.Story[..200] + "..." : story.Story;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return Ok(story);
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

    /// <summary>
    /// All chapters of the current user's story, in reading order.
    /// </summary>
    [HttpGet("chapters")]
    public async Task<ActionResult<StoryChaptersResponse>> GetChaptersAsync(CancellationToken cancellationToken)
    {
        var userId = _currentAppUser.UserId;

        var chapters = await (
            from story in _dbContext.Stories
            join profile in _dbContext.LearningProfiles on story.LearningProfileId equals profile.Id
            join chapter in _dbContext.StoryChapters on story.Id equals chapter.StoryId
            where profile.UserId == userId && profile.LanguageCode == TestLanguageCode
            orderby chapter.ChapterNumber
            select new StoryChapterResponse(
                chapter.ChapterNumber,
                story.Title,
                chapter.Content,
                chapter.CreatedAt)
        ).ToListAsync(cancellationToken);

        return Ok(new StoryChaptersResponse(chapters));
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

public sealed record StoryChapterResponse(int ChapterNumber, string StoryTitle, string Content, DateTimeOffset CreatedAt);

public sealed record StoryChaptersResponse(IReadOnlyList<StoryChapterResponse> Chapters);

