namespace Vocabula.Spike;

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
}
