namespace Vocabula.Core.Domain;

public sealed class AppUser
{
    public Guid Id { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? IdentityProvider { get; set; }
    public string? ExternalSubject { get; set; }
    /// <summary>UI language preference ("en", "fr"); null = follow the
    /// device language.</summary>
    public string? Language { get; set; }
    /// <summary>The language the user is learning ("en", "fr", "es", "sv");
    /// null = not chosen yet. Turns the active learning profile.</summary>
    public string? LearningLanguage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    private readonly List<LearningProfile> _learningProfiles = [];
    public IReadOnlyCollection<LearningProfile> LearningProfiles => _learningProfiles.AsReadOnly();
}
