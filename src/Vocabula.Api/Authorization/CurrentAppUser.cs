using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Vocabula.Core.Domain;
using Vocabula.Infrastructure.Persistence;

namespace Vocabula.Api.Authorization;

public sealed class CurrentAppUser : ICurrentAppUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly VocabulaDbContext _dbContext;
    private Guid? _userId;

    public CurrentAppUser(IHttpContextAccessor httpContextAccessor, VocabulaDbContext dbContext)
    {
        _httpContextAccessor = httpContextAccessor;
        _dbContext = dbContext;
    }

    public Guid UserId
    {
        get
        {
            if (_userId.HasValue)
            {
                return _userId.Value;
            }

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
            {
                throw new InvalidOperationException("HTTP context is not available.");
            }
            
            var user = httpContext.User;
            if (user == null || !user.Identity!.IsAuthenticated)
            {
                throw new InvalidOperationException("User is not authenticated.");
            }

            var identityProvider = user.FindFirstValue("iss");
            var externalSubject = user.FindFirstValue("sub");

            if (string.IsNullOrWhiteSpace(identityProvider) || string.IsNullOrWhiteSpace(externalSubject))
            {
                throw new InvalidOperationException("Missing iss or sub claim.");
            }

            // Try to find existing AppUser by identity
            var existingUser = _dbContext.AppUsers
                .FirstOrDefault(u => u.IdentityProvider == identityProvider && u.ExternalSubject == externalSubject);

            if (existingUser != null)
            {
                _userId = existingUser.Id;
                return _userId.Value;
            }

            // Create new AppUser
            var newUser = new AppUser
            {
                Id = Guid.NewGuid(),
                IdentityProvider = identityProvider,
                ExternalSubject = externalSubject,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.AppUsers.Add(newUser);

            try
            {
                _dbContext.SaveChanges();
                _userId = newUser.Id;
                return _userId.Value;
            }
            catch (DbUpdateException)
            {
                // Race condition: another request may have created the user
                // Re-query to get the existing user
                var createdUser = _dbContext.AppUsers
                    .FirstOrDefault(u => u.IdentityProvider == identityProvider && u.ExternalSubject == externalSubject);

                if (createdUser != null)
                {
                    _userId = createdUser.Id;
                    return _userId.Value;
                }

                throw;
            }
        }
    }
}
