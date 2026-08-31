using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Vocabula.Spike;

public sealed class MistralNotebookPipeline
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private const string KnowledgeSchema = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["targetLanguage", "sourceSummary", "vocabulary", "grammarPoints", "uncertainItems"],
          "properties": {
            "targetLanguage": { "type": "string" },
            "sourceSummary": { "type": "string" },
            "vocabulary": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["term", "baseForm", "meaning", "partOfSpeech", "evidence"],
                "properties": {
                  "term": { "type": "string" },
                  "baseForm": { "type": "string" },
                  "meaning": { "type": "string" },
                  "partOfSpeech": { "type": "string" },
                  "evidence": { "type": "string" }
                }
              }
            },
            "grammarPoints": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "required": ["name", "explanation", "examples"],
                "properties": {
                  "name": { "type": "string" },
                  "explanation": { "type": "string" },
                  "examples": { "type": "array", "items": { "type": "string" } }
                }
              }
            },
            "uncertainItems": { "type": "array", "items": { "type": "string" } }
          }
        }
        """;

    private const string StorySchema = """
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["title", "story", "usedVocabulary", "usedGrammarPoints", "potentiallyNewLanguage"],
          "properties": {
            "title": { "type": "string" },
            "story": { "type": "string" },
            "usedVocabulary": { "type": "array", "items": { "type": "string" } },
            "usedGrammarPoints": { "type": "array", "items": { "type": "string" } },
            "potentiallyNewLanguage": { "type": "array", "items": { "type": "string" } }
          }
        }
        """;

    private readonly HttpClient _httpClient;
    private readonly string _visionModel;
    private readonly string _storyModel;

    public MistralNotebookPipeline(
        HttpClient httpClient,
        string apiKey,
        string visionModel,
        string storyModel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(visionModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(storyModel);

        _httpClient = httpClient;
        _visionModel = visionModel;
        _storyModel = storyModel;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<SpikeOutput> RunAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var knowledge = await ExtractKnowledgeAsync(imagePath, cancellationToken);
        var story = await GenerateStoryAsync(knowledge, cancellationToken);
        return new SpikeOutput(knowledge, story);
    }

    private async Task<NotebookKnowledge> ExtractKnowledgeAsync(
        string imagePath,
        CancellationToken cancellationToken)
    {
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
                            text = """
                                Analyze this language-learning notebook page. Extract only vocabulary and
                                grammar supported by visible evidence. Preserve the studied-language spelling.
                                Give meanings and explanations in English. Put unreadable or ambiguous content
                                in uncertainItems instead of guessing. Return JSON matching the supplied schema.
                                """
                        },
                        new
                        {
                            type = "image_url",
                            image_url = LocalImage.ToDataUri(imagePath)
                        }
                    }
                }
            },
            temperature = 0,
            response_format = StructuredOutput("notebook_knowledge", KnowledgeSchema)
        };

        return await SendAsync<NotebookKnowledge>(request, cancellationToken);
    }

    private async Task<GeneratedStory> GenerateStoryAsync(
        NotebookKnowledge knowledge,
        CancellationToken cancellationToken)
    {
        var knowledgeJson = JsonSerializer.Serialize(knowledge, JsonOptions);
        var request = new
        {
            model = _storyModel,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = $$"""
                        Write a short, coherent, engaging beginner story in {{knowledge.TargetLanguage}}.
                        Prioritize the supplied vocabulary and grammar. Keep unavoidable new language to a
                        minimum and report it honestly in potentiallyNewLanguage. Do not claim an item was used
                        unless it appears in the story. Return JSON matching the supplied schema.

                        Approved notebook knowledge:
                        {{knowledgeJson}}
                        """
                }
            },
            temperature = 0.7,
            response_format = StructuredOutput("generated_story", StorySchema)
        };

        return await SendAsync<GeneratedStory>(request, cancellationToken);
    }

    private async Task<T> SendAsync<T>(object request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            "v1/chat/completions",
            request,
            JsonOptions,
            cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Mistral returned {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}");
        }

        var completion = JsonSerializer.Deserialize<ChatCompletion>(responseBody, JsonOptions)
                         ?? throw new InvalidOperationException("Mistral returned an empty response.");
        var content = completion.Choices.FirstOrDefault()?.Message.Content
                      ?? throw new InvalidOperationException("Mistral returned no completion content.");

        return JsonSerializer.Deserialize<T>(content, JsonOptions)
               ?? throw new InvalidOperationException("Mistral returned empty structured JSON.");
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

    private sealed record ChatCompletion(IReadOnlyList<Choice> Choices);
    private sealed record Choice(Message Message);
    private sealed record Message(string Content);
}
