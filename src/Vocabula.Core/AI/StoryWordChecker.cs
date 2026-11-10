using System.Text;

namespace Vocabula.Core.AI;

/// <summary>
/// Verifies programmatically that a generated chapter only uses words the
/// learner has been taught (plus a small budget of new ones). LLMs cannot
/// reliably count their own words, so the cap is checked in code.
/// </summary>
public static class StoryWordChecker
{
    public const int MaxNewWordsPerChapter = 3;

    // Basic function words any beginner sentence needs. Swedish only —
    // Vocabula currently targets Swedish notebooks.
    private static readonly HashSet<string> FunctionWords = new(StringComparer.Ordinal)
    {
        // pronouns and articles
        "jag", "du", "han", "hon", "hen", "den", "det", "de", "dom", "vi", "ni",
        "mig", "dig", "honom", "henne", "oss", "er", "dem",
        "en", "ett", "min", "din", "hans", "hennes", "sin", "sitt", "sina",
        "vår", "vårt", "våra", "era", "deras",
        // copulas and auxiliaries
        "är", "var", "har", "hade", "kan", "ska", "måste", "vill", "får", "blev", "blir",
        // conjunctions and common particles
        "och", "eller", "men", "att", "som", "inte", "också", "bara", "nu", "här", "då", "där",
        "sedan", "när", "om", "så", "ja", "nej", "igen", "kanske", "tills",
        // simple adverbs of place/direction used as prepositions
        "ut", "in", "upp", "ner",
        // prepositions
        "i", "på", "med", "av", "för", "från", "till", "vid", "hos",
        "över", "under", "genom", "utan",
        // question words
        "vad", "vem", "hur", "varför", "vilken", "vilket", "vilka"
    };

    /// <summary>
    /// Distinct words of <paramref name="story"/> that appear neither in the
    /// allowed terms nor in the function-word lists. Multi-word allowed terms
    /// (e.g. "Jag är trött") allow every word they contain.
    /// </summary>
    public static IReadOnlyList<string> FindNewWords(string story, IEnumerable<string> allowedTerms)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(story);
        ArgumentNullException.ThrowIfNull(allowedTerms);

        var allowed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var term in allowedTerms)
        {
            foreach (var word in Tokenize(term))
            {
                allowed.Add(word);
            }
        }

        var newWords = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in Tokenize(story))
        {
            if (allowed.Contains(word) || FunctionWords.Contains(word) || !seen.Add(word))
            {
                continue;
            }

            newWords.Add(word);
        }

        return newWords;
    }

    /// <summary>
    /// Words of the text, lowercased, in order: letter runs split on
    /// everything else. Handles Swedish characters (åäö) via Unicode.
    /// </summary>
    public static IEnumerable<string> Tokenize(string text)
    {
        var current = new StringBuilder();
        foreach (var character in text.ToLowerInvariant())
        {
            if (char.IsLetter(character))
            {
                current.Append(character);
            }
            else if (current.Length > 0)
            {
                yield return current.ToString();
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }
}
