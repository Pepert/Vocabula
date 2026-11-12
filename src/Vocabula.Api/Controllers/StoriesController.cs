using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vocabula.Api.Authorization;
using Vocabula.Core.AI;
using Vocabula.Core.Languages;
using Vocabula.Core.Models;
using Vocabula.Infrastructure.Persistence;
using Domain = Vocabula.Core.Domain;

namespace Vocabula.Api.Controllers;

[ApiController]
[Route("api/stories")]
[Authorize]
public sealed class StoriesController : ControllerBase
{
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

            // The account's chosen learning language drives the profile;
            // the chapters, story and knowledge all live under it.
            var user = await _dbContext.AppUsers
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user?.LearningLanguage == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Learning language not chosen",
                    Detail = "Choose the language you are learning in the app's settings first."
                });
            }

            // Find learning profile for the active language
            var learningProfile = await _dbContext.LearningProfiles
                .FirstOrDefaultAsync(lp => lp.UserId == userId && lp.LanguageCode == user.LearningLanguage, cancellationToken);

            if (learningProfile == null)
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Learning profile not found",
                    Detail = $"No learning profile found for the active language '{user.LearningLanguage}'. Add learning material first."
                });
            }

            // Daily guard: checked before any generation cost is incurred.
            // Shared with the extraction endpoint via StoryQuota, so the
            // limit is also reported before pictures are transcribed.
            var (chaptersToday, resetsAtUtc) = await StoryQuota.GetChaptersTodayAsync(
                _dbContext, userId, cancellationToken);

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
                // The account's language wins over whatever the client
                // sends: chapters are always written in the language the
                // user is learning, with definitions in their UI language.
                knowledge with { TargetLanguage = LearningLanguages.EnglishNameOf(user.LearningLanguage) ?? knowledge.TargetLanguage },
                user.LearningLanguage,
                storyEntity?.CurrentSummary,
                acquiredVocabulary,
                acquiredGrammar,
                LearningLanguages.EnglishNameOf(user.Language) ?? "English",
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

            // The model recaps the chapter in the summary field so the next
            // generation continues from a real recap; the truncation is only
            // a fallback if the model ever returns an empty summary.
            var summary = !string.IsNullOrWhiteSpace(story.Summary)
                ? story.Summary
                : story.Story.Length > 200 ? story.Story[..200] + "..." : story.Story;

            // Create chapter (the generated title is this chapter's own title;
            // the story-level title stays the one chosen for chapter 1)
            var chapter = new Domain.StoryChapter
            {
                Id = Guid.NewGuid(),
                StoryId = storyEntity.Id,
                ChapterNumber = nextChapterNumber,
                Title = story.Title,
                Content = story.Story,
                ResultingSummary = summary,
                CreatedAt = DateTimeOffset.UtcNow
            };
            _dbContext.StoryChapters.Add(chapter);

            // The words this chapter introduced (blue underline), for the
            // dotted-underline feature on the reading page.
            var position = 0;
            foreach (var newWord in story.NewWords)
            {
                _dbContext.ChapterNewWords.Add(new Domain.ChapterNewWord
                {
                    Id = Guid.NewGuid(),
                    ChapterId = chapter.Id,
                    Word = newWord.Word,
                    Definition = newWord.Definition,
                    Kind = newWord.Kind ?? NewWordKinds.Ai,
                    Position = position++
                });
            }

            // Words the LEARNER'S NOTEBOOK taught earlier, appearing in the
            // story for the first time: reinforcement, green underline, with
            // the notebook's own translation. Only words never seen in any
            // earlier chapter count as first appearance.
            var earlierChapterContents = await _dbContext.StoryChapters
                .Where(previous => previous.StoryId == storyEntity.Id)
                .Select(previous => previous.Content)
                .ToListAsync(cancellationToken);
            var seenWords = new HashSet<string>(
                earlierChapterContents.SelectMany(StoryWordChecker.Tokenize),
                StringComparer.Ordinal);

            var notebookItems = await _dbContext.VocabularyItems
                .Where(item => item.LearningProfileId == learningProfile.Id)
                .Select(item => new { item.Term, item.Translation })
                .ToListAsync(cancellationToken);

            // Membership: every word of every notebook term, translation or
            // not ("bor" and "solna" are notebook words via "Jag bor i Solna").
            var notebookWords = new HashSet<string>(StringComparer.Ordinal);
            // Word -> translation. Standalone entries win over phrases
            // ("bra" -> its own entry, not the "Inte så bra" meaning).
            var translationByWord = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in notebookItems)
            {
                var words = StoryWordChecker.Tokenize(item.Term).ToList();
                if (words.Count == 1 && item.Translation != null)
                {
                    translationByWord[item.Term] = item.Translation;
                }
                notebookWords.UnionWith(words);
            }
            foreach (var item in notebookItems)
            {
                if (item.Translation == null) continue;
                foreach (var word in StoryWordChecker.Tokenize(item.Term))
                {
                    translationByWord.TryAdd(word, item.Translation);
                }
            }

            // Word-level glosses from the model beat phrase translations
            // ("bor" -> "live" rather than "I live in Solna").
            var glossByWord = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var gloss in story.WordGlosses)
            {
                glossByWord.TryAdd(gloss.Word.ToLowerInvariant(), gloss.Gloss);
            }

            var underlinedWords = new HashSet<string>(
                story.NewWords.Select(entry => entry.Word.ToLowerInvariant()),
                StringComparer.Ordinal);
            foreach (var word in StoryWordChecker.Tokenize(story.Story).Distinct())
            {
                // Nothing to show without a definition.
                var definition = glossByWord.TryGetValue(word, out var gloss)
                    ? gloss
                    : translationByWord.TryGetValue(word, out var translation)
                        ? translation
                        : null;

                if (!notebookWords.Contains(word) ||
                    definition == null ||
                    seenWords.Contains(word) ||
                    StoryWordChecker.IsFunctionWord(word, user.LearningLanguage) ||
                    underlinedWords.Contains(word))
                {
                    continue;
                }

                _dbContext.ChapterNewWords.Add(new Domain.ChapterNewWord
                {
                    Id = Guid.NewGuid(),
                    ChapterId = chapter.Id,
                    Word = word,
                    Definition = definition,
                    Kind = NewWordKinds.Extracted,
                    Position = position++
                });
            }

            // Update story current summary
            storyEntity.CurrentSummary = summary;

            await _dbContext.SaveChangesAsync(cancellationToken);

            // The chapter number lets the client redirect straight to the
            // new chapter instead of re-fetching the whole list.
            return Ok(new GenerateChapterResponse(nextChapterNumber, story));
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

        // The account's active learning language selects which story is
        // shown: switching languages switches to that language's chapters.
        var user = await _dbContext.AppUsers
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user?.LearningLanguage == null)
        {
            return Ok(new StoryChaptersResponse([]));
        }

        var chapters = await (
            from story in _dbContext.Stories
            join profile in _dbContext.LearningProfiles on story.LearningProfileId equals profile.Id
            join chapter in _dbContext.StoryChapters on story.Id equals chapter.StoryId
            where profile.UserId == userId && profile.LanguageCode == user.LearningLanguage
            orderby chapter.ChapterNumber
            select new
            {
                Chapter = chapter,
                StoryTitle = story.Title
            }
        ).ToListAsync(cancellationToken);

        var chapterIds = chapters.Select(c => c.Chapter.Id).ToList();
        var newWords = await _dbContext.ChapterNewWords
            .Where(word => chapterIds.Contains(word.ChapterId))
            .OrderBy(word => word.Position)
            .Select(word => new { word.ChapterId, word.Word, word.Definition, word.Kind })
            .ToListAsync(cancellationToken);

        var wordsByChapter = newWords
            .GroupBy(word => word.ChapterId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<NewWordDefinition>)group
                    .Select(word => new NewWordDefinition(word.Word, word.Definition, word.Kind))
                    .ToList());

        var response = chapters
            .Select(c => new StoryChapterResponse(
                c.Chapter.ChapterNumber,
                c.StoryTitle,
                c.Chapter.Title ?? c.StoryTitle,
                c.Chapter.Content,
                wordsByChapter.TryGetValue(c.Chapter.Id, out var newWords) ? newWords : [],
                c.Chapter.CreatedAt))
            .ToList();

        return Ok(new StoryChaptersResponse(response));
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

public sealed record GenerateChapterResponse(int ChapterNumber, GeneratedStory Story);

public sealed record StoryChapterResponse(
    int ChapterNumber,
    string StoryTitle,
    string Title,
    string Content,
    IReadOnlyList<NewWordDefinition> NewWords,
    DateTimeOffset CreatedAt);

public sealed record StoryChaptersResponse(IReadOnlyList<StoryChapterResponse> Chapters);

