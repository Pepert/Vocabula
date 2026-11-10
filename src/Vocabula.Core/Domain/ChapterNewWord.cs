namespace Vocabula.Core.Domain;

/// <summary>A new word a chapter introduced, kept so the reading page can
/// underline it and show its definition later.</summary>
public sealed class ChapterNewWord
{
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string Word { get; set; } = null!;
    public string? Definition { get; set; }
    /// <summary><see cref="Models.NewWordKinds"/>: ai (story-introduced) or
    /// extracted (from the notebook, first appearance).</summary>
    public string Kind { get; set; } = null!;
    public int Position { get; set; }

    private StoryChapter _chapter = null!;
    public StoryChapter Chapter
    {
        get => _chapter;
        set => _chapter = value;
    }
}
