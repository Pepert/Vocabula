using System.Text.Json;
using Vocabula.Core.Models;

namespace Vocabula.Core.AI;

public sealed class StoryGenerator : IStoryGenerator
{
    private const string StorySchema = "{\n" +
        "  \"type\": \"object\",\n" +
        "  \"additionalProperties\": false,\n" +
        "  \"required\": [\"title\", \"story\", \"usedVocabulary\", \"usedGrammarPoints\", \"potentiallyNewLanguage\"],\n" +
        "  \"properties\": {\n" +
        "    \"title\": { \"type\": \"string\" },\n" +
        "    \"story\": { \"type\": \"string\" },\n" +
        "    \"usedVocabulary\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } },\n" +
        "    \"usedGrammarPoints\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } },\n" +
        "    \"potentiallyNewLanguage\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }\n" +
        "  }\n" +
        "}";

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
        string? previousStorySummary = null,
        IReadOnlyList<string>? acquiredVocabulary = null,
        IReadOnlyList<string>? acquiredGrammar = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(knowledge);

        var knowledgeJson = JsonSerializer.Serialize(knowledge, JsonOptions);

        var continuationInstruction = string.IsNullOrWhiteSpace(previousStorySummary)
            ? "Write a very short, simple beginner story in "
            : "Continue the following story with its next chapter, at the same simple beginner level, in ";

        var constraints =
            "STRICT CONSTRAINTS: " +
            "1. Write at beginner level with short, simple sentences. " +
            "2. Use ONLY the vocabulary and grammar supplied below, plus the basic function words needed to form sentences. " +
            "3. You may introduce AT MOST 3 words that are not in the supplied lists; " +
            "list every such word in potentiallyNewLanguage. " +
            "4. Do not claim an item was used unless it appears in the story.\n\n";

        var acquiredSection =
            acquiredVocabulary is { Count: > 0 } || acquiredGrammar is { Count: > 0 }
                ? "Vocabulary and grammar the learner has already acquired (reuse these first):\n" +
                  string.Join(", ", acquiredVocabulary ?? []) +
                  (acquiredGrammar is { Count: > 0 } ? "\nGrammar: " + string.Join(", ", acquiredGrammar) : "") +
                  "\n\n"
                : "";

        var request = new
        {
            model = _storyModel,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = continuationInstruction + knowledge.TargetLanguage +
                             ".\n" + constraints +
                             (string.IsNullOrWhiteSpace(previousStorySummary)
                                 ? ""
                                 : "Story so far (last chapter summary):\n" + previousStorySummary + "\n\n") +
                             acquiredSection +
                             "Approved notebook knowledge:\n" + knowledgeJson
                }
            },
            temperature = 0.7,
            response_format = StructuredOutput("generated_story", StorySchema)
        };

        return await _mistralClient.SendAsync<GeneratedStory>(request, JsonOptions, "v1/chat/completions", cancellationToken);
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
