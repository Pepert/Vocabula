using System.Text;
using Vocabula.Spike;

const string imagePath = "/Users/leo/Development/Projects/Vocabula/input/notebook.jpeg";
const string outputPath = "/Users/leo/Development/Projects/Vocabula/output/result.json";
const string visionModel = "mistral-small-2506";
const string storyModel = "mistral-small-2506";

Console.OutputEncoding = Encoding.UTF8;

if (!File.Exists(imagePath))
{
    Console.Error.WriteLine($"Notebook image not found: {imagePath}");
    Console.Error.WriteLine("Copy a JPG notebook photo to that path, or edit imagePath in Program.cs.");
    return 1;
}

var apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine("MISTRAL_API_KEY is not set.");
    Console.Error.WriteLine("Add it to the Rider run configuration; do not put it in source code.");
    return 1;
}

using var httpClient = new HttpClient
{
    BaseAddress = new Uri("https://api.mistral.ai/"),
    Timeout = TimeSpan.FromMinutes(2)
};
var pipeline = new MistralNotebookPipeline(
    httpClient,
    apiKey,
    visionModel,
    storyModel);

try
{
    Console.WriteLine($"Reading notebook image: {imagePath}");
    var result = await pipeline.RunAsync(imagePath);
    var json = SpikeOutputJson.Serialize(result);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllTextAsync(outputPath, json, Encoding.UTF8);

    Console.WriteLine(json);
    Console.WriteLine($"\nSaved result to: {outputPath}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Spike failed: {exception.Message}");
    return 1;
}
