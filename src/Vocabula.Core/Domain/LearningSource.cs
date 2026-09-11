namespace Vocabula.Core.Domain;

public sealed class LearningSource
{
    public Guid Id { get; set; }
    public Guid LearningProfileId { get; set; }
    public string ProcessingStatus { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    private LearningProfile _learningProfile = null!;
    public LearningProfile LearningProfile
    {
        get => _learningProfile;
        set => _learningProfile = value;
    }
}
