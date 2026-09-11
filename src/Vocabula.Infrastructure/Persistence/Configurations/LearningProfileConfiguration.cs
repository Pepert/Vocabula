using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vocabula.Core.Domain;

namespace Vocabula.Infrastructure.Persistence.Configurations;

public sealed class LearningProfileConfiguration : IEntityTypeConfiguration<LearningProfile>
{
    public void Configure(EntityTypeBuilder<LearningProfile> builder)
    {
        builder.ToTable("learning_profiles");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(p => p.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(p => p.LanguageCode)
            .HasColumnName("language_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(p => p.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(p => new { p.UserId, p.LanguageCode })
            .HasDatabaseName("ix_learning_profiles_user_id_language_code")
            .IsUnique();

        builder.HasOne(p => p.User)
            .WithMany(u => u.LearningProfiles)
            .HasForeignKey(p => p.UserId)
            .HasConstraintName("fk_learning_profiles_app_users")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.User)
            .HasField("_user")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.LearningSources)
            .HasField("_learningSources")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.VocabularyItems)
            .HasField("_vocabularyItems")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.GrammarPoints)
            .HasField("_grammarPoints")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(p => p.Stories)
            .HasField("_stories")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
