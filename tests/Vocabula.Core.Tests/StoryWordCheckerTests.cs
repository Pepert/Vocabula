using Vocabula.Core.AI;

namespace Vocabula.Core.Tests;

public sealed class StoryWordCheckerTests
{
    [Fact]
    public void FindNewWords_IgnoresCasePunctuationDuplicatesAndFunctionWords()
    {
        var newWords = StoryWordChecker.FindNewWords(
            "— HEJ! hej, jag är TRÖTT... den katten! KATTEN sover.",
            ["hej"]);

        // "jag", "är" and "den" are function words; "hej" is allowed and
        // repeated; only the content words outside the list are reported.
        Assert.Equal(["trött", "katten", "sover"], newWords);
    }

    [Fact]
    public void FindNewWords_AllowsEveryWordOfMultiWordTerms()
    {
        var newWords = StoryWordChecker.FindNewWords(
            "Jag är trött idag.",
            ["Jag är trött"]);

        Assert.Equal(["idag"], newWords);
    }

    [Fact]
    public void FindNewWords_EmptyStory_Throws()
    {
        Assert.Throws<ArgumentException>(() => StoryWordChecker.FindNewWords("   ", ["hej"]));
        Assert.Throws<ArgumentNullException>(() => StoryWordChecker.FindNewWords("hej", null!));
    }

    [Fact]
    public void FindNewWords_HandlesSwedishCharacters()
    {
        var newWords = StoryWordChecker.FindNewWords(
            "ÅÄÖ! Fikar du? Glad midsommar.",
            ["fika", "glad"]);

        Assert.Equal(["åäö", "fikar", "midsommar"], newWords);
    }
}
