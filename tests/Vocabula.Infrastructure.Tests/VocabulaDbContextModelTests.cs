using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Vocabula.Core.Domain;
using Vocabula.Infrastructure.Persistence;

namespace Vocabula.Infrastructure.Tests;

public sealed class VocabulaDbContextModelTests
{
    private readonly DbContextOptions<VocabulaDbContext> _options;

    public VocabulaDbContextModelTests()
    {
        _options = new DbContextOptionsBuilder<VocabulaDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;
    }

    [Fact]
    public void AllEntityTypes_AreInModel()
    {
        using var context = new VocabulaDbContext(_options);
        var model = context.Model;
        var entityTypes = model.GetEntityTypes().Select(e => e.ClrType).ToList();

        Assert.Contains(typeof(AppUser), entityTypes);
        Assert.Contains(typeof(LearningProfile), entityTypes);
        Assert.Contains(typeof(LearningSource), entityTypes);
        Assert.Contains(typeof(VocabularyItem), entityTypes);
        Assert.Contains(typeof(GrammarPoint), entityTypes);
        Assert.Contains(typeof(Story), entityTypes);
        Assert.Contains(typeof(StoryChapter), entityTypes);
    }

    [Fact]
    public void TableNames_AreSnakeCase()
    {
        using var context = new VocabulaDbContext(_options);
        var model = context.Model;

        Assert.Equal("app_users", model.FindEntityType(typeof(AppUser))!.GetTableName());
        Assert.Equal("learning_profiles", model.FindEntityType(typeof(LearningProfile))!.GetTableName());
        Assert.Equal("learning_sources", model.FindEntityType(typeof(LearningSource))!.GetTableName());
        Assert.Equal("vocabulary_items", model.FindEntityType(typeof(VocabularyItem))!.GetTableName());
        Assert.Equal("grammar_points", model.FindEntityType(typeof(GrammarPoint))!.GetTableName());
        Assert.Equal("stories", model.FindEntityType(typeof(Story))!.GetTableName());
        Assert.Equal("story_chapters", model.FindEntityType(typeof(StoryChapter))!.GetTableName());
    }

