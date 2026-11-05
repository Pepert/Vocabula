using Vocabula.Core.AI;
using Vocabula.Core.Models;

namespace Vocabula.Core.Tests;

public sealed class NotebookKnowledgeMergerTests
{
    private static NotebookKnowledge Knowledge(
        string summary = "a page",
        string[]? terms = null,
        string[]? grammar = null,
        string[]? uncertain = null)
    {
        return new NotebookKnowledge(
            "Swedish",
            summary,
            (terms ?? []).Select(t => new VocabularyEntry(t, t, $"meaning of {t}", "noun", "seen")).ToList(),
            (grammar ?? []).Select(g => new GrammarPoint(g, $"about {g}", [])).ToList(),
            uncertain ?? []);
    }

    [Fact]
    public void Merge_SingleKnowledge_ReturnsItAsIs()
    {
        var knowledge = Knowledge();

        var merged = NotebookKnowledgeMerger.Merge([knowledge]);

        Assert.Same(knowledge, merged);
    }

    [Fact]
    public void Merge_EmptyInput_Throws()
    {
        Assert.Throws<ArgumentException>(() => NotebookKnowledgeMerger.Merge([]));
    }

    [Fact]
    public void Merge_DeduplicatesVocabularyAndGrammarAcrossPages()
    {
        var page1 = Knowledge(terms: ["hej", "hus"], grammar: ["present tense"]);
        var page2 = Knowledge(terms: ["HEJ", "katt"], grammar: ["Present Tense", "word order"]);

        var merged = NotebookKnowledgeMerger.Merge([page1, page2]);

        var terms = merged.Vocabulary.Select(v => v.Term).ToList();
        var grammar = merged.GrammarPoints.Select(g => g.Name).ToList();

        Assert.Equal(["hej", "hus", "katt"], terms);
        Assert.Equal(["present tense", "word order"], grammar);
    }

    [Fact]
    public void Merge_CombinesSummariesAndUncertainItems()
    {
        var page1 = Knowledge(summary: "page one", uncertain: ["smudged word"]);
        var page2 = Knowledge(summary: "page two", uncertain: ["smudged word", "unreadable note"]);

        var merged = NotebookKnowledgeMerger.Merge([page1, page2]);

        Assert.Equal("page one | page two", merged.SourceSummary);
        Assert.Equal(["smudged word", "unreadable note"], merged.UncertainItems);
    }
}
