using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Vocabula.Api.Authorization;
using Vocabula.Infrastructure.Persistence;
using Xunit;

namespace Vocabula.Api.Tests;

public sealed class CurrentAppUserTests : IDisposable
{
    private readonly VocabulaDbContext _dbContext;

    public CurrentAppUserTests()
    {
        var options = new DbContextOptionsBuilder<VocabulaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new VocabulaDbContext(options);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void UserId_WithUnauthenticatedUser_ThrowsInvalidOperationException()
    {
        // Given
        var httpContext = new DefaultHttpContext();
        // User is not authenticated by default
        
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.SetupGet(x => x.HttpContext).Returns(httpContext);

        var currentAppUser = new CurrentAppUser(httpContextAccessorMock.Object, _dbContext);

        // When/Then
        var exception = Assert.Throws<InvalidOperationException>(() => currentAppUser.UserId);
        Assert.Contains("not authenticated", exception.Message);
    }

    [Fact]
    public void UserId_WithMissingIssClaim_ThrowsInvalidOperationException()
    {
        // Given
        var claims = new[] { new Claim("sub", "user-123") };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext();
        httpContext.User = principal;
        
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.SetupGet(x => x.HttpContext).Returns(httpContext);

        var currentAppUser = new CurrentAppUser(httpContextAccessorMock.Object, _dbContext);

        // When/Then
        var exception = Assert.Throws<InvalidOperationException>(() => currentAppUser.UserId);
        Assert.Contains("Missing iss or sub claim", exception.Message);
    }

    [Fact]
    public void UserId_WithMissingSubClaim_ThrowsInvalidOperationException()
    {
        // Given
        var claims = new[] { new Claim("iss", "https://cognito-idp.test.amazonaws.com/test") };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext();
        httpContext.User = principal;
        
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.SetupGet(x => x.HttpContext).Returns(httpContext);

        var currentAppUser = new CurrentAppUser(httpContextAccessorMock.Object, _dbContext);

        // When/Then
        var exception = Assert.Throws<InvalidOperationException>(() => currentAppUser.UserId);
        Assert.Contains("Missing iss or sub claim", exception.Message);
    }

    [Fact]
    public void UserId_WithValidClaimsAndExistingUser_ReturnsExistingUserId()
    {
        // Given
        var identityProvider = "https://cognito-idp.test.amazonaws.com/test";
        var externalSubject = "user-123";
        
        var existingUser = new Core.Domain.AppUser
        {
            Id = Guid.NewGuid(),
            IdentityProvider = identityProvider,
            ExternalSubject = externalSubject
        };
        _dbContext.AppUsers.Add(existingUser);
        _dbContext.SaveChanges();
        
        var claims = new[] 
        {
            new Claim("iss", identityProvider),
            new Claim("sub", externalSubject)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext();
        httpContext.User = principal;
        
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.SetupGet(x => x.HttpContext).Returns(httpContext);

        var currentAppUser = new CurrentAppUser(httpContextAccessorMock.Object, _dbContext);

        // When
        var userId = currentAppUser.UserId;

        // Then
        Assert.Equal(existingUser.Id, userId);
    }

    [Fact]
    public void UserId_WithValidClaimsAndNoExistingUser_CreatesNewUser()
    {
        // Given
        var identityProvider = "https://cognito-idp.test.amazonaws.com/test";
        var externalSubject = "user-new";
        
        var claims = new[] 
        {
            new Claim("iss", identityProvider),
            new Claim("sub", externalSubject)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        var httpContext = new DefaultHttpContext();
        httpContext.User = principal;
        
        var httpContextAccessorMock = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock.SetupGet(x => x.HttpContext).Returns(httpContext);

        var currentAppUser = new CurrentAppUser(httpContextAccessorMock.Object, _dbContext);

        // When
        var userId = currentAppUser.UserId;

        // Then
        Assert.NotEqual(Guid.Empty, userId);
        
        // Verify the user was created with correct identity data
        var createdUser = _dbContext.AppUsers.FirstOrDefault(u => u.IdentityProvider == identityProvider && u.ExternalSubject == externalSubject);
        Assert.NotNull(createdUser);
        Assert.Equal(userId, createdUser.Id);
    }

    [Fact]
    public void UserId_WithDifferentExternalSubjects_CreatesDifferentUsers()
    {
        // Given
        var identityProvider = "https://cognito-idp.test.amazonaws.com/test";
        var subject1 = "user-1";
        var subject2 = "user-2";
        
        // First user
        var claims1 = new[] 
        {
            new Claim("iss", identityProvider),
            new Claim("sub", subject1)
        };
        var identity1 = new ClaimsIdentity(claims1, "Test");
        var principal1 = new ClaimsPrincipal(identity1);
        
        var httpContext1 = new DefaultHttpContext();
        httpContext1.User = principal1;
        
        var httpContextAccessorMock1 = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock1.SetupGet(x => x.HttpContext).Returns(httpContext1);

        var currentAppUser1 = new CurrentAppUser(httpContextAccessorMock1.Object, _dbContext);
        var userId1 = currentAppUser1.UserId;
        
        // Second user
        var claims2 = new[] 
        {
            new Claim("iss", identityProvider),
            new Claim("sub", subject2)
        };
        var identity2 = new ClaimsIdentity(claims2, "Test");
        var principal2 = new ClaimsPrincipal(identity2);
        
        var httpContext2 = new DefaultHttpContext();
        httpContext2.User = principal2;
        
        var httpContextAccessorMock2 = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock2.SetupGet(x => x.HttpContext).Returns(httpContext2);

        var currentAppUser2 = new CurrentAppUser(httpContextAccessorMock2.Object, _dbContext);
        var userId2 = currentAppUser2.UserId;

        // Then
        Assert.NotEqual(userId1, userId2);
        Assert.NotEqual(Guid.Empty, userId1);
        Assert.NotEqual(Guid.Empty, userId2);
    }

    [Fact]
    public void UserId_TwoRequestsWithSameIdentity_ReturnsSameUserId()
    {
        // Given
        var identityProvider = "https://cognito-idp.test.amazonaws.com/test";
        var externalSubject = "user-same";
        
        var claims = new[] 
        {
            new Claim("iss", identityProvider),
            new Claim("sub", externalSubject)
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        
        // First request
        var httpContext1 = new DefaultHttpContext();
        httpContext1.User = principal;
        
        var httpContextAccessorMock1 = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock1.SetupGet(x => x.HttpContext).Returns(httpContext1);

        var currentAppUser1 = new CurrentAppUser(httpContextAccessorMock1.Object, _dbContext);
        var userId1 = currentAppUser1.UserId;
        
        // Second request with same identity
        var httpContext2 = new DefaultHttpContext();
        httpContext2.User = principal;
        
        var httpContextAccessorMock2 = new Mock<IHttpContextAccessor>();
        httpContextAccessorMock2.SetupGet(x => x.HttpContext).Returns(httpContext2);

        var currentAppUser2 = new CurrentAppUser(httpContextAccessorMock2.Object, _dbContext);
        var userId2 = currentAppUser2.UserId;

        // Then
        Assert.Equal(userId1, userId2);
        
        // Verify only one user was created in the database
        var userCount = _dbContext.AppUsers.Count(u => u.IdentityProvider == identityProvider && u.ExternalSubject == externalSubject);
        Assert.Equal(1, userCount);
    }
}