    [Fact]
    public void AppUser_HasCorrectPrimaryKey()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(AppUser))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        Assert.Single(primaryKey.Properties);
        Assert.Equal("id", primaryKey.Properties[0].GetColumnName());
        Assert.Equal(typeof(Guid), primaryKey.Properties[0].ClrType);
    }

    [Fact]
    public void AppUser_HasNullableEmailWithMaxLength()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(AppUser))!;

        var emailProperty = entityType.FindProperty("Email")!;
        Assert.True(emailProperty.IsNullable);
        Assert.Equal(320, emailProperty.GetMaxLength());

        var normalizedEmailProperty = entityType.FindProperty("NormalizedEmail")!;
        Assert.True(normalizedEmailProperty.IsNullable);
        Assert.Equal(320, normalizedEmailProperty.GetMaxLength());
    }

    [Fact]
    public void AppUser_HasNullableIdentityProviderAndExternalSubject()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(AppUser))!;

        var identityProviderProperty = entityType.FindProperty("IdentityProvider")!;
        Assert.True(identityProviderProperty.IsNullable);
        Assert.Equal(100, identityProviderProperty.GetMaxLength());
        Assert.Equal("identity_provider", identityProviderProperty.GetColumnName());

        var externalSubjectProperty = entityType.FindProperty("ExternalSubject")!;
        Assert.True(externalSubjectProperty.IsNullable);
        Assert.Equal(255, externalSubjectProperty.GetMaxLength());
        Assert.Equal("external_subject", externalSubjectProperty.GetColumnName());
    }

    [Fact]
    public void AppUser_HasCompositeUniqueIndexOnIdentityProviderAndExternalSubject()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(AppUser))!;
        var index = entityType.GetIndexes()
            .Single(i => i.GetDatabaseName() == "ix_app_users_identity_provider_external_subject");

        Assert.True(index.IsUnique);
        Assert.Equal(2, index.Properties.Count);
        Assert.Equal("identity_provider", index.Properties[0].GetColumnName());
        Assert.Equal("external_subject", index.Properties[1].GetColumnName());
    }

    [Fact]
    public void AppUser_HasCreatedAtAsDateTimeOffset()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(AppUser))!;
        var createdAtProperty = entityType.FindProperty("CreatedAt")!;

        Assert.False(createdAtProperty.IsNullable);
        Assert.Equal(typeof(DateTimeOffset), createdAtProperty.ClrType);
    }

    [Fact]
    public void AppUser_HasUniqueNormalizedEmailIndex()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(AppUser))!;
        var index = entityType.GetIndexes().Single(i => i.GetDatabaseName() == "ix_app_users_normalized_email");

        Assert.True(index.IsUnique);
        Assert.Single(index.Properties);
        Assert.Equal("normalized_email", index.Properties[0].GetColumnName());
    }

    [Fact]
    public void LearningProfile_HasCorrectPrimaryKey()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(LearningProfile))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        Assert.Single(primaryKey.Properties);
        Assert.Equal("id", primaryKey.Properties[0].GetColumnName());
    }

    [Fact]
    public void LearningProfile_HasRequiredLanguageCodeWithMaxLength()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(LearningProfile))!;
        var languageCodeProperty = entityType.FindProperty("LanguageCode")!;

        Assert.False(languageCodeProperty.IsNullable);
        Assert.Equal(10, languageCodeProperty.GetMaxLength());
    }

    [Fact]
    public void LearningProfile_HasForeignKeyToAppUser()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(LearningProfile))!;
        var foreignKey = entityType.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(AppUser));

        Assert.Equal("user_id", foreignKey.Properties[0].GetColumnName());
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void LearningProfile_HasUniqueUserIdLanguageCodeIndex()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(LearningProfile))!;
        var index = entityType.GetIndexes().Single(i => i.GetDatabaseName() == "ix_learning_profiles_user_id_language_code");

        Assert.True(index.IsUnique);
        Assert.Equal(2, index.Properties.Count);
    }

    [Fact]
    public void LearningSource_HasCorrectPrimaryKey()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(LearningSource))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        Assert.Single(primaryKey.Properties);
        Assert.Equal("id", primaryKey.Properties[0].GetColumnName());
    }

    [Fact]
    public void LearningSource_HasForeignKeyToLearningProfile()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(LearningSource))!;
        var foreignKey = entityType.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(LearningProfile));

        Assert.Equal("learning_profile_id", foreignKey.Properties[0].GetColumnName());
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void VocabularyItem_HasCorrectPrimaryKey()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(VocabularyItem))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        Assert.Single(primaryKey.Properties);
        Assert.Equal("id", primaryKey.Properties[0].GetColumnName());
    }

    [Fact]
    public void VocabularyItem_HasRequiredTermAndNormalizedTerm()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(VocabularyItem))!;

        var termProperty = entityType.FindProperty("Term")!;
        Assert.False(termProperty.IsNullable);

        var normalizedTermProperty = entityType.FindProperty("NormalizedTerm")!;
        Assert.False(normalizedTermProperty.IsNullable);
    }

    [Fact]
    public void VocabularyItem_HasOptionalTranslation()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(VocabularyItem))!;
        var translationProperty = entityType.FindProperty("Translation")!;

        Assert.True(translationProperty.IsNullable);
    }

    [Fact]
    public void VocabularyItem_HasNonUniqueLearningProfileNormalizedTermIndex()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(VocabularyItem))!;
        var index = entityType.GetIndexes().Single(i => i.GetDatabaseName() == "ix_vocabulary_items_learning_profile_id_normalized_term");

        Assert.False(index.IsUnique);
        Assert.Equal(2, index.Properties.Count);
    }

    [Fact]
    public void VocabularyItem_HasForeignKeyToLearningProfile()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(VocabularyItem))!;
        var foreignKey = entityType.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(LearningProfile));

        Assert.Equal("learning_profile_id", foreignKey.Properties[0].GetColumnName());
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void GrammarPoint_HasCorrectPrimaryKey()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(GrammarPoint))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        Assert.Single(primaryKey.Properties);
        Assert.Equal("id", primaryKey.Properties[0].GetColumnName());
    }

    [Fact]
    public void GrammarPoint_HasRequiredNameAndExplanation()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(GrammarPoint))!;

        var nameProperty = entityType.FindProperty("Name")!;
        Assert.False(nameProperty.IsNullable);

        var explanationProperty = entityType.FindProperty("Explanation")!;
        Assert.False(explanationProperty.IsNullable);
    }

    [Fact]
    public void GrammarPoint_HasForeignKeyToLearningProfile()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(GrammarPoint))!;
        var foreignKey = entityType.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(LearningProfile));

        Assert.Equal("learning_profile_id", foreignKey.Properties[0].GetColumnName());
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Story_HasCorrectPrimaryKey()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(Story))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        Assert.Single(primaryKey.Properties);
        Assert.Equal("id", primaryKey.Properties[0].GetColumnName());
    }

    [Fact]
    public void Story_HasRequiredTitle()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(Story))!;
        var titleProperty = entityType.FindProperty("Title")!;

        Assert.False(titleProperty.IsNullable);
    }

    [Fact]
    public void Story_HasOptionalCurrentSummary()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(Story))!;
        var summaryProperty = entityType.FindProperty("CurrentSummary")!;

        Assert.True(summaryProperty.IsNullable);
    }

    [Fact]
    public void Story_HasForeignKeyToLearningProfile()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(Story))!;
        var foreignKey = entityType.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(LearningProfile));

        Assert.Equal("learning_profile_id", foreignKey.Properties[0].GetColumnName());
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void StoryChapter_HasCorrectPrimaryKey()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(StoryChapter))!;
        var primaryKey = entityType.FindPrimaryKey()!;

        Assert.Single(primaryKey.Properties);
        Assert.Equal("id", primaryKey.Properties[0].GetColumnName());
    }

    [Fact]
    public void StoryChapter_HasRequiredContentAndChapterNumber()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(StoryChapter))!;

        var contentProperty = entityType.FindProperty("Content")!;
        Assert.False(contentProperty.IsNullable);

        var chapterNumberProperty = entityType.FindProperty("ChapterNumber")!;
        Assert.False(chapterNumberProperty.IsNullable);
        Assert.Equal(typeof(int), chapterNumberProperty.ClrType);
    }

    [Fact]
    public void StoryChapter_HasOptionalResultingSummary()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(StoryChapter))!;
        var summaryProperty = entityType.FindProperty("ResultingSummary")!;

        Assert.True(summaryProperty.IsNullable);
    }

    [Fact]
    public void StoryChapter_HasForeignKeyToStory()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(StoryChapter))!;
        var foreignKey = entityType.GetForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(Story));

        Assert.Equal("story_id", foreignKey.Properties[0].GetColumnName());
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void StoryChapter_HasUniqueStoryIdChapterNumberIndex()
    {
        using var context = new VocabulaDbContext(_options);
        var entityType = context.Model.FindEntityType(typeof(StoryChapter))!;
        var index = entityType.GetIndexes().Single(i => i.GetDatabaseName() == "ix_story_chapters_story_id_chapter_number");

        Assert.True(index.IsUnique);
        Assert.Equal(2, index.Properties.Count);
    }

    [Fact]
    public void StoryChapter_HasChapterNumberCheckConstraint()
    {
        using var context = new VocabulaDbContext(_options);
        var designTimeModel = context.GetService<IDesignTimeModel>()!.Model;
        var entityType = designTimeModel.FindEntityType(typeof(StoryChapter))!;

        var checkConstraints = entityType.GetCheckConstraints().ToList();
        Assert.Single(checkConstraints);
        var chapterNumberConstraint = checkConstraints[0];
        var storeObjectIdentifier = StoreObjectIdentifier.Table("story_chapters", null);
        Assert.Equal("ck_story_chapters_chapter_number_gt_0", chapterNumberConstraint.GetName(storeObjectIdentifier));
    }

    [Fact]
    public void VocabulaDbContext_HasAllDbSets()
    {
        using var context = new VocabulaDbContext(_options);

        Assert.NotNull(context.AppUsers);
        Assert.NotNull(context.LearningProfiles);
        Assert.NotNull(context.LearningSources);
        Assert.NotNull(context.VocabularyItems);
        Assert.NotNull(context.GrammarPoints);
        Assert.NotNull(context.Stories);
        Assert.NotNull(context.StoryChapters);
    }
}
