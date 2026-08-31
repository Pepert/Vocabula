using Vocabula.Spike;

namespace Vocabula.Spike.Tests;

public sealed class SpikeOutputJsonTests
{
    [Fact]
    public void Serialize_ProducesReadableCamelCaseJson()
    {
        var output = new SpikeOutput(
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

        var json = SpikeOutputJson.Serialize(output);

        Assert.Contains("\"knowledge\"", json);
        Assert.Contains("\"targetLanguage\": \"Swedish\"", json);
        Assert.Contains("\"story\": {", json);
        Assert.Contains("\"title\": \"Ett möte\"", json);
        Assert.DoesNotContain("\"Knowledge\"", json);
    }
}
