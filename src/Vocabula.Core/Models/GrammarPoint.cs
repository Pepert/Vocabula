namespace Vocabula.Core.Models;

public sealed record GrammarPoint(
    string Name,
    string Explanation,
    IReadOnlyList<string> Examples);
