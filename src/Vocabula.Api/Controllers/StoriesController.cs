using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vocabula.Core.AI;
using Vocabula.Core.Models;
using Vocabula.Infrastructure.Persistence;
using Domain = Vocabula.Core.Domain;

namespace Vocabula.Api.Controllers;

[ApiController]
[Route("api/stories")]
public sealed class StoriesController : ControllerBase
{
    private static readonly Guid TestUserId = Guid.Parse("a7fa0f0d-30bc-4aaf-822d-6f7c6c9ebbe7");
    private const string TestLanguageCode = "sv";

    private readonly IStoryGenerator _generator;
    private readonly VocabulaDbContext _dbContext;

    public StoriesController(IStoryGenerator generator, VocabulaDbContext dbContext)
    {
        _generator = generator;
        _dbContext = dbContext;
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
            var story = await _generator.GenerateAsync(knowledge, cancellationToken);

            // Find learning profile for test user
            var learningProfile = await _dbContext.LearningProfiles
                .FirstOrDefaultAsync(lp => lp.UserId == TestUserId && lp.LanguageCode == TestLanguageCode, cancellationToken);

            if (learningProfile == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Learning profile not found",
                    Detail = "No learning profile found for test user with language code 'sv'. Extract knowledge first."
                });
            }

            // Find or create story
            var storyEntity = await _dbContext.Stories
                .FirstOrDefaultAsync(s => s.LearningProfileId == learningProfile.Id, cancellationToken);

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
