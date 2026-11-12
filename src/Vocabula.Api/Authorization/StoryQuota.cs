using Microsoft.EntityFrameworkCore;
using Vocabula.Infrastructure.Persistence;

namespace Vocabula.Api.Authorization;

/// <summary>
/// The shared daily quota for the create-a-chapter flow. Since adding
/// resources always leads to writing a chapter, both the extraction and
/// the generation endpoints enforce the same limit, so an over-quota user
/// learns before any transcription cost is incurred or knowledge is stored.
/// The quota is per user across all their learning languages: studying
/// Swedish and Spanish does not buy six chapters.
/// </summary>
public static class StoryQuota
{
    public const int MaxChaptersPerDay = 3;

    /// <summary>The user's chapter count today across all learning
    /// profiles, and when the quota resets (UTC midnight).</summary>
    public static async Task<(int ChaptersToday, DateTimeOffset ResetsAtUtc)> GetChaptersTodayAsync(
        VocabulaDbContext dbContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        var todayStartUtc = new DateTimeOffset(nowUtc.Year, nowUtc.Month, nowUtc.Day, 0, 0, 0, TimeSpan.Zero);

        var chaptersToday = await (
            from profile in dbContext.LearningProfiles
            join story in dbContext.Stories on profile.Id equals story.LearningProfileId
            join chapter in dbContext.StoryChapters on story.Id equals chapter.StoryId
            where profile.UserId == userId && chapter.CreatedAt >= todayStartUtc
            select chapter
        ).CountAsync(cancellationToken);

        return (chaptersToday, todayStartUtc.AddDays(1));
    }

    public static bool IsExceeded(int chaptersToday) => chaptersToday >= MaxChaptersPerDay;
}
