using Vocabula.Core.Models;

namespace Vocabula.Core.Tests;

public sealed class NotebookProcessingResultJsonTests
{
    [Fact]
    public void Serialize_ProducesReadableCamelCaseJson()
    {
        var result = new NotebookProcessingResult(
            new NotebookKnowledge(
                "Swedish",
                "Greetings",
                [new VocabularyEntry("hej", "hej", "hello", "interjection", "hej")],
                [new GrammarPoint("present tense", "Current actions", ["jag heter"])],
                []),
            new GeneratedStory(
                "Ett möte",
                "Hej!",
                ["hej"],
                ["present tense"],
                []));

        var json = NotebookProcessingResultJson.Serialize(result);

        Assert.Contains("\"knowledge\"", json);
        Assert.Contains("\"targetLanguage\": \"Swedish\"", json);
        Assert.Contains("\"story\": {", json);
        Assert.Contains("\"title\": \"Ett möte\"", json);
        Assert.DoesNotContain("\"Knowledge\"", json);
    }
}
