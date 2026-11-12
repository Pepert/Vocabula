using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Vocabula.Core.Models;

namespace Vocabula.Api.Tests;

/// <summary>
/// Integration tests of the authenticated story flow against the real API
/// with an in-memory database: batch extraction, the daily chapter limit,
/// and the chapter history endpoint.
/// </summary>
public sealed class StoryFlowTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Extract_MultipleImages_ReturnsMergedKnowledge()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.SeedUser(learningLanguage: "sv");

        using var form = AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 3);
        using var response = await client.PostAsync("/api/knowledge/extract", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var knowledge = await response.Content.ReadFromJsonAsync<NotebookKnowledge>(JsonOptions);

        Assert.NotNull(knowledge);
        // Both images extract the same knowledge; the merge deduplicates it.
        Assert.Single(knowledge.Vocabulary);
        Assert.Equal("hej", knowledge.Vocabulary[0].Term);
    }

    [Fact]
    public async Task Extract_MoreThanTenImages_Returns400()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.SeedUser(learningLanguage: "sv");

        using var form = AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 11);
        using var response = await client.PostAsync("/api/knowledge/extract", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_FourthChapterOnSameDay_Returns429WithRetryAfter()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.SeedUser(learningLanguage: "sv");

        // Create the learning profile by extracting once.
        using var form = AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 1);
        var knowledge = await (await client.PostAsync("/api/knowledge/extract", form))
            .Content.ReadFromJsonAsync<NotebookKnowledge>(JsonOptions);
        Assert.NotNull(knowledge);

        // Three chapters within the same minute: allowed.
        for (var i = 0; i < 3; i++)
        {
            using var generate = await client.PostAsync(
                "/api/stories/generate",
                AuthenticatedWebApplicationFactory.KnowledgeJson(knowledge));
            Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
        }

        // The fourth is over the daily limit.
        using var rejected = await client.PostAsync(
            "/api/stories/generate",
            AuthenticatedWebApplicationFactory.KnowledgeJson(knowledge));

        Assert.Equal((HttpStatusCode)429, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"), "the 429 must carry a Retry-After header");

        var problem = await rejected.Content.ReadFromJsonAsync<HttpValidationProblemDetailsStub>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("Daily story limit reached", problem.Title);
    }

    [Fact]
    public async Task Extract_AfterThirdChapterOnSameDay_Returns429BeforeTranscribing()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.SeedUser(learningLanguage: "sv");

        // Three chapters: the day's quota is spent.
        using var form = AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 1);
        var knowledge = await (await client.PostAsync("/api/knowledge/extract", form))
            .Content.ReadFromJsonAsync<NotebookKnowledge>(JsonOptions);
        Assert.NotNull(knowledge);

        for (var i = 0; i < 3; i++)
        {
            using var generate = await client.PostAsync(
                "/api/stories/generate",
                AuthenticatedWebApplicationFactory.KnowledgeJson(knowledge));
            Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
        }

        // The next upload is rejected before any transcription runs.
        // The fake extractor throws if reached: too-late enforcement fails the test.
        factory.EnsureExtractorThrows = true;
        using var rejected = await client.PostAsync(
            "/api/knowledge/extract",
            AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 1));

        Assert.Equal((HttpStatusCode)429, rejected.StatusCode);
        Assert.True(rejected.Headers.Contains("Retry-After"), "the 429 must carry a Retry-After header");
    }

    [Fact]
    public async Task GetChapters_ReturnsChaptersInReadingOrder()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.SeedUser(learningLanguage: "sv");

        using var form = AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 1);
        var knowledge = await (await client.PostAsync("/api/knowledge/extract", form))
            .Content.ReadFromJsonAsync<NotebookKnowledge>(JsonOptions);
        Assert.NotNull(knowledge);

        var chapterNumbers = new List<int>();
        for (var i = 0; i < 2; i++)
        {
            using var generate = await client.PostAsync(
                "/api/stories/generate",
                AuthenticatedWebApplicationFactory.KnowledgeJson(knowledge));
            Assert.Equal(HttpStatusCode.OK, generate.StatusCode);

            // The generate response carries the new chapter's number so the
            // client can redirect without re-fetching the chapter list.
            var generated = await generate.Content.ReadFromJsonAsync<GenerateStub>(JsonOptions);
            Assert.NotNull(generated);
            chapterNumbers.Add(generated.ChapterNumber);
        }
        Assert.Equal([1, 2], chapterNumbers);

        using var chaptersResponse = await client.GetAsync("/api/stories/chapters");

        Assert.Equal(HttpStatusCode.OK, chaptersResponse.StatusCode);

        var chapters = await chaptersResponse.Content
            .ReadFromJsonAsync<ChaptersStub>(JsonOptions);

        Assert.NotNull(chapters);
        Assert.Equal([1, 2], chapters.Chapters.Select(c => c.ChapterNumber).ToArray());
        Assert.All(chapters.Chapters, chapter =>
        {
            Assert.Equal("Hej", chapter.StoryTitle);
            Assert.Equal("Hej", chapter.Title); // each chapter carries its own title
        });

        // Chapter 1: the story-introduced word (ai) plus the notebook word
        // appearing for the first time (extracted). The green word's
        // definition is the model's word-level gloss, which wins over the
        // notebook's own translation.
        var first = chapters.Chapters[0].NewWords;
        Assert.Equal(
            [("sade", "said", "ai"), ("hej", "hello (word-level gloss)", "extracted")],
            first.Select(word => (word.Word, word.Definition!, word.Kind!)).ToArray());

        // Chapter 2: "hej" was already seen in chapter 1, so only the
        // story-introduced word remains — first appearances only.
        var second = chapters.Chapters[1].NewWords;
        Assert.Equal(
            [("sade", "said", "ai")],
            second.Select(word => (word.Word, word.Definition!, word.Kind!)).ToArray());
    }

    [Fact]
    public async Task Extract_WithoutLearningLanguage_Returns400BeforeTranscribing()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        // No SeedUser: the auto-created account has chosen no language yet.

        // The fake extractor throws if reached: enforcement must happen before.
        factory.EnsureExtractorThrows = true;
        using var response = await client.PostAsync(
            "/api/knowledge/extract",
            AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 1));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetailsStub>(JsonOptions);
        Assert.NotNull(problem);
        Assert.Equal("Learning language not chosen", problem.Title);
    }

    [Fact]
    public async Task Generate_WithoutLearningLanguage_Returns400()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        // No SeedUser: the auto-created account has chosen no language yet.

        using var response = await client.PostAsync(
            "/api/stories/generate",
            AuthenticatedWebApplicationFactory.KnowledgeJson(
                new NotebookKnowledge("Swedish", "page", [], [], [])));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SwitchingLanguage_SeparatesStoriesAndSharesTheQuota()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.SeedUser(learningLanguage: "sv");

        // One Swedish chapter.
        using var swedishForm = AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 1);
        var knowledge = await (await client.PostAsync("/api/knowledge/extract", swedishForm))
            .Content.ReadFromJsonAsync<NotebookKnowledge>(JsonOptions);
        Assert.NotNull(knowledge);
        using (var generate = await client.PostAsync(
            "/api/stories/generate", AuthenticatedWebApplicationFactory.KnowledgeJson(knowledge)))
        {
            Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
        }

        // Switch to French: a separate story begins at chapter 1.
        using (var switchResponse = await client.PatchAsync(
            "/api/users/me/learning-language",
            new StringContent(
                JsonSerializer.Serialize(new { learningLanguage = "fr" }, JsonOptions),
                Encoding.UTF8,
                "application/json")))
        {
            Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);
        }

        using var frenchForm = AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 1);
        knowledge = await (await client.PostAsync("/api/knowledge/extract", frenchForm))
            .Content.ReadFromJsonAsync<NotebookKnowledge>(JsonOptions);
        using (var generate = await client.PostAsync(
            "/api/stories/generate", AuthenticatedWebApplicationFactory.KnowledgeJson(knowledge)))
        {
            var generated = await generate.Content.ReadFromJsonAsync<GenerateStub>(JsonOptions);
            Assert.NotNull(generated);
            Assert.Equal(1, generated.ChapterNumber); // the French story's own chapter 1
        }

        // The chapter list shows only the active language's chapters.
        using var chaptersResponse = await client.GetAsync("/api/stories/chapters");
        var chapters = await chaptersResponse.Content.ReadFromJsonAsync<ChaptersStub>(JsonOptions);
        Assert.NotNull(chapters);
        Assert.Equal([1], chapters.Chapters.Select(c => c.ChapterNumber).ToArray());

        // The quota is shared across languages: with one Swedish and one
        // French chapter spent, only today's third chapter is left.
        using (var generate = await client.PostAsync(
            "/api/stories/generate", AuthenticatedWebApplicationFactory.KnowledgeJson(knowledge)))
        {
            Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
        }

        using var rejected = await client.PostAsync(
            "/api/stories/generate", AuthenticatedWebApplicationFactory.KnowledgeJson(knowledge));
        Assert.Equal((HttpStatusCode)429, rejected.StatusCode);
    }

    private sealed record GenerateStub(int ChapterNumber);

    private sealed record ChaptersStub(ChapterStub[] Chapters);

    private sealed record ChapterStub(
        int ChapterNumber,
        string StoryTitle,
        string Title,
        string Content,
        NewWordStub[] NewWords,
        DateTimeOffset CreatedAt);

    private sealed record NewWordStub(string Word, string? Definition, string? Kind);

    private sealed record HttpValidationProblemDetailsStub(string? Title);
}
