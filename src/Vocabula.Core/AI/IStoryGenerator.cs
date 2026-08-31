using Vocabula.Core.Models;

namespace Vocabula.Core.AI;

public interface IStoryGenerator
{
    Task<GeneratedStory> GenerateAsync(
        NotebookKnowledge knowledge,
        CancellationToken cancellationToken = default);
}
