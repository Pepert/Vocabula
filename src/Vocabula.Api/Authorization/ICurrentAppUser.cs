namespace Vocabula.Api.Authorization;

public interface ICurrentAppUser
{
    Guid UserId { get; }
}
