namespace Vocabula.Core.Languages;

/// <summary>
/// The languages a learner can study with Vocabula. The code is stored on
/// the account and on learning profiles; the English name is what the AI
/// prompts use ("write the chapter in Swedish"). Extend <see cref="All"/>
/// to add a language - the API validation, the prompts and the function
/// word lists all key off these codes.
/// </summary>
public static class LearningLanguages
{
    public sealed record SupportedLanguage(string Code, string EnglishName);

    public static readonly IReadOnlyList<SupportedLanguage> All =
    [
        new("en", "English"),
        new("fr", "French"),
        new("es", "Spanish"),
        new("sv", "Swedish"),
    ];

    /// <summary>English name for a language code, or null if unsupported.</summary>
    public static string? EnglishNameOf(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        return All.FirstOrDefault(language => language.Code == code)?.EnglishName;
    }
}
