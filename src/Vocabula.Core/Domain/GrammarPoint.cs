namespace Vocabula.Core.Domain;

public sealed class GrammarPoint
{
    public Guid Id { get; set; }
    public Guid LearningProfileId { get; set; }
    public string Name { get; set; } = null!;
    public string Explanation { get; set; } = null!;
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
