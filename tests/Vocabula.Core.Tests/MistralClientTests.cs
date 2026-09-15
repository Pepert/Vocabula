using System.Net;
using System.Text;
using Vocabula.Core.AI;
using Vocabula.Core.Models;

namespace Vocabula.Core.Tests;

public sealed class MistralClientTests
{
    [Fact]
    public async Task SendAsync_503FollowedBySuccess_RetriesAndSucceeds()
    {
        var extractionJson = "{\"targetLanguage\":\"Swedish\",\"sourceSummary\":\"test\",\"vocabulary\":[],\"grammarPoints\":[],\"uncertainItems\":[]}";

        var handler = new RecordingHandler(
            ServiceUnavailableResponse(),
            CompletionResponse(extractionJson));
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.mistral.ai/")
        };
        var client = new MistralClient(httpClient, "test-key");

        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };

        var result = await client.SendAsync<NotebookKnowledge>(new { }, jsonOptions, cancellationToken: default);

        Assert.Equal("Swedish", result.TargetLanguage);
        Assert.Equal(2, handler.RequestBodies.Count);
    }

    [Fact]
    public async Task SendAsync_400Response_NotRetried()
    {
        var handler = new RecordingHandler(BadRequestResponse());
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.mistral.ai/")
        };
        var client = new MistralClient(httpClient, "test-key");

        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => 
            client.SendAsync<NotebookKnowledge>(new { }, jsonOptions, cancellationToken: default));

        Assert.Contains("400", exception.Message);
        Assert.Single(handler.RequestBodies);
    }

    [Fact]
    public async Task SendAsync_RetryExhaustion_ReturnsFinalError()
    {
        var handler = new RecordingHandler(
            ServiceUnavailableResponse(),
            ServiceUnavailableResponse(),
            ServiceUnavailableResponse(),
            ServiceUnavailableResponse(),
            ServiceUnavailableResponse());
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.mistral.ai/")
        };
        var client = new MistralClient(httpClient, "test-key");

        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => 
            client.SendAsync<NotebookKnowledge>(new { }, jsonOptions, cancellationToken: default));

        Assert.Contains("503", exception.Message);
        Assert.Equal(5, handler.RequestBodies.Count);
    }

    [Fact]
    public async Task SendAsync_Cancellation_StopsRetries()
    {
        var cts = new CancellationTokenSource();

        var handler = new RecordingHandler(
            ServiceUnavailableResponse(),
            ServiceUnavailableResponse());
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.mistral.ai/")
        };
        var client = new MistralClient(httpClient, "test-key");

        var jsonOptions = new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web);

        var task = client.SendAsync<NotebookKnowledge>(new { }, jsonOptions, "v1/chat/completions", cts.Token);
        await Task.Delay(50);
        cts.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Single(handler.RequestBodies);
    }

    private static HttpResponseMessage ServiceUnavailableResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("Service Unavailable", Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage BadRequestResponse()
    {
        return new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Bad Request", Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage CompletionResponse(string content)
    {
        var responseBody = System.Text.Json.JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content } }
            }
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
        };
    }

    private sealed class RecordingHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);

        public List<string> RequestBodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return _responses.Dequeue();
        }
    }
}
