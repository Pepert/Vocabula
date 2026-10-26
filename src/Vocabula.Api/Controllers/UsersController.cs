using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vocabula.Api.Authorization;

namespace Vocabula.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly ICurrentAppUser _currentAppUser;

    public UsersController(ICurrentAppUser currentAppUser)
    {
        _currentAppUser = currentAppUser;
    }

    [HttpGet("me")]
    public ActionResult<CurrentUserResponse> GetMe()
    {
        return Ok(new CurrentUserResponse(_currentAppUser.UserId));
    }
}

public sealed record CurrentUserResponse(Guid UserId);
