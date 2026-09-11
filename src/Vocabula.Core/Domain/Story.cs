namespace Vocabula.Core.Domain;

public sealed class Story
{
    public Guid Id { get; set; }
    public Guid LearningProfileId { get; set; }
    public string Title { get; set; } = null!;
    public string? CurrentSummary { get; set; }

    private LearningProfile _learningProfile = null!;
    public LearningProfile LearningProfile
    {
        get => _learningProfile;
        set => _learningProfile = value;
    }

    private readonly List<StoryChapter> _chapters = [];
    public IReadOnlyCollection<StoryChapter> Chapters => _chapters.AsReadOnly();
}
