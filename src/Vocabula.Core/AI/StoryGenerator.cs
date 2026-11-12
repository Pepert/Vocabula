using System.Text.Json;
using Vocabula.Core.Models;

namespace Vocabula.Core.AI;

public sealed class StoryGenerator : IStoryGenerator
{
    private const string StorySchema = "{\n" +
        "  \"type\": \"object\",\n" +
        "  \"additionalProperties\": false,\n" +
        "  \"required\": [\"title\", \"story\", \"usedVocabulary\", \"usedGrammarPoints\", \"potentiallyNewLanguage\", \"summary\", \"newWords\", \"wordGlosses\"],\n" +
        "  \"properties\": {\n" +
        "    \"title\": { \"type\": \"string\" },\n" +
        "    \"story\": { \"type\": \"string\" },\n" +
        "    \"usedVocabulary\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } },\n" +
        "    \"usedGrammarPoints\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } },\n" +
        "    \"potentiallyNewLanguage\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } },\n" +
        "    \"summary\": { \"type\": \"string\" },\n" +
        "    \"newWords\": {\n" +
        "      \"type\": \"array\",\n" +
        "      \"items\": {\n" +
        "        \"type\": \"object\",\n" +
        "        \"additionalProperties\": false,\n" +
        "        \"required\": [\"word\", \"definition\"],\n" +
        "        \"properties\": {\n" +
        "          \"word\": { \"type\": \"string\" },\n" +
        "          \"definition\": { \"type\": \"string\" }\n" +
        "        }\n" +
        "      }\n" +
        "    },\n" +
        "    \"wordGlosses\": {\n" +
        "      \"type\": \"array\",\n" +
        "      \"items\": {\n" +
        "        \"type\": \"object\",\n" +
        "        \"additionalProperties\": false,\n" +
        "        \"required\": [\"word\", \"gloss\"],\n" +
        "        \"properties\": {\n" +
        "          \"word\": { \"type\": \"string\" },\n" +
        "          \"gloss\": { \"type\": \"string\" }\n" +
        "        }\n" +
        "      }\n" +
        "    }\n" +
        "  }\n" +
        "}";

    private const double Temperature = 0.4;

    // The new-word limit is enforced in code; the model gets this many
    // chances to rewrite before the latest attempt is returned as-is.
    private const int MaxRewrites = 2;

    private readonly IMistralClient _mistralClient;
    private readonly string _storyModel;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public StoryGenerator(IMistralClient mistralClient, string storyModel)
    {
        ArgumentNullException.ThrowIfNull(mistralClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(storyModel);

        _mistralClient = mistralClient;
        _storyModel = storyModel;
    }

    public async Task<GeneratedStory> GenerateAsync(
        NotebookKnowledge knowledge,
        string targetLanguageCode,
        string? previousStorySummary = null,
        IReadOnlyList<string>? acquiredVocabulary = null,
        IReadOnlyList<string>? acquiredGrammar = null,
        string? definitionLanguageName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(knowledge);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguageCode);

        // Definitions follow the account's UI language ("French" for a
        // French phone), defaulting to English; the story itself is
        // written in the learned language regardless.
        var definitionLanguage = string.IsNullOrWhiteSpace(definitionLanguageName)
            ? "English"
            : definitionLanguageName;

        var knowledgeJson = JsonSerializer.Serialize(knowledge, JsonOptions);
        // The recap of the previous chapter whitelists its words (character
        // names included): they were already presented to the learner, so
        // they neither count against the cap nor deserve underlines.
        var allowedTerms = CollectAllowedTerms(knowledge, acquiredVocabulary, acquiredGrammar, previousStorySummary);

        var messages = new List<object>
        {
            new { role = "user", content = BuildUserPrompt(knowledge, knowledgeJson, previousStorySummary, acquiredVocabulary, acquiredGrammar, definitionLanguage) }
        };

        var rewrites = 0;
        GeneratedStory story;
        while (true)
        {
            var request = new
            {
                model = _storyModel,
                // Snapshot: later retries must not mutate earlier payloads.
                messages = messages.ToList(),
                temperature = Temperature,
                response_format = StructuredOutput("generated_story", StorySchema)
            };

            story = await _mistralClient.SendAsync<GeneratedStory>(request, JsonOptions, "v1/chat/completions", cancellationToken);

            // LLMs cannot reliably count their own words, so the cap is
            // verified programmatically instead of trusted.
            var newWords = StoryWordChecker.FindNewWords(story.Story, allowedTerms, targetLanguageCode);

            if (newWords.Count <= StoryWordChecker.MaxNewWordsPerChapter || rewrites >= MaxRewrites)
            {
                // The displayed words are the model's newWords claims,
                // intersected with the code's computed new-words set: a
                // claim only survives if the word is really in the story
                // AND outside the learner's known vocabulary (every word
                // of a known phrase, the recap, the notebook batch).
                var computed = new HashSet<string>(newWords, StringComparer.Ordinal);

                var verified = story.NewWords
                    .Where(entry => computed.Contains(entry.Word.ToLowerInvariant()))
                    .Select(entry => entry with { Kind = NewWordKinds.Ai })
                    .ToList();

                return story with
                {
                    PotentiallyNewLanguage = newWords,
                    NewWords = verified
                };
            }

            rewrites++;
            messages.Add(new { role = "assistant", content = JsonSerializer.Serialize(story, JsonOptions) });
            messages.Add(new { role = "user", content = BuildRewriteFeedback(newWords) });
        }
    }

