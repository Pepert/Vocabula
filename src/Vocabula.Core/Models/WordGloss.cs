namespace Vocabula.Core.Models;

/// <summary>Word-level English gloss for a notebook vocabulary word the
/// story uses, produced by the model — unlike the notebook's phrase
/// translations ("Jag bor i Solna" → "I live in Solna").</summary>
public sealed record WordGloss(string Word, string Gloss);
