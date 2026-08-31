namespace Vocabula.Spike;

public sealed record VocabularyEntry(
    string Term,
    string BaseForm,
    string Meaning,
    string PartOfSpeech,
    string Evidence);

public sealed record GrammarPoint(
    string Name,
    string Explanation,
    IReadOnlyList<string> Examples);

public sealed record NotebookKnowledge(
    string TargetLanguage,
    string SourceSummary,
    IReadOnlyList<VocabularyEntry> Vocabulary,
    IReadOnlyList<GrammarPoint> GrammarPoints,
    IReadOnlyList<string> UncertainItems);

public sealed record GeneratedStory(
    string Title,
    string Story,
    IReadOnlyList<string> UsedVocabulary,
    IReadOnlyList<string> UsedGrammarPoints,
    IReadOnlyList<string> PotentiallyNewLanguage);

public sealed record SpikeOutput(
    NotebookKnowledge Knowledge,
    GeneratedStory Story);
