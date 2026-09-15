using System.Text.Json;

namespace Vocabula.Core.AI;

public interface IMistralClient
{
    Task<T> SendAsync<T>(
        object request,
        JsonSerializerOptions jsonOptions,
        string endpointPath = "v1/chat/completions",
        CancellationToken cancellationToken = default);
}
