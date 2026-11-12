using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vocabula.Api.Authorization;
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

    public UsersController(ICurrentAppUser currentAppUser, VocabulaDbContext dbContext)
    {
        _currentAppUser = currentAppUser;
        _dbContext = dbContext;
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

    private async Task<AppUser?> GetUserAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.AppUsers
            .FirstOrDefaultAsync(u => u.Id == _currentAppUser.UserId, cancellationToken);
    }
}

public sealed record CurrentUserResponse(Guid UserId, string? Language, string? LearningLanguage);

public sealed record UpdateCurrentUserRequest(string? Language);

public sealed record UpdateLearningLanguageRequest(string LearningLanguage);
