using Amazon.CognitoIdentityProvider;
using Amazon.CognitoIdentityProvider.Model;

namespace Vocabula.Api.Users;

/// <summary>
/// Deletes a user's identity record from the Cognito user pool, as part of
/// in-app account deletion (required by App Store review and GDPR).
/// </summary>
public interface ICognitoUserDeleter
{
    /// <summary>
    /// Deletes the Cognito user matching the given subject. Returns false
    /// when no such user exists, so a retry after a partially completed
    /// deletion stays idempotent.
    /// </summary>
    Task<bool> DeleteAsync(string subject, CancellationToken cancellationToken);
}

/// <summary>
/// The API only ever sees the Cognito access token, whose `sub` identifies
/// the user - but Cognito usernames differ per federation type: native users
/// use their email while federated users get provider-prefixed usernames
/// such as "Google_1234". The username is resolved from the subject right
/// before deletion.
/// </summary>
public sealed class CognitoUserDeleter : ICognitoUserDeleter
{
    private readonly IAmazonCognitoIdentityProvider _cognito;
    private readonly string _userPoolId;

    public CognitoUserDeleter(IAmazonCognitoIdentityProvider cognito, string userPoolId)
    {
        if (string.IsNullOrWhiteSpace(userPoolId))
        {
            throw new InvalidOperationException(
                "Cognito user pool id configuration is missing. " +
                "Set Authentication:Cognito:UserPoolId.");
        }

        _cognito = cognito;
        _userPoolId = userPoolId;
    }

    public async Task<bool> DeleteAsync(string subject, CancellationToken cancellationToken)
    {
        var listed = await _cognito.ListUsersAsync(new ListUsersRequest
        {
            UserPoolId = _userPoolId,
            Filter = $"sub = \"{subject}\""
        }, cancellationToken);

        var username = listed.Users.SingleOrDefault()?.Username;
        if (username == null)
        {
            // The identity is already gone (e.g. a previous deletion failed
            // after this step); let the caller clean up the remaining data.
            return false;
        }

        await _cognito.AdminDeleteUserAsync(new AdminDeleteUserRequest
        {
            UserPoolId = _userPoolId,
            Username = username
        }, cancellationToken);

        return true;
    }
}
