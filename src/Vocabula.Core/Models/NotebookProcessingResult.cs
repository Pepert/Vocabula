namespace Vocabula.Core.Models;

public sealed record NotebookProcessingResult(
    NotebookKnowledge Knowledge,
    GeneratedStory Story);
