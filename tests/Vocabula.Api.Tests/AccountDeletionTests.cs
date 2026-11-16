using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vocabula.Core.Domain;
using Vocabula.Infrastructure.Persistence;

namespace Vocabula.Api.Tests;

/// <summary>
/// DELETE /api/users/me - the in-app account deletion path required by App
/// Store review and GDPR. Deletes the Cognito identity first, then the
/// account row, from which Postgres cascades every learning profile,
/// vocabulary item, grammar point, story and chapter (the database-side
/// cascade itself is asserted by the model tests in Infrastructure).
/// The in-memory provider used here has no referential enforcement, so
/// these tests prove the account row and identity go, not the cascade.
/// </summary>
public sealed class AccountDeletionTests
{
    private const string TestSubject = "11111111-1111-1111-1111-111111111111";

    [Fact]
    public async Task DeleteMe_DeletesIdentityAndAccountRow()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.SeedUser();

        var response = await client.DeleteAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // The identity is deleted with the subject from the user's record -
        // exactly what the API knows from the access token.
        Assert.Single(factory.DeletedSubjects, TestSubject);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VocabulaDbContext>();
        Assert.Empty(db.AppUsers.ToList());
    }

    [Fact]
    public async Task DeleteMe_WithLearningData_StillSucceeds()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.SeedUser(learningLanguage: "sv");
        SeedLearningData(factory);

        var response = await client.DeleteAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(factory.DeletedSubjects, TestSubject);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VocabulaDbContext>();
        Assert.Empty(db.AppUsers.ToList());
    }

    [Fact]
    public async Task DeleteMe_WhenIdentityDeletionFails_KeepsAllData()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();
        factory.SeedUser();
        factory.EnsureCognitoDeleterThrows = true;

        var response = await client.DeleteAsync("/api/users/me");

        // The failure must be visible, and nothing may be removed: the
        // identity is always deleted before any data row.
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Empty(factory.DeletedSubjects);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VocabulaDbContext>();
        Assert.Single(db.AppUsers.ToList());
    }

    [Fact]
    public async Task DeleteMe_IsIdempotentWhenIdentityIsAlreadyGone()
    {
        using var factory = new AuthenticatedWebApplicationFactory();
        using var client = factory.CreateClient();

        // No seeded record: pairing (sub, iss) has never signed in. The
        // auto-provisioned empty account is deleted along with a missing
        // identity, which must not error.
        var response = await client.DeleteAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(factory.DeletedSubjects, TestSubject);
    }

    private static void SeedLearningData(AuthenticatedWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VocabulaDbContext>();
        var user = db.AppUsers.Single();
        var profile = new LearningProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            LanguageCode = "sv",
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.LearningProfiles.Add(profile);
        db.VocabularyItems.Add(new VocabularyItem
        {
            Id = Guid.NewGuid(),
            LearningProfileId = profile.Id,
            Term = "hej",
            NormalizedTerm = "hej",
            Translation = "hello",
            FirstSeenAt = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
    }
}
