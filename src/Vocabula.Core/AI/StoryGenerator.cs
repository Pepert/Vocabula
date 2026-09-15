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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(knowledge);

        var knowledgeJson = JsonSerializer.Serialize(knowledge, JsonOptions);

        var request = new
        {
            model = _storyModel,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = "Write a short, coherent, engaging beginner story in " + knowledge.TargetLanguage +
                             ". Prioritize the supplied vocabulary and grammar. Keep unavoidable new language to a " +
                             "minimum and report it honestly in potentiallyNewLanguage. Do not claim an item was used " +
                             "unless it appears in the story. Return JSON matching the supplied schema.\n\n" +
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
