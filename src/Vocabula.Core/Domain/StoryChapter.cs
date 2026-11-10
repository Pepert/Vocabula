namespace Vocabula.Core.Domain;

public sealed class StoryChapter
{
    public Guid Id { get; set; }
    public Guid StoryId { get; set; }
    public int ChapterNumber { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = null!;
    public string? ResultingSummary { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    private Story _story = null!;
    public Story Story
    {
        get => _story;
        set => _story = value;
    }

    private readonly List<ChapterNewWord> _newWords = [];
    public IReadOnlyCollection<ChapterNewWord> NewWords => _newWords;
}
