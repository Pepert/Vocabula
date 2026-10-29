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
            var story = await _generator.GenerateAsync(knowledge, cancellationToken);

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
