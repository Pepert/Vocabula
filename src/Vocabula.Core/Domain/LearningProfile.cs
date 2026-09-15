namespace Vocabula.Core.Domain;

public sealed class LearningProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string LanguageCode { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    private AppUser _user = null!;
    public AppUser User
    {
        get => _user;
        set => _user = value;
    }

    private readonly List<LearningSource> _learningSources = [];
    public IReadOnlyCollection<LearningSource> LearningSources => _learningSources.AsReadOnly();

    private readonly List<VocabularyItem> _vocabularyItems = [];
    public IReadOnlyCollection<VocabularyItem> VocabularyItems => _vocabularyItems.AsReadOnly();

    private readonly List<GrammarPoint> _grammarPoints = [];
    public IReadOnlyCollection<GrammarPoint> GrammarPoints => _grammarPoints.AsReadOnly();

    private readonly List<Story> _stories = [];
    public IReadOnlyCollection<Story> Stories => _stories.AsReadOnly();
}
