using Microsoft.AspNetCore.Http;

namespace Vocabula.Api;

/// <summary>
/// Upload of one or more notebook page photos (multipart form field "Images").
/// </summary>
public sealed class ImageUploadRequest
{
    /// <summary>Between 1 and 10 images.</summary>
    public IReadOnlyList<IFormFile> Images { get; set; } = [];
}
