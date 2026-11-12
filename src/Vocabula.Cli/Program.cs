using System.Text;
using System.Text.Json;
using Vocabula.Core;
using Vocabula.Core.AI;
using Vocabula.Core.Languages;
using Vocabula.Core.Models;

const string imagePath = "/Users/leo/Development/Projects/Vocabula/input/notebook.jpeg";
const string outputPath = "/Users/leo/Development/Projects/Vocabula/output/result.json";
const string knowledgeStorePath = "/Users/leo/Development/Projects/Vocabula/knowledge-store.json";
const string visionModel = "mistral-small-2506";
const string storyModel = "mistral-small-2506";
const string targetLanguageCode = "sv";

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

var mistralClient = new MistralClient(httpClient, apiKey);
var extractor = new NotebookKnowledgeExtractor(mistralClient, visionModel);
var generator = new StoryGenerator(mistralClient, storyModel);

try
{
    Console.WriteLine($"Reading notebook image: {imagePath}");

    await using var imageStream = File.OpenRead(imagePath);
    var knowledge = await extractor.ExtractAsync(imageStream, "image/jpeg", LearningLanguages.EnglishNameOf(targetLanguageCode));

    NotebookKnowledge mergedKnowledge = knowledge;
    if (File.Exists(knowledgeStorePath))
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true
        };
        var existingJson = await File.ReadAllTextAsync(knowledgeStorePath);
        var previousKnowledge = JsonSerializer.Deserialize<NotebookKnowledge>(existingJson, options);

        if (previousKnowledge != null)
        {
            mergedKnowledge = MergeWithNewKnowledge(previousKnowledge, knowledge);
        }
    }

    Directory.CreateDirectory(Path.GetDirectoryName(knowledgeStorePath)!);
    var mergedJson = JsonSerializer.Serialize(mergedKnowledge, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(knowledgeStorePath, mergedJson);

    var story = await generator.GenerateAsync(mergedKnowledge, targetLanguageCode);

    var result = new NotebookProcessingResult(mergedKnowledge, story);
    var outputJson = NotebookProcessingResultJson.Serialize(result);

    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    await File.WriteAllTextAsync(outputPath, outputJson, Encoding.UTF8);

    Console.WriteLine(outputJson);
    Console.WriteLine($"\nSaved result to: {outputPath}");
    Console.WriteLine($"Saved merged knowledge to: {knowledgeStorePath}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"CLI failed: {exception.Message}");
    return 1;
}

static NotebookKnowledge MergeWithNewKnowledge(NotebookKnowledge previous, NotebookKnowledge current)
{
    var mergedVocab = current.Vocabulary
        .Concat(previous.Vocabulary.Where(e => !current.Vocabulary.Any(n => n.Term == e.Term)))
        .ToList();

    var mergedGrammar = current.GrammarPoints
        .Concat(previous.GrammarPoints.Where(g => !current.GrammarPoints.Any(n => n.Name == g.Name)))
        .ToList();

    var mergedUncertain = current.UncertainItems
        .Concat(previous.UncertainItems)
        .Distinct()
        .ToList();

    return new NotebookKnowledge(
        TargetLanguage: current.TargetLanguage,
        SourceSummary: $"{previous.SourceSummary}\n\n---\n\n{current.SourceSummary}".Trim(),
        Vocabulary: mergedVocab,
        GrammarPoints: mergedGrammar,
        UncertainItems: mergedUncertain
    );
}
