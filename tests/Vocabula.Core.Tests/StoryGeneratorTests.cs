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

        Assert.Contains("Write a very short, simple beginner story", content);
        Assert.DoesNotContain("Continue the following story", content);
        Assert.DoesNotContain("Story so far", content);
    }

    [Fact]
    public async Task GenerateAsync_ReturnsGeneratedStory()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        var story = await generator.GenerateAsync(Knowledge, "previous chapter summary");

        Assert.Equal("A morning in Stockholm", story.Title);
        Assert.Contains("Hej", story.Story);
    }

    [Fact]
    public async Task GenerateAsync_PromptLimitsNewVocabularyToThree()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("AT MOST 3 words", content);
        Assert.Contains("Use ONLY the vocabulary and grammar supplied", content);
    }

    [Fact]
    public async Task GenerateAsync_WithAcquiredKnowledge_IncludesItInPrompt()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(
            Knowledge,
            previousStorySummary: null,
            acquiredVocabulary: ["hus", "katt", "springer"],
            acquiredGrammar: ["present tense"],
            cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("already acquired", content);
        Assert.Contains("hus", content);
        Assert.Contains("katt", content);
        Assert.Contains("springer", content);
        Assert.Contains("present tense", content);
    }

    [Fact]
    public async Task GenerateAsync_WithoutAcquiredKnowledge_OmitsAcquiredSection()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.DoesNotContain("already acquired", content);
    }
}
