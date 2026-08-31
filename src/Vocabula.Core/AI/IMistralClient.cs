using System.Text.Json;

namespace Vocabula.Core.AI;

public interface IMistralClient
{
    Task<T> SendAsync<T>(
        object request,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken = default);
}
