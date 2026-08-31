using System.Net;
using Microsoft.AspNetCore.Mvc;
using Vocabula.Core.AI;
using Vocabula.Core.Models;

namespace Vocabula.Api.Controllers;

[ApiController]
[Route("api/stories")]
public sealed class StoriesController : ControllerBase
{
    private readonly IStoryGenerator _generator;

    public StoriesController(IStoryGenerator generator)
    {
        _generator = generator;
    }

    [HttpPost("generate")]
    public async Task<ActionResult<GeneratedStory>> GenerateAsync(
        [FromBody] NotebookKnowledge knowledge,
        CancellationToken cancellationToken)
    {
        if (knowledge == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing request body",
                Detail = "Please provide NotebookKnowledge in the request body."
            });
        }

        try
        {
            var story = await _generator.GenerateAsync(knowledge, cancellationToken);
            return Ok(story);
        }
        catch (HttpRequestException ex) when (ex.StatusCode != null && IsTransientError(ex.StatusCode.Value))
        {
            return StatusCode(503, new ProblemDetails
            {
                Title = "Service unavailable",
                Detail = ex.Message,
                Status = 503
            });
        }
        catch (HttpRequestException ex)
        {
            var statusCode = (int)(ex.StatusCode ?? HttpStatusCode.InternalServerError);
            return StatusCode(statusCode, new ProblemDetails
            {
                Title = "Mistral API error",
                Detail = ex.Message,
                Status = statusCode
            });
        }
    }

    private static bool IsTransientError(HttpStatusCode? statusCode)
    {
        if (statusCode == null) return false;
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => true,
            HttpStatusCode.InternalServerError => true,
            HttpStatusCode.BadGateway => true,
            HttpStatusCode.ServiceUnavailable => true,
            HttpStatusCode.GatewayTimeout => true,
            _ => false
        };
    }
}
