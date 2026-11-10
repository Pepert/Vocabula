namespace Vocabula.Core.Models;

/// <summary>A word used in a chapter that was not part of the learner's
/// known vocabulary yet, with the meaning the learner needs for it.</summary>
public sealed record NewWordDefinition(string Word, string? Definition);
