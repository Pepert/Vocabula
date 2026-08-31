using System.Net;
using Microsoft.AspNetCore.Mvc;
using Vocabula.Core.AI;
using Vocabula.Core.Models;

namespace Vocabula.Api.Controllers;

[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController : ControllerBase
{
    private readonly INotebookKnowledgeExtractor _extractor;

    public KnowledgeController(INotebookKnowledgeExtractor extractor)
    {
        _extractor = extractor;
    }

    [HttpPost("extract")]
    [RequestSizeLimit(10_000_000)] // 10 MB
    public async Task<ActionResult<NotebookKnowledge>> ExtractAsync(
        [FromForm] ImageUploadRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Image == null || request.Image.Length == 0)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing or empty file",
                Detail = "Please upload a non-empty notebook image."
            });
        }

        if (!IsSupportedMediaType(request.Image.ContentType))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Unsupported media type",
                Detail = $"Unsupported image type: {request.Image.ContentType}. Use image/jpeg, image/png, or image/webp."
            });
        }

        try
        {
            await using var stream = request.Image.OpenReadStream();
            var knowledge = await _extractor.ExtractAsync(stream, request.Image.ContentType, cancellationToken);
            return Ok(knowledge);
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

    private static bool IsSupportedMediaType(string mediaType)
    {
        return mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("image/webp", StringComparison.OrdinalIgnoreCase);
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
