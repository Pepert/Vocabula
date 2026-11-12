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

    // Basic function words any beginner sentence needs: scaffolding, never
    // highlighted vocabulary. Swedish is the best-tuned list (the original
    // target); the others are starter lists covering the usual pronouns,
    // articles, copulas, conjunctions, prepositions and question words.
    private static readonly IReadOnlyDictionary<string, HashSet<string>> FunctionWordsByLanguage = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
    {
        ["sv"] = new(StringComparer.Ordinal)
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
        },
        ["en"] = new(StringComparer.Ordinal)
        {
            // pronouns and articles
            "i", "you", "he", "she", "it", "we", "they", "me", "him", "her", "us", "them",
            "my", "your", "his", "its", "our", "their", "mine", "yours", "hers", "ours", "theirs",
            "this", "that", "these", "those", "a", "an", "the",
            // copulas and auxiliaries
            "am", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had",
            "do", "does", "did", "can", "will", "would", "should", "must", "shall", "may", "might",
            // conjunctions and common particles
            "and", "or", "but", "not", "so", "if", "then", "yes", "no", "now", "here", "there",
            "again", "too", "very", "just", "also", "than", "as", "because", "when", "while",
            // prepositions and particles
            "in", "on", "at", "to", "from", "by", "of", "for", "with", "without", "about",
            "into", "over", "under", "up", "down", "out", "off",
            // question words
            "what", "who", "where", "why", "how", "which", "whose"
        },
        ["fr"] = new(StringComparer.Ordinal)
        {
            // pronouns and articles
            "je", "tu", "il", "elle", "on", "nous", "vous", "ils", "elles",
            "me", "te", "se", "le", "la", "les", "lui", "leur", "eux",
            "mon", "ton", "son", "ma", "ta", "sa", "mes", "tes", "ses",
            "notre", "votre", "nos", "vos", "leurs",
            "ce", "cet", "cette", "ces", "un", "une", "des", "du", "de", "la",
            // copulas and auxiliaries
            "suis", "es", "est", "sommes", "êtes", "sont", "était", "étaient", "être",
            "ai", "as", "a", "avons", "avez", "ont", "avait", "avoir", "eu",
            "peux", "peut", "pouvons", "peuvent", "veux", "veut", "voulons", "veulent",
            "vais", "vas", "va", "allons", "allez", "vont", "aller", "fais", "fait", "faire",
            // conjunctions and common particles
            "et", "ou", "mais", "ne", "pas", "plus", "moins", "très", "bien", "mal",
            "oui", "non", "ici", "là", "maintenant", "aussi", "comme", "tout", "tous", "toute", "toutes",
            "que", "qui", "quoi", "où", "quand", "comment", "pourquoi", "si", "parce",
            // prepositions and particles
            "dans", "sur", "avec", "au", "aux", "en", "y", "pour", "par", "sans", "chez", "entre",
            "vers", "depuis", "jusque", "pendant",
            "bonjour", "merci", "salut"
        },
        ["es"] = new(StringComparer.Ordinal)
        {
            // pronouns and articles
            "yo", "tú", "él", "ella", "usted", "ustedes", "nosotros", "nosotras",
            "vosotros", "vosotras", "ellos", "ellas", "me", "te", "se", "lo", "la", "los", "las",
            "le", "les", "mí", "ti", "mi", "tu", "su", "mis", "tus", "sus",
            "nuestro", "nuestra", "vuestro", "vuestra", "estos", "estas",
            "el", "un", "una", "unos", "unas",
            // copulas and auxiliaries
            "soy", "eres", "es", "somos", "sois", "son", "era", "éramos", "eran", "ser",
            "estoy", "estás", "está", "estamos", "están", "estar", "estaba",
            "tengo", "tienes", "tiene", "tenemos", "tienen", "tener", "tenía",
            "he", "has", "ha", "hemos", "han", "había",
            "puedo", "puedes", "puede", "podemos", "pueden", "poder",
            "quiero", "quieres", "quiere", "queremos", "quieren", "querer",
            "hago", "haces", "hace", "hacemos", "hacen", "hacer",
            // conjunctions and common particles
            "y", "o", "pero", "no", "sí", "muy", "bien", "mal", "aquí", "allí", "ahora",
            "también", "como", "todo", "todos", "toda", "todas", "más", "menos", "porque",
            "que", "qué", "quién", "cuál", "dónde", "cuándo", "cómo", "por", "si",
            // prepositions and particles
            "en", "de", "a", "con", "para", "sin", "sobre", "entre", "hasta", "desde",
            "hola", "gracias"
        },
    };

    /// <summary>
    /// Distinct words of <paramref name="story"/> that appear neither in the
    /// allowed terms nor in the function-word list of the target language.
    /// Multi-word allowed terms (e.g. "Jag är trött") allow every word they
    /// contain.
    /// </summary>
    public static IReadOnlyList<string> FindNewWords(string story, IEnumerable<string> allowedTerms, string languageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(story);
        ArgumentNullException.ThrowIfNull(allowedTerms);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        var functionWords = FunctionWordsFor(languageCode);

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
            if (allowed.Contains(word) || functionWords.Contains(word) || !seen.Add(word))
            {
                continue;
            }

            newWords.Add(word);
        }

        return newWords;
    }

    /// <summary>True for the basic function words any beginner sentence
    /// needs in the target language; they are scaffolding, never
    /// highlighted vocabulary.</summary>
    public static bool IsFunctionWord(string word, string languageCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(word);
        ArgumentException.ThrowIfNullOrWhiteSpace(languageCode);

        return FunctionWordsFor(languageCode).Contains(word.ToLowerInvariant());
    }

    private static HashSet<string> FunctionWordsFor(string languageCode)
    {
        return FunctionWordsByLanguage.TryGetValue(languageCode.ToLowerInvariant(), out var words)
            ? words
            : [];
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
