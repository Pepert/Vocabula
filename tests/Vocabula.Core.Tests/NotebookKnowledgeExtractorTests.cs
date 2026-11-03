using System.Text.Json;
using Vocabula.Core.AI;
using Vocabula.Core.Models;

namespace Vocabula.Core.Tests;

public sealed class NotebookKnowledgeExtractorTests
{
    private sealed class RecordingMistralClient : IMistralClient
    {
        public object? LastRequest { get; private set; }
        public string? LastEndpointPath { get; private set; }

        public Task<T> SendAsync<T>(
            object request,
            JsonSerializerOptions jsonOptions,
            string endpointPath = "v1/chat/completions",
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            LastEndpointPath = endpointPath;

            var canned = new NotebookKnowledge(
                "Swedish",
                "a notebook page",
                [],
                [],
                []);

            return Task.FromResult((T)(object)canned);
        }
    }

    private static Stream JpegImage()
    {
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        return new MemoryStream(bytes);
    }

    [Fact]
    public async Task ExtractAsync_UsesChatCompletionsEndpoint()
    {
        var client = new RecordingMistralClient();
        var extractor = new NotebookKnowledgeExtractor(client, "mistral-small-latest");

        await extractor.ExtractAsync(JpegImage(), "image/jpeg");

        Assert.Equal("v1/chat/completions", client.LastEndpointPath);
    }

    [Fact]
    public async Task ExtractAsync_SendsImageAndModelInRequest()
    {
        var client = new RecordingMistralClient();
        var extractor = new NotebookKnowledgeExtractor(client, "mistral-small-latest");

        await extractor.ExtractAsync(JpegImage(), "image/jpeg");

        var document = JsonSerializer.SerializeToDocument(client.LastRequest!);
        Assert.Equal("mistral-small-latest", document.RootElement.GetProperty("model").GetString());
        var content = document.RootElement.GetProperty("messages")[0].GetProperty("content");
        // The message contains the image as a data URI part alongside the text part.
        var hasImagePart = false;
        foreach (var part in content.EnumerateArray())
        {
            if (part.TryGetProperty("type", out var type) && type.GetString() == "image_url")
            {
                hasImagePart = true;
                Assert.StartsWith("data:image/jpeg;base64,", part.GetProperty("image_url").GetString());
            }
        }
        Assert.True(hasImagePart, "the request must contain an image_url part");
    }
}
