using Vocabula.Core.Models;

namespace Vocabula.Core.AI;

public interface INotebookKnowledgeExtractor
{
    Task<NotebookKnowledge> ExtractAsync(
        Stream image,
        string mediaType,
        CancellationToken cancellationToken = default);
}
