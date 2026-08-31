using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Vocabula.Core.AI;

public sealed class MistralClient : IMistralClient
{
    private readonly HttpClient _httpClient;

    public MistralClient(HttpClient httpClient, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentNullException.ThrowIfNull(httpClient);

        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<T> SendAsync<T>(
        object request,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 5;
        var random = new Random();
        HttpResponseMessage? response = null;
        string responseBody = string.Empty;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            response?.Dispose();
            response = null;

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                response = await _httpClient.PostAsJsonAsync(
                    "v1/chat/completions",
                    request,
                    jsonOptions,
                    cancellationToken);
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    break;
                }

                if (!IsTransientError(response.StatusCode))
                {
                    break;
                }

                if (attempt < maxAttempts - 1)
                {
                    await DelayWithBackoffAsync(attempt, response, random, cancellationToken);
                }
            }
            catch
            {
                if (attempt < maxAttempts - 1)
                {
                    await DelayWithBackoffAsync(attempt, response, random, cancellationToken);
                }
                else
                {
                    throw;
                }
            }
        }

        if (response == null)
        {
            throw new InvalidOperationException("No response received from Mistral.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Mistral returned {(int)response.StatusCode} ({response.ReasonPhrase}): {responseBody}");
        }

        var completion = JsonSerializer.Deserialize<ChatCompletion>(responseBody, jsonOptions)
                         ?? throw new InvalidOperationException("Mistral returned an empty response.");
        var content = completion.Choices.FirstOrDefault()?.Message.Content
                      ?? throw new InvalidOperationException("Mistral returned no completion content.");

        return JsonSerializer.Deserialize<T>(content, jsonOptions)
               ?? throw new InvalidOperationException("Mistral returned empty structured JSON.");

        static bool IsTransientError(HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                HttpStatusCode.TooManyRequests      => true,
                HttpStatusCode.InternalServerError   => true,
                HttpStatusCode.BadGateway            => true,
                HttpStatusCode.ServiceUnavailable    => true,
                HttpStatusCode.GatewayTimeout        => true,
                _                                   => false
            };
        }

        async Task DelayWithBackoffAsync(
            int attempt,
            HttpResponseMessage? lastResponse,
            Random random,
            CancellationToken ct)
        {
            var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
            var jitter = TimeSpan.FromMilliseconds(random.Next(0, 500));
            var delay = baseDelay + jitter;

            var retryAfter = lastResponse?.Headers?.RetryAfter?.Delta;
            if (retryAfter != null)
            {
                delay = retryAfter.Value;
            }

            await Task.Delay(delay, ct);
        }
    }

    private sealed record ChatCompletion(IReadOnlyList<Choice> Choices);
    private sealed record Choice(Message Message);
    private sealed record Message(string Content);
}
