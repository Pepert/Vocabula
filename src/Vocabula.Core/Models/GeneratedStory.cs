namespace Vocabula.Core.Models;

public sealed record GeneratedStory(
    string Title,
    string Story,
    IReadOnlyList<string> UsedVocabulary,
    IReadOnlyList<string> UsedGrammarPoints,
    IReadOnlyList<string> PotentiallyNewLanguage,
    string Summary,
    IReadOnlyList<NewWordDefinition> NewWords);
