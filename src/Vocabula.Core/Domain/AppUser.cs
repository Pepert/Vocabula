namespace Vocabula.Core.Domain;

public sealed class AppUser
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? IdentityProvider { get; set; }
    public string? ExternalSubject { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    private readonly List<LearningProfile> _learningProfiles = [];
    public IReadOnlyCollection<LearningProfile> LearningProfiles => _learningProfiles.AsReadOnly();
}
