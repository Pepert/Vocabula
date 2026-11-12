using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Vocabula.Core.Domain;
using Vocabula.Infrastructure.Persistence;

namespace Vocabula.Api.Tests;

/// <summary>
/// Account-level preferences: the language stored on the user's record,
/// served by /api/users/me and updated through its PATCH version.
/// </summary>
public sealed class UsersControllerTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task GetMe_ReturnsLanguageFromTheUserRecord()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.SeedUser(language: "fr");

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeStub>(JsonOptions);
        Assert.NotNull(me);
        Assert.Equal("fr", me.Language);
    }

    [Fact]
    public async Task GetMe_WithoutLanguagePreference_ReturnsNull()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();

        var me = await (await client.GetAsync("/api/users/me"))
            .Content.ReadFromJsonAsync<MeStub>(JsonOptions);

        Assert.NotNull(me);
        Assert.Null(me.Language);
    }

    [Fact]
    public async Task UpdateMe_PatchLanguage_PersistsAndServesIt()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();

        var updated = await client.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateRequestStub(Language: "fr"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        // Rounds-tripped through the database, not just echoed: fetch again.
        var me = await (await client.GetAsync("/api/users/me"))
            .Content.ReadFromJsonAsync<MeStub>(JsonOptions);
        Assert.NotNull(me);
        Assert.Equal("fr", me.Language);
    }

    [Fact]
    public async Task UpdateMe_WithUnsupportedLanguage_Returns400()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();

        var rejected = await client.PatchAsJsonAsync(
            "/api/users/me",
            new UpdateRequestStub(Language: "sv"));

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var me = await (await client.GetAsync("/api/users/me"))
            .Content.ReadFromJsonAsync<MeStub>(JsonOptions);
        Assert.NotNull(me);
        Assert.Null(me.Language); // unchanged
    }

    private sealed record MeStub(Guid UserId, string? Language, string? LearningLanguage);

    private sealed record UpdateRequestStub(string? Language);

    private sealed record UpdateLearningLanguageRequestStub(string LearningLanguage);

    [Fact]
    public async Task UpdateLearningLanguage_PersistsAndServesIt()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();

        var updated = await client.PatchAsJsonAsync(
            "/api/users/me/learning-language",
            new UpdateLearningLanguageRequestStub("es"));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        // Rounds-tripped through the database, not just echoed: fetch again.
        var me = await (await client.GetAsync("/api/users/me"))
            .Content.ReadFromJsonAsync<MeStub>(JsonOptions);
        Assert.NotNull(me);
        Assert.Equal("es", me.LearningLanguage);
        Assert.Null(me.Language); // the UI language is a separate concern
    }

    [Fact]
    public async Task UpdateLearningLanguage_WithUnsupportedCode_Returns400()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();

        var rejected = await client.PatchAsJsonAsync(
            "/api/users/me/learning-language",
            new UpdateLearningLanguageRequestStub("de"));

        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var me = await (await client.GetAsync("/api/users/me"))
            .Content.ReadFromJsonAsync<MeStub>(JsonOptions);
        Assert.NotNull(me);
        Assert.Null(me.LearningLanguage); // unchanged
    }
}
