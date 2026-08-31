using Microsoft.AspNetCore.Mvc;

namespace Vocabula.Api.Controllers;

public sealed class ImageUploadRequest
{
    [FromForm]
    public IFormFile Image { get; set; } = null!;
}
