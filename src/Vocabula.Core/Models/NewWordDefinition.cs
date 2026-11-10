namespace Vocabula.Core.Models;

/// <summary>Where a word shown to the learner comes from.</summary>
public static class NewWordKinds
{
    /// <summary>Vocabulary the story itself introduced (model claim).</summary>
    public const string Ai = "ai";

    /// <summary>Vocabulary extracted from the learner's notebook earlier,
    /// appearing in the story for the first time.</summary>
    public const string Extracted = "extracted";
}

/// <summary>A word a chapter introduced, with the meaning the learner
/// needs for it. <see cref="Kind"/> drives the underline colour.</summary>
public sealed record NewWordDefinition(string Word, string? Definition, string? Kind = null);
