using System.Text.Json;
using Vocabula.Core.Models;

namespace Vocabula.Core.AI;

public sealed class NotebookKnowledgeExtractor : INotebookKnowledgeExtractor
{
    private const string KnowledgeSchema = "{\n" +
        "  \"type\": \"object\",\n" +
        "  \"additionalProperties\": false,\n" +
        "  \"required\": [\"targetLanguage\", \"sourceSummary\", \"vocabulary\", \"grammarPoints\", \"uncertainItems\"],\n" +
        "  \"properties\": {\n" +
        "    \"targetLanguage\": { \"type\": \"string\" },\n" +
        "    \"sourceSummary\": { \"type\": \"string\" },\n" +
        "    \"vocabulary\": {\n" +
        "      \"type\": \"array\",\n" +
        "      \"items\": {\n" +
        "        \"type\": \"object\",\n" +
        "        \"additionalProperties\": false,\n" +
        "        \"required\": [\"term\", \"baseForm\", \"meaning\", \"partOfSpeech\", \"evidence\"],\n" +
        "        \"properties\": {\n" +
        "          \"term\": { \"type\": \"string\" },\n" +
        "          \"baseForm\": { \"type\": \"string\" },\n" +
        "          \"meaning\": { \"type\": \"string\" },\n" +
        "          \"partOfSpeech\": { \"type\": \"string\" },\n" +
        "          \"evidence\": { \"type\": \"string\" }\n" +
        "        }\n" +
        "      }\n" +
        "    },\n" +
        "    \"grammarPoints\": {\n" +
        "      \"type\": \"array\",\n" +
        "      \"items\": {\n" +
        "        \"type\": \"object\",\n" +
        "        \"additionalProperties\": false,\n" +
        "        \"required\": [\"name\", \"explanation\", \"examples\"],\n" +
        "        \"properties\": {\n" +
        "          \"name\": { \"type\": \"string\" },\n" +
        "          \"explanation\": { \"type\": \"string\" },\n" +
        "          \"examples\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }\n" +
        "        }\n" +
        "      }\n" +
        "    },\n" +
        "    \"uncertainItems\": { \"type\": \"array\", \"items\": { \"type\": \"string\" } }\n" +
        "  }\n" +
        "}";

    private readonly IMistralClient _mistralClient;
    private readonly string _visionModel;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public NotebookKnowledgeExtractor(IMistralClient mistralClient, string visionModel)
    {
        ArgumentNullException.ThrowIfNull(mistralClient);
        ArgumentException.ThrowIfNullOrWhiteSpace(visionModel);

        _mistralClient = mistralClient;
        _visionModel = visionModel;
    }

    public async Task<NotebookKnowledge> ExtractAsync(
        Stream image,
        string mediaType,
        CancellationToken cancellationToken = default)
    {
        var imageDataUri = LocalImage.ToDataUri(image, mediaType);

        var request = new
        {
            model = _visionModel,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "text",
                            text = "Analyze this language-learning notebook page. Extract only vocabulary and " +
                                   "grammar supported by visible evidence. Preserve the studied-language spelling. " +
                                   "Give meanings and explanations in English. Put unreadable or ambiguous content " +
                                   "in uncertainItems instead of guessing. Return JSON matching the supplied schema."
                        },
                        new
                        {
                            type = "image_url",
                            image_url = imageDataUri
                        }
                    }
                }
            },
            temperature = 0,
            response_format = StructuredOutput("notebook_knowledge", KnowledgeSchema)
        };

        return await _mistralClient.SendAsync<NotebookKnowledge>(request, JsonOptions, "v1/chat/completions", cancellationToken);
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
