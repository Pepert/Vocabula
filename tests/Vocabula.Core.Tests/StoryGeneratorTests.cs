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
        private readonly Queue<GeneratedStory> _responses = new();

        public List<object> Requests { get; } = [];

        public void Enqueue(GeneratedStory response) => _responses.Enqueue(response);

        public Task<T> SendAsync<T>(
            object request,
            JsonSerializerOptions jsonOptions,
            string endpointPath = "v1/chat/completions",
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            return Task.FromResult((T)(object)_responses.Dequeue());
        }
    }

    private static string MessageContent(RecordingMistralClient client, int index = 0)
    {
        var document = JsonSerializer.SerializeToDocument(client.Requests[index]);
        return document.RootElement.GetProperty("messages")[index].GetProperty("content").GetString()!;
    }

    private static GeneratedStory ValidStory(string title = "A morning in Stockholm") => new(
        title,
        "Hej! sade hon...",
        ["hej"],
        [],
        [],
        "Hon säger hej.");

    // "sade" is the only word outside the supplied "hej" vocabulary,
    // so this story passes the code-enforced word cap without a rewrite.
    private static GeneratedStory OverCapStory(string title) => new(
        title,
        "Banan klockan solen skiner morgonen.",
        ["hej"],
        [],
        [],
        "Han äter banan.");

    [Fact]
    public async Task GenerateAsync_WithPreviousSummary_PromptsForContinuation()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

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
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

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
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        var story = await generator.GenerateAsync(Knowledge, "previous chapter summary");

        Assert.Equal("A morning in Stockholm", story.Title);
        Assert.Contains("Hej", story.Story);
        Assert.Equal("Hon säger hej.", story.Summary);
    }

    [Fact]
    public async Task GenerateAsync_PromptLimitsNewVocabularyToThree()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("AT MOST 3 words", content);
        Assert.Contains("Use ONLY the vocabulary and grammar supplied", content);
        // The model must self-check the count instead of exceeding the cap.
        Assert.Contains("rewrite the chapter", content);
    }

    [Fact]
    public async Task GenerateAsync_PromptRequiresChapterTitleInTargetLanguage()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("Write the title of THIS chapter in Swedish", content);
        Assert.Contains("never in another language", content);
    }

    [Fact]
    public async Task GenerateAsync_PromptPutsDialogueLinesOnSeparateLines()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("put every spoken line on its own line", content);
        Assert.Contains("em dash", content);
    }

    [Fact]
    public async Task GenerateAsync_PromptLimitsEmojiToOne()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("at most one emoji", content);
    }

    [Fact]
    public async Task GenerateAsync_PromptRequestsChapterRecapInSummary()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("recapping what happened in this chapter", content);
    }

    [Fact]
    public async Task GenerateAsync_SummaryIsWrittenInTargetLanguageLevel()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("1 to 3 short sentences in Swedish", content);
        Assert.Contains("learner's level", content);
    }

    [Fact]
    public async Task GenerateAsync_SchemaRequiresSummaryField()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var schema = JsonSerializer.SerializeToDocument(client.Requests[0])
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
    public async Task GenerateAsync_WithAcquiredKnowledge_IncludesItInPrompt()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

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
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.DoesNotContain("already acquired", content);
    }

    [Fact]
    public async Task GenerateAsync_CompliantChapter_SingleCallAndComputedNewWords()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        var story = await generator.GenerateAsync(Knowledge, cancellationToken: default);

        Assert.Single(client.Requests);
        // The word check result replaces whatever the model claimed: only
        // "sade" is outside the "hej" vocabulary (hon is a function word).
        Assert.Equal(["sade"], story.PotentiallyNewLanguage);
    }

    [Fact]
    public async Task GenerateAsync_OverCapChapter_IsRewrittenWithFeedback()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(OverCapStory("Kapitel ett"));
        client.Enqueue(ValidStory("Kapitel ett, bättre"));
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        var story = await generator.GenerateAsync(Knowledge, cancellationToken: default);

        // The retry request replays the first attempt as an assistant
        // message, then points at the exact words that broke the cap.
        Assert.Equal(2, client.Requests.Count);
        var retry = JsonSerializer.SerializeToDocument(client.Requests[1])
            .RootElement
            .GetProperty("messages");
        Assert.Equal(3, retry.GetArrayLength());
        Assert.Equal("assistant", retry[1].GetProperty("role").GetString());
        Assert.Contains("Kapitel ett", retry[1].GetProperty("content").GetString());

        var feedback = retry[2].GetProperty("content").GetString()!;
        Assert.Equal("user", retry[2].GetProperty("role").GetString());
        Assert.Contains("klockan", feedback);
        Assert.Contains("morgonen", feedback);

        Assert.Equal("Kapitel ett, bättre", story.Title);
        Assert.Equal(["sade"], story.PotentiallyNewLanguage);
    }

    [Fact]
    public async Task GenerateAsync_RepeatedlyOverCap_ReturnsLatestChapterAfterMaxRetries()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(OverCapStory("Overlimit chapter one"));
        client.Enqueue(OverCapStory("Overlimit chapter two"));
        client.Enqueue(OverCapStory("Overlimit chapter three"));
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        var story = await generator.GenerateAsync(Knowledge, cancellationToken: default);

        // Initial attempt plus two rewrites, then give up gracefully.
        Assert.Equal(3, client.Requests.Count);
        Assert.Equal("Overlimit chapter three", story.Title);
        ConfirmThenRewrite(client.Requests[1], firstOffending: "Overlimit chapter one");
        ConfirmThenRewrite(client.Requests[2], firstOffending: "Overlimit chapter two");
    }

    private static void ConfirmThenRewrite(object request, string firstOffending)
    {
        // The attempt that broke the cap is replayed as the assistant message.
        var messages = JsonSerializer.SerializeToDocument(request)
            .RootElement
            .GetProperty("messages");
        Assert.Contains(firstOffending, messages[messages.GetArrayLength() - 2]
            .GetProperty("content")
            .GetString());
    }
}
