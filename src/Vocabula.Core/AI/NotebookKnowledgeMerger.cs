using Vocabula.Core.Models;

namespace Vocabula.Core.AI;

/// <summary>
/// Merges the knowledge extracted from several notebook pages into one
/// notebook-knowledge object: vocabulary and grammar are deduplicated, the
/// first extraction of an item wins, and uncertain items are combined.
/// </summary>
public static class NotebookKnowledgeMerger
{
    public static NotebookKnowledge Merge(IReadOnlyList<NotebookKnowledge> knowledges)
    {
        if (knowledges.Count == 0)
        {
            throw new ArgumentException("Nothing to merge: at least one knowledge object is required.", nameof(knowledges));
        }

        if (knowledges.Count == 1)
        {
            return knowledges[0];
        }

        var vocabulary = new List<VocabularyEntry>();
        var seenVocabulary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var grammar = new List<GrammarPoint>();
        var seenGrammar = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var uncertain = new List<string>();
        var seenUncertain = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var summaries = new List<string>();

        foreach (var knowledge in knowledges)
        {
            summaries.Add(knowledge.SourceSummary);

            foreach (var entry in knowledge.Vocabulary)
            {
                if (seenVocabulary.Add(entry.BaseForm))
                {
                    vocabulary.Add(entry);
                }
            }

            foreach (var point in knowledge.GrammarPoints)
            {
                if (seenGrammar.Add(point.Name))
                {
                    grammar.Add(point);
                }
            }

            foreach (var item in knowledge.UncertainItems)
            {
                if (seenUncertain.Add(item))
                {
                    uncertain.Add(item);
                }
            }
        }

        return new NotebookKnowledge(
            knowledges[0].TargetLanguage,
            string.Join(" | ", summaries.Where(s => !string.IsNullOrWhiteSpace(s))),
            vocabulary,
            grammar,
            uncertain);
    }
}
