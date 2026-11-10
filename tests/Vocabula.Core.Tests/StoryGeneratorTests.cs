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
        "Hon säger hej.",
        [new NewWordDefinition("sade", "said")]);

    // "sade" is the only word outside the supplied "hej" vocabulary,
    // so this story passes the code-enforced word cap without a rewrite.
    private static GeneratedStory OverCapStory(string title) => new(
        title,
        "Banan klockan solen skiner morgonen.",
        ["hej"],
        [],
        [],
        "Han äter banan.",
        [new NewWordDefinition("klockan", "clock")]);

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
        // New words need learner-friendly definitions.
        Assert.Contains("short English definition", content);
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
    public async Task GenerateAsync_SchemaRequiresNewWordsWithDefinitions()
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

        Assert.Contains("newWords", schema.GetProperty("required")
            .EnumerateArray()
            .Select(required => required.GetString()));

        var newWords = schema.GetProperty("properties").GetProperty("newWords");
        Assert.Equal("array", newWords.GetProperty("type").GetString());
        var wordItem = newWords.GetProperty("items");
        Assert.Equal("string", wordItem.GetProperty("properties").GetProperty("word").GetProperty("type").GetString());
        Assert.Equal("string", wordItem.GetProperty("properties").GetProperty("definition").GetProperty("type").GetString());
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
        Assert.Equal(["sade"], story.NewWords.Select(entry => entry.Word));
        Assert.Equal("said", story.NewWords[0].Definition);
    }

    [Fact]
    public async Task GenerateAsync_DropsClaimsNotPresentInTheStory()
    {
        // Claims are verified against the text: words that are not in the
        // story are dropped, words that appear keep their definition even
        // when the code's computed list differs (the model knows what is
        // worth learning — e.g. it will not claim proper nouns).
        var client = new RecordingMistralClient();
        client.Enqueue(new GeneratedStory(
            "En dag",
            "Hej! Katten bananer solen.",
            ["hej"],
            [],
            [],
            "Katten säger hej.",
            [
                new NewWordDefinition("bananer", "bananas"),
                new NewWordDefinition("fiende", "enemy") // fantasy claim: not in the story
            ]));
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        var story = await generator.GenerateAsync(Knowledge, cancellationToken: default);

        // Computed new words (used for the cap): all words outside "hej".
        Assert.Equal(["katten", "bananer", "solen"], story.PotentiallyNewLanguage);
        // Displayed words: only the claim that appears in the text.
        var bananer = Assert.Single(story.NewWords);
        Assert.Equal("bananer", bananer.Word);
        Assert.Equal("bananas", bananer.Definition);
        Assert.Equal(NewWordKinds.Ai, bananer.Kind); // story-introduced vocabulary
    }

    [Fact]
    public async Task GenerateAsync_RecapWordsDoNotCountAgainstTheCap()
    {
        // The previous chapter's recap whitelists its words, so recurring
        // characters (Anna) are neither counted as new vocabulary nor retried.
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory("Anna kommer"));
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        var story = await generator.GenerateAsync(
            Knowledge,
            previousStorySummary: "Anna säger hej. Hon bor i Solna.",
            cancellationToken: default);

        Assert.Single(client.Requests); // no rewrite necessary
        // "säger" and "hon" are whitelisted by the recap.
        Assert.Equal(["sade"], story.PotentiallyNewLanguage);
    }

    [Fact]
    public async Task GenerateAsync_PromptExcludesProperNounsFromNewWords()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(ValidStory());
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var content = MessageContent(client);

        Assert.Contains("Never list proper nouns", content);
        Assert.Contains("reuse the same character names", content);
        Assert.Contains("Every word of a supplied multi-word entry counts as known", content);
    }

    [Fact]
    public async Task GenerateAsync_DropsClaimsOfWordsInsideKnownPhrases()
    {
        // Knowing "Vad heter du?" means "heter" is known: the model may
        // still claim it, but the word checker whitelists it and the
        // claim is dropped from the words shown to the learner.
        var client = new RecordingMistralClient();
        client.Enqueue(new GeneratedStory(
            "Vad heter du?",
            "Vad heter du? Jag heter Erik!",
            ["hej"],
            [],
            [],
            "Erik frågar.",
            [new NewWordDefinition("heter", "is called")]));
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        var story = await generator.GenerateAsync(
            Knowledge,
            previousStorySummary: null,
            acquiredVocabulary: ["Vad heter du?"],
            cancellationToken: default);

        Assert.Equal(["erik"], story.PotentiallyNewLanguage);
        Assert.Empty(story.NewWords);
    }

    [Fact]
    public async Task GenerateAsync_DefinitionCaseInsensitiveMatch()
    {
        var client = new RecordingMistralClient();
        client.Enqueue(new GeneratedStory(
            "En dag",
            "Hej! Solen skiner.",
            ["hej"],
            [],
            [],
            "Solen skiner.",
            [new NewWordDefinition("Solen", "the sun")]));
        var generator = new StoryGenerator(client, "mistral-medium-latest");

        var story = await generator.GenerateAsync(Knowledge, cancellationToken: default);

        var solen = Assert.Single(story.NewWords);
        Assert.Equal("solen", solen.Word.ToLowerInvariant());
        Assert.Equal("the sun", solen.Definition);
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
