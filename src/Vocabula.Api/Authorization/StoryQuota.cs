using Microsoft.EntityFrameworkCore;
using Vocabula.Infrastructure.Persistence;

namespace Vocabula.Api.Authorization;

/// <summary>
/// The shared daily quota for the create-a-chapter flow. Since adding
/// resources always leads to writing a chapter, both the extraction and
/// the generation endpoints enforce the same limit, so an over-quota user
/// learns before any transcription cost is incurred or knowledge is stored.
/// </summary>
public static class StoryQuota
{
    public const int MaxChaptersPerDay = 3;

    /// <summary>The user's chapter count today, and when the quota resets (UTC midnight).</summary>
    public static async Task<(int ChaptersToday, DateTimeOffset ResetsAtUtc)> GetChaptersTodayAsync(
        VocabulaDbContext dbContext,
        Guid learningProfileId,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var todayStartUtc = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, TimeSpan.Zero);

        var storyIdForProfile = await dbContext.Stories
            .Where(s => s.LearningProfileId == learningProfileId)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var chaptersToday = storyIdForProfile == null
            ? 0
            : await dbContext.StoryChapters.CountAsync(
                sc => sc.StoryId == storyIdForProfile && sc.CreatedAt >= todayStartUtc,
                cancellationToken);

        return (chaptersToday, todayStartUtc.AddDays(1));
    }

    public static bool IsExceeded(int chaptersToday) => chaptersToday >= MaxChaptersPerDay;
}
