namespace Vocabula.Core.Models;

public sealed record VocabularyEntry(
    string Term,
    string BaseForm,
    string Meaning,
    string PartOfSpeech,
    string Evidence);
