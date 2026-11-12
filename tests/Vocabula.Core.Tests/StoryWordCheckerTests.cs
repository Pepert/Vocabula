using Vocabula.Core.AI;

namespace Vocabula.Core.Tests;

public sealed class StoryWordCheckerTests
{
    [Fact]
    public void FindNewWords_IgnoresCasePunctuationDuplicatesAndFunctionWords()
    {
        var newWords = StoryWordChecker.FindNewWords(
            "— HEJ! hej, jag är TRÖTT... den katten! KATTEN sover.",
            ["hej"],
            "sv");

        // "jag", "är" and "den" are function words; "hej" is allowed and
        // repeated; only the content words outside the list are reported.
        Assert.Equal(["trött", "katten", "sover"], newWords);
    }

    [Fact]
    public void FindNewWords_AllowsEveryWordOfMultiWordTerms()
    {
        var newWords = StoryWordChecker.FindNewWords(
            "Jag är trött idag.",
            ["Jag är trött"],
            "sv");

        Assert.Equal(["idag"], newWords);
    }

    [Fact]
    public void FindNewWords_EmptyStory_Throws()
    {
        Assert.Throws<ArgumentException>(() => StoryWordChecker.FindNewWords("   ", ["hej"], "sv"));
        Assert.Throws<ArgumentNullException>(() => StoryWordChecker.FindNewWords("hej", null!, "sv"));
        Assert.Throws<ArgumentException>(() => StoryWordChecker.FindNewWords("hej", ["hej"], ""));
    }

    [Fact]
    public void FindNewWords_HandlesSwedishCharacters()
    {
        var newWords = StoryWordChecker.FindNewWords(
            "ÅÄÖ! Fikar du? Glad midsommar.",
            ["fika", "glad"],
            "sv");

        Assert.Equal(["åäö", "fikar", "midsommar"], newWords);
    }

    [Fact]
    public void FunctionWordsArePerLanguage()
    {
        // The Swedish articles do not silently excuse the same words in
        // an English or French chapter, and vice versa.
        Assert.True(StoryWordChecker.IsFunctionWord("och", "sv"));
        Assert.False(StoryWordChecker.IsFunctionWord("och", "en"));
        Assert.True(StoryWordChecker.IsFunctionWord("the", "en"));
        Assert.True(StoryWordChecker.IsFunctionWord("est", "fr"));
        Assert.True(StoryWordChecker.IsFunctionWord("pero", "es"));

        var englishNewWords = StoryWordChecker.FindNewWords("The cat and the dog.", ["cat"], "en");
        Assert.Equal(["dog"], englishNewWords);
    }

    [Fact]
    public void UnknownLanguageHasNoFunctionWords()
    {
        // A language without a curated list reports every unknown word,
        // which errs on the side of the new-word cap.
        var newWords = StoryWordChecker.FindNewWords("ja tytto tenemme", [], "fi");
        Assert.Equal(["ja", "tytto", "tenemme"], newWords);
    }
}
