using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vocabula.Api.Authorization;
using Vocabula.Api.Users;
using Vocabula.Core.Domain;
using Vocabula.Core.Languages;
using Vocabula.Infrastructure.Persistence;

namespace Vocabula.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private static readonly string[] SupportedLanguages = ["en", "fr"];

    private readonly ICurrentAppUser _currentAppUser;
    private readonly VocabulaDbContext _dbContext;
    private readonly ICognitoUserDeleter _cognitoUserDeleter;

    public UsersController(
        ICurrentAppUser currentAppUser,
        VocabulaDbContext dbContext,
        ICognitoUserDeleter cognitoUserDeleter)
    {
        _currentAppUser = currentAppUser;
        _dbContext = dbContext;
        _cognitoUserDeleter = cognitoUserDeleter;
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserResponse>> GetMeAsync(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);

        return Ok(new CurrentUserResponse(_currentAppUser.UserId, user?.Language, user?.LearningLanguage));
    }

    /// <summary>
    /// Updates the authenticated user's account preferences. Language may
    /// be null, meaning "follow the device language".
    /// </summary>
    [HttpPatch("me")]
    public async Task<ActionResult<CurrentUserResponse>> UpdateMeAsync(
        [FromBody] UpdateCurrentUserRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Language is not null && !SupportedLanguages.Contains(request.Language))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported language",
                Detail = $"Language must be one of: {string.Join(", ", SupportedLanguages)}.",
                Status = 400
            });
        }

        var user = await GetUserAsync(cancellationToken);
        if (user == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "User not found",
                Detail = "The authenticated user has no account record yet.",
                Status = 404
            });
        }

        user.Language = request.Language;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CurrentUserResponse(_currentAppUser.UserId, user.Language, user.LearningLanguage));
    }

    /// <summary>
    /// Switches the language the user is learning. Switching keeps every
    /// stored profile: knowledge, story and chapters of each language stay
    /// separate, and the active one selects what the app shows.
    /// </summary>
    [HttpPatch("me/learning-language")]
    public async Task<ActionResult<CurrentUserResponse>> UpdateLearningLanguageAsync(
        [FromBody] UpdateLearningLanguageRequest request,
        CancellationToken cancellationToken)
    {
        var supportedCodes = LearningLanguages.All.Select(language => language.Code).ToList();
        if (string.IsNullOrEmpty(request.LearningLanguage) || !supportedCodes.Contains(request.LearningLanguage))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported learning language",
                Detail = $"Learning language must be one of: {string.Join(", ", supportedCodes)}.",
                Status = 400
            });
        }

        var user = await GetUserAsync(cancellationToken);
        if (user == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "User not found",
                Detail = "The authenticated user has no account record yet.",
                Status = 404
            });
        }

        user.LearningLanguage = request.LearningLanguage;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new CurrentUserResponse(_currentAppUser.UserId, user.Language, user.LearningLanguage));
    }

    /// <summary>
    /// Deletes the authenticated user's account: the Cognito identity first,
    /// then every database row (profiles, vocabulary, grammar, stories and
    /// chapters cascade from the user). Deletion is idempotent - the
    /// identity being already gone is not an error.
    /// </summary>
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteMeAsync(CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(cancellationToken);
        if (user == null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "User not found",
                Detail = "The authenticated user has no account record yet.",
                Status = 404
            });
        }

        // Deleting the identity first guarantees that a failure halfway
        // leaves "cannot sign in, data intact" (retryable), never the
        // reverse state where data is gone but sign-in still works.
        try
        {
            await _cognitoUserDeleter.DeleteAsync(user.ExternalSubject, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Problem(
                title: "Account deletion failed",
                detail: "The identity could not be deleted; no data has been removed. Try again later.",
                statusCode: 503);
        }

        _dbContext.AppUsers.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<AppUser?> GetUserAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.AppUsers
            .FirstOrDefaultAsync(u => u.Id == _currentAppUser.UserId, cancellationToken);
    }
}

public sealed record CurrentUserResponse(Guid UserId, string? Language, string? LearningLanguage);

public sealed record UpdateCurrentUserRequest(string? Language);

public sealed record UpdateLearningLanguageRequest(string LearningLanguage);
