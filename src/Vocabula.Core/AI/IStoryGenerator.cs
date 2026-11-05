using Vocabula.Core.Models;

namespace Vocabula.Core.AI;

public interface IStoryGenerator
{
    Task<GeneratedStory> GenerateAsync(
        NotebookKnowledge knowledge,
        string? previousStorySummary = null,
        IReadOnlyList<string>? acquiredVocabulary = null,
        IReadOnlyList<string>? acquiredGrammar = null,
        CancellationToken cancellationToken = default);
}
