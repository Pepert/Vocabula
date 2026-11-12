using Vocabula.Core.Models;

namespace Vocabula.Core.AI;

public interface INotebookKnowledgeExtractor
{
    Task<NotebookKnowledge> ExtractAsync(
        Stream image,
        string mediaType,
        string? expectedLanguageName = null,
        string? definitionLanguageName = null,
        CancellationToken cancellationToken = default);
}
