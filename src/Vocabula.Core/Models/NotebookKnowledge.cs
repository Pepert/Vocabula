namespace Vocabula.Core.Models;

public sealed record NotebookKnowledge(
    string TargetLanguage,
    string SourceSummary,
    IReadOnlyList<VocabularyEntry> Vocabulary,
    IReadOnlyList<GrammarPoint> GrammarPoints,
    IReadOnlyList<string> UncertainItems);
