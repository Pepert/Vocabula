using System.Text;
using Vocabula.Core;

namespace Vocabula.Core.Tests;

public sealed class LocalImageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"vocabula-{Guid.NewGuid():N}");

    [Fact]
    public void ToDataUri_EncodesLocalJpeg()
    {
        Directory.CreateDirectory(_directory);
        var imagePath = Path.Combine(_directory, "notes.jpg");
        File.WriteAllBytes(imagePath, Encoding.UTF8.GetBytes("notebook-image"));

        var result = LocalImage.ToDataUri(imagePath);

        Assert.Equal("data:image/jpeg;base64,bm90ZWJvb2staW1hZ2U=", result);
    }

    [Fact]
    public void ToDataUri_RejectsUnsupportedFileType()
    {
        Directory.CreateDirectory(_directory);
        var imagePath = Path.Combine(_directory, "notes.txt");
        File.WriteAllText(imagePath, "not an image");

        var exception = Assert.Throws<NotSupportedException>(() => LocalImage.ToDataUri(imagePath));

        Assert.Contains(".txt", exception.Message);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
