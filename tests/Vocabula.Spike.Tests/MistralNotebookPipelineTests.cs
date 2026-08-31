using System.Net;
using System.Text;
using Vocabula.Spike;

namespace Vocabula.Spike.Tests;

public sealed class MistralNotebookPipelineTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vocabula-{Guid.NewGuid():N}");

    [Fact]
    public async Task RunAsync_ExtractsKnowledgeThenGeneratesStory()
    {
        Directory.CreateDirectory(_directory);
        var imagePath = Path.Combine(_directory, "notes.jpg");
        await File.WriteAllBytesAsync(imagePath, "fake-image"u8.ToArray());

        var extractionJson = """
            {
              "targetLanguage": "Swedish",
              "sourceSummary": "Beginner notes about introductions",
              "vocabulary": [
                {
                  "term": "hej",
                  "baseForm": "hej",
                  "meaning": "hello",
                  "partOfSpeech": "interjection",
                  "evidence": "hej"
                }
              ],
              "grammarPoints": [
                {
                  "name": "present tense",
                  "explanation": "Present-tense verbs do not change by person.",
                  "examples": ["jag heter"]
                }
              ],
              "uncertainItems": []
            }
            """;
        var storyJson = """
            {
              "title": "Ett möte",
              "story": "Hej! Jag heter Anna.",
              "usedVocabulary": ["hej"],
              "usedGrammarPoints": ["present tense"],
              "potentiallyNewLanguage": ["möte"]
            }
            """;

        var handler = new RecordingHandler(
            CompletionResponse(extractionJson),
            CompletionResponse(storyJson));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.mistral.ai/")
        };
        var pipeline = new MistralNotebookPipeline(
            httpClient,
            apiKey: "test-key",
            visionModel: "vision-model",
            storyModel: "story-model");

        var result = await pipeline.RunAsync(imagePath);

        Assert.Equal("Swedish", result.Knowledge.TargetLanguage);
        Assert.Equal("hej", Assert.Single(result.Knowledge.Vocabulary).Term);
        Assert.Equal("Ett möte", result.Story.Title);
        Assert.Equal("Hej! Jag heter Anna.", result.Story.Story);

        Assert.Equal(2, handler.RequestBodies.Count);
        Assert.Contains("data:image/jpeg;base64,ZmFrZS1pbWFnZQ==", handler.RequestBodies[0]);
        Assert.Contains("json_schema", handler.RequestBodies[0]);
        Assert.Contains("vision-model", handler.RequestBodies[0]);
        Assert.Contains("hej", handler.RequestBodies[1]);
        Assert.Contains("present tense", handler.RequestBodies[1]);
        Assert.Contains("story-model", handler.RequestBodies[1]);
    }

    private static HttpResponseMessage CompletionResponse(string content)
    {
        var responseBody = System.Text.Json.JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content } }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}
