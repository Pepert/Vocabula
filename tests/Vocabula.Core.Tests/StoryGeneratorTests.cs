using System.Text.Json;
using Vocabula.Core.AI;
using Vocabula.Core.Models;

namespace Vocabula.Core.Tests;

public sealed class StoryGeneratorTests
{
    private static readonly NotebookKnowledge Knowledge = new(
        "Swedish",
        "a notebook page",
        [new VocabularyEntry("hej", "hej", "hello", "interjection", "seen on page")],
        [],
        []);

    private sealed class RecordingMistralClient : IMistralClient
    {
        public object? LastRequest { get; private set; }

        public Task<T> SendAsync<T>(
            object request,
            JsonSerializerOptions jsonOptions,
            string endpointPath = "v1/chat/completions",
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            var canned = new GeneratedStory(
                "A morning in Stockholm",
                "Hej! sade hon...",
                ["hej"],
                [],
                []);

            return Task.FromResult((T)(object)canned);
        }
    }

    private static string MessageContent(RecordingMistralClient client)
    {
        var document = JsonSerializer.SerializeToDocument(client.LastRequest!);
        return document.RootElement.GetProperty("messages")[0].GetProperty("content").GetString()!;
    }

    [Fact]
    public async Task GenerateAsync_WithPreviousSummary_PromptsForContinuation()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(Knowledge, "Nora found a red envelope under the door...", cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("Continue the following story with its next chapter", content);
        Assert.Contains("Story so far", content);
        Assert.Contains("Nora found a red envelope under the door...", content);
        Assert.Contains("hej", content); // the knowledge is still part of the prompt
    }

    [Fact]
    public async Task GenerateAsync_WithoutPreviousSummary_PromptsForNewStory()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("Write a short, coherent, engaging beginner story", content);
        Assert.DoesNotContain("Continue the following story", content);
        Assert.DoesNotContain("Story so far", content);
    }

    [Fact]
    public async Task GenerateAsync_WithPreviousSummary_ReturnsGeneratedStory()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        var story = await generator.GenerateAsync(Knowledge, "previous chapter summary");

        Assert.Equal("A morning in Stockholm", story.Title);
        Assert.Contains("Hej", story.Story);
    }
}
