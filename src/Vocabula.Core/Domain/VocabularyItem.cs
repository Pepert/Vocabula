namespace Vocabula.Core.Domain;

public sealed class VocabularyItem
{
    public Guid Id { get; set; }
    public Guid LearningProfileId { get; set; }
    public string Term { get; set; } = null!;
    public string NormalizedTerm { get; set; } = null!;
    public string? Translation { get; set; }
    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public int TimesUsed { get; set; }

    private LearningProfile _learningProfile = null!;
    public LearningProfile LearningProfile
    {
        get => _learningProfile;
        set => _learningProfile = value;
    }
}
