using System.Net;
using System.Net.Http.Json;
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

        using var form = AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 11);
        using var response = await client.PostAsync("/api/knowledge/extract", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_FourthChapterOnSameDay_Returns429WithRetryAfter()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();

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
    public async Task GetChapters_ReturnsChaptersInReadingOrder()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();

        using var form = AuthenticatedWebApplicationFactory.NotebookForm(imageCount: 1);
        var knowledge = await (await client.PostAsync("/api/knowledge/extract", form))
            .Content.ReadFromJsonAsync<NotebookKnowledge>(JsonOptions);
        Assert.NotNull(knowledge);

        for (var i = 0; i < 2; i++)
        {
            using var generate = await client.PostAsync(
                "/api/stories/generate",
                AuthenticatedWebApplicationFactory.KnowledgeJson(knowledge));
            Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
        }

        using var chaptersResponse = await client.GetAsync("/api/stories/chapters");

        Assert.Equal(HttpStatusCode.OK, chaptersResponse.StatusCode);

        var chapters = await chaptersResponse.Content
            .ReadFromJsonAsync<ChaptersStub>(JsonOptions);

        Assert.NotNull(chapters);
        Assert.Equal([1, 2], chapters.Chapters.Select(c => c.ChapterNumber).ToArray());
        Assert.All(chapters.Chapters, chapter => Assert.Equal("Hej", chapter.StoryTitle));
    }

    private sealed record ChaptersStub(ChapterStub[] Chapters);

    private sealed record ChapterStub(int ChapterNumber, string StoryTitle, string Content, DateTimeOffset CreatedAt);

    private sealed record HttpValidationProblemDetailsStub(string? Title);
}
