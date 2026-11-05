using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Vocabula.Api.Tests;

/// <summary>
/// Fake authentication scheme for integration tests: authenticates every
/// request with a fixed iss/sub principal, matching the claims the API's
/// CurrentAppUser resolves the user from.
/// </summary>
public sealed class TestAuthHandlerOptions : AuthenticationSchemeOptions
{
    public string Issuer { get; set; } = "https://cognito-idp.test.amazonaws.com/test";
    public string Subject { get; set; } = "11111111-1111-1111-1111-111111111111";
}

public sealed class TestAuthHandler(
    IOptionsMonitor<TestAuthHandlerOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<TestAuthHandlerOptions>(options, logger, encoder)
{
    public const string SchemeName = "TestScheme";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("iss", Options.Issuer),
            new Claim("sub", Options.Subject)
        ], Scheme.Name);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Task.CompletedTask;
    }
}
