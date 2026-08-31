namespace Vocabula.Core;

public static class LocalImage
{
    private static readonly IReadOnlyDictionary<string, string> MimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".png"] = "image/png",
            [".webp"] = "image/webp"
        };

    public static string ToDataUri(string imagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imagePath);

        var extension = Path.GetExtension(imagePath);
        if (!MimeTypes.TryGetValue(extension, out var mimeType))
        {
            throw new NotSupportedException(
                $"Unsupported notebook image type '{extension}'. Use JPG, PNG, or WebP.");
        }

        var bytes = File.ReadAllBytes(imagePath);
        return $"data:{mimeType};base64,{Convert.ToBase64String(bytes)}";
    }

    public static string ToDataUri(Stream imageStream, string mediaType)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(mediaType);

        if (!IsSupportedMediaType(mediaType))
        {
            throw new NotSupportedException(
                $"Unsupported notebook image media type '{mediaType}'. Use image/jpeg, image/png, or image/webp.");
        }

        using var memoryStream = new MemoryStream();
        imageStream.CopyTo(memoryStream);
        var bytes = memoryStream.ToArray();
        return $"data:{mediaType};base64,{Convert.ToBase64String(bytes)}";
    }

    private static bool IsSupportedMediaType(string mediaType)
    {
        return mediaType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("image/png", StringComparison.OrdinalIgnoreCase) ||
               mediaType.Equals("image/webp", StringComparison.OrdinalIgnoreCase);
    }
}
