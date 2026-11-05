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
                [],
                "Hon säger hej.");

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
        Assert.Equal("Hon säger hej.", story.Summary);
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
        // The model must self-check the count instead of exceeding the cap.
        Assert.Contains("rewrite the chapter", content);
    }

    [Fact]
    public async Task GenerateAsync_PromptRequiresTitleInTargetLanguage()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("Write the title in Swedish", content);
        Assert.Contains("never in another language", content);
    }

    [Fact]
    public async Task GenerateAsync_PromptEnforcesDialogueScriptFormatting()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("start every spoken line with an em dash", content);
    }

    [Fact]
    public async Task GenerateAsync_PromptLimitsEmojiToOne()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("at most one emoji", content);
    }

    [Fact]
    public async Task GenerateAsync_PromptRequestsChapterRecapInSummary()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("recapping what happened in this chapter", content);
    }

    [Fact]
    public async Task GenerateAsync_SchemaRequiresSummaryField()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var schema = JsonSerializer.SerializeToDocument(client.LastRequest!)
            .RootElement
            .GetProperty("response_format")
            .GetProperty("json_schema")
            .GetProperty("schema");

        Assert.Contains("summary", schema.GetProperty("required")
            .EnumerateArray()
            .Select(required => required.GetString()));
        Assert.Equal("string", schema.GetProperty("properties")
            .GetProperty("summary")
            .GetProperty("type")
            .GetString());
    }

    [Fact]
    public async Task GenerateAsync_SummaryIsWrittenInTargetLanguageLevel()
    {
        var client = new RecordingMistralClient();
        var generator = new StoryGenerator(client, "mistral-small-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("1 to 3 short sentences in Swedish", content);
        Assert.Contains("learner's level", content);
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
