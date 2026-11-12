using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Vocabula.Core.AI;
using Vocabula.Core.Models;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Vocabula.Infrastructure.Persistence;

namespace Vocabula.Api.Tests;

/// <summary>
/// Factory that runs the real API with:
/// - the TestAuthHandler scheme so every request is authenticated
/// - an in-memory EF Core database instead of PostgreSQL
/// - fake AI services instead of Mistral calls
/// </summary>
public sealed class AuthenticatedWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// When true, any reach of the (fake) extractor throws - used by tests
    /// that must prove code is NOT reached.
    /// </summary>
    public bool EnsureExtractorThrows { get; set; }

    /// <summary>
    /// Inserts the account row matching the test principal, optionally with
    /// a UI language and/or learning language preference. Call after
    /// CreateClient, so the in-memory database exists.
    /// </summary>
    public void SeedUser(string? language = null, string? learningLanguage = null)
    {
        var user = new Vocabula.Core.Domain.AppUser
        {
            Id = Guid.NewGuid(),
            IdentityProvider = "https://cognito-idp.test.amazonaws.com/test",
            ExternalSubject = "11111111-1111-1111-1111-111111111111",
            Language = language,
            LearningLanguage = learningLanguage,
            CreatedAt = DateTimeOffset.UtcNow
        };

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VocabulaDbContext>();
        db.AppUsers.Add(user);
        db.SaveChanges();
    }

    public NotebookKnowledge ExtractorKnowledge { get; set; } = new(
        "Swedish",
        "a notebook page",
        [new VocabularyEntry("hej", "hej", "hello", "interjection", "seen")],
        [new GrammarPoint("present tense", "basic present", [])],
        []);

    public GeneratedStory StoryResult { get; set; } = new(
        "Hej",
        "Hej! sade hon och log.",
        ["hej"],
        ["present tense"],
        [],
        "Hon säger hej.",
        [new NewWordDefinition("sade", "said")],
        [new WordGloss("hej", "hello (word-level gloss)")]);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureHostConfiguration(config =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Authentication:Cognito:Authority"] = "https://cognito-idp.test.amazonaws.com/test",
                ["Authentication:Cognito:AppClientId"] = "test-client-id",
                ["MISTRAL_API_KEY"] = "test-key",
                ["ConnectionStrings:VocabulaDb"] = "Host=localhost;Database=test",
                ["ASPNETCORE_URLS"] = "http://127.0.0.1:0"
            });

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VocabulaDb"] = "placeholder"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Swap PostgreSQL for a fresh in-memory database per factory. EF Core
            // registers the Npgsql options both as DbContextOptions and as an
            // IDbContextOptionsConfiguration; both must go, or the context ends
            // up configured for two providers at once.
            services.RemoveAll(typeof(DbContextOptions<VocabulaDbContext>));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<VocabulaDbContext>));
            var databaseName = $"vocabula-test-{Guid.NewGuid()}";
            services.AddDbContext<VocabulaDbContext>(options => options.UseInMemoryDatabase(databaseName));

            // Make every request authenticate with a fixed principal.
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<TestAuthHandlerOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            // Fake AI services.
            services.Replace(ServiceDescriptor.Singleton<INotebookKnowledgeExtractor>(
                new FakeExtractor(() => ExtractorKnowledge, () => EnsureExtractorThrows)));
            services.Replace(ServiceDescriptor.Singleton<IStoryGenerator>(
                new FakeStoryGenerator(() => StoryResult)));
        });

        return base.CreateHost(builder);
    }

    private sealed class FakeExtractor(Func<NotebookKnowledge> knowledge, Func<bool> throwIfReached) : INotebookKnowledgeExtractor
    {
        public Task<NotebookKnowledge> ExtractAsync(
            Stream image,
            string mediaType,
            string? expectedLanguageName = null,
            string? definitionLanguageName = null,
            CancellationToken cancellationToken = default)
        {
            if (throwIfReached())
            {
                throw new InvalidOperationException("The extractor was reached, but the test required it not to be.");
            }

            image.ReadByte(); // consume something like the real client would
            return Task.FromResult(knowledge());
        }
    }

    private sealed class FakeStoryGenerator(Func<GeneratedStory> story) : IStoryGenerator
    {
        public Task<GeneratedStory> GenerateAsync(
            NotebookKnowledge knowledge,
            string targetLanguageCode,
            string? previousStorySummary = null,
            IReadOnlyList<string>? acquiredVocabulary = null,
            IReadOnlyList<string>? acquiredGrammar = null,
            string? definitionLanguageName = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(story());
        }
    }

    public static StringContent KnowledgeJson(NotebookKnowledge knowledge) =>
        new(
            JsonSerializer.Serialize(knowledge, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            Encoding.UTF8,
            "application/json");

    public static MultipartFormDataContent NotebookForm(int imageCount, string fileName = "page.jpg")
    {
        var form = new MultipartFormDataContent();
        for (var i = 0; i < imageCount; i++)
        {
            var content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0]);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            form.Add(content, "Images", fileName);
        }

        return form;
    }
}