    private static string BuildUserPrompt(
        NotebookKnowledge knowledge,
        string knowledgeJson,
        string? previousStorySummary,
        IReadOnlyList<string>? acquiredVocabulary,
        IReadOnlyList<string>? acquiredGrammar,
        string definitionLanguage)
    {
        var continuationInstruction = string.IsNullOrWhiteSpace(previousStorySummary)
            ? "Write a very short, simple beginner story in "
            : "Continue the following story with its next chapter, at the same simple beginner level, in ";

        var constraints =
            "STRICT CONSTRAINTS: " +
            "1. Write at beginner level with short, simple sentences. " +
            "2. Write the title of THIS chapter in " + knowledge.TargetLanguage + " (never in another language), " +
            "maximum 4 words, favouring words from the supplied vocabulary. " +
            "3. Use ONLY the vocabulary and grammar supplied below, plus the basic function words needed to form sentences. " +
            "4. You may use AT MOST 3 words that are not in the supplied lists. " +
            "Count them before answering: if you find more than 3, rewrite the chapter with simpler wording instead of answering. " +
            "List every such word in potentiallyNewLanguage. " +
            "5. If the chapter contains dialogue, put every spoken line on its own line, " +
            "starting with an em dash followed by a space (example: — Hej, säger Anna.). " +
            "6. Include at most one emoji in the chapter. " +
            "7. Do not claim an item was used unless it appears in the story. " +
            "8. Write in the summary field 1 to 3 short sentences in " + knowledge.TargetLanguage + " " +
            "at the learner's level, recapping what happened in this chapter, so the next chapter can continue the story. " +
            "9. Never list proper nouns (people's names, place names) in newWords or potentiallyNewLanguage — " +
            "they are not vocabulary to learn; reuse the same character names as earlier chapters. " +
            "Every word of a supplied multi-word entry counts as known (knowing 'Vad heter du?' means 'heter' is known). " +
            "There are no unimportant words: every word of the story that is not in the supplied lists and not a proper noun " +
            "must appear in potentiallyNewLanguage and newWords, with no exception — even words that look basic or international. " +
            "Every entry in newWords is a content word that appears in the story and is not already known, " +
            "with a short " + definitionLanguage + " definition a beginner can understand. " +
            "10. In wordGlosses, give a short word-level " + definitionLanguage + " gloss for EVERY supplied-vocabulary word that appears in the story " +
            "(example: bor -> live; Solna -> Solna, an area of Stockholm). " +
            "The gloss is for the single word, never the notebook's full phrase translation.\n\n";

        var acquiredSection =
            acquiredVocabulary is { Count: > 0 } || acquiredGrammar is { Count: > 0 }
                ? "Vocabulary and grammar the learner has already acquired (reuse these first):\n" +
                  string.Join(", ", acquiredVocabulary ?? []) +
                  (acquiredGrammar is { Count: > 0 } ? "\nGrammar: " + string.Join(", ", acquiredGrammar) : "") +
                  "\n\n"
                : "";

        return continuationInstruction + knowledge.TargetLanguage +
               ".\n" + constraints +
               (string.IsNullOrWhiteSpace(previousStorySummary)
                   ? ""
                   : "Story so far (last chapter summary):\n" + previousStorySummary + "\n\n") +
               acquiredSection +
               "Approved notebook knowledge:\n" + knowledgeJson;
    }

    private static string BuildRewriteFeedback(IReadOnlyList<string> newWords)
    {
        return "Your chapter uses more than " + StoryWordChecker.MaxNewWordsPerChapter +
               " words that are not in the supplied lists: " + string.Join(", ", newWords) + ". " +
               "Rewrite the chapter with the same constraints and the same JSON output format, " +
               "replacing these words with supplied vocabulary wherever possible. " +
               "Keep the same story and the same summary.";
    }

    private static IEnumerable<string> CollectAllowedTerms(
        NotebookKnowledge knowledge,
        IReadOnlyList<string>? acquiredVocabulary,
        IReadOnlyList<string>? acquiredGrammar,
        string? previousStorySummary)
    {
        foreach (var term in acquiredVocabulary ?? [])
        {
            yield return term;
        }

        foreach (var grammar in acquiredGrammar ?? [])
        {
            yield return grammar;
        }

        // Words the recap mentions were presented in earlier chapters
        // (character names especially): FindNewWords tokenizes each
        // allowed term, so a whole sentence whitelists its words.
        if (!string.IsNullOrWhiteSpace(previousStorySummary))
        {
            yield return previousStorySummary;
        }

        foreach (var entry in knowledge.Vocabulary)
        {
            yield return entry.Term;
            yield return entry.BaseForm;
        }

        foreach (var point in knowledge.GrammarPoints)
        {
            yield return point.Name;
            foreach (var example in point.Examples)
            {
                yield return example;
            }
        }
    }

    private static object StructuredOutput(string name, string schema)
    {
        return new
        {
            type = "json_schema",
            json_schema = new
            {
                name,
                strict = true,
                schema = JsonSerializer.Deserialize<JsonElement>(schema)
            }
        };
    }
}
