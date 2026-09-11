using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vocabula.Core.Domain;

namespace Vocabula.Infrastructure.Persistence.Configurations;

public sealed class VocabularyItemConfiguration : IEntityTypeConfiguration<VocabularyItem>
{
    public void Configure(EntityTypeBuilder<VocabularyItem> builder)
    {
        builder.ToTable("vocabulary_items");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(i => i.LearningProfileId)
            .HasColumnName("learning_profile_id")
            .IsRequired();

        builder.Property(i => i.Term)
            .HasColumnName("term")
            .IsRequired();

        builder.Property(i => i.NormalizedTerm)
            .HasColumnName("normalized_term")
            .IsRequired();

        builder.Property(i => i.Translation)
            .HasColumnName("translation");

        builder.Property(i => i.FirstSeenAt)
            .HasColumnName("first_seen_at")
            .IsRequired();

        builder.Property(i => i.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(i => i.TimesUsed)
            .HasColumnName("times_used")
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasIndex(i => new { i.LearningProfileId, i.NormalizedTerm })
            .HasDatabaseName("ix_vocabulary_items_learning_profile_id_normalized_term")
            .IsUnique(false);

        builder.HasIndex(i => new { i.LearningProfileId, i.FirstSeenAt })
            .HasDatabaseName("ix_vocabulary_items_learning_profile_id_first_seen_at");

        builder.HasIndex(i => new { i.LearningProfileId, i.LastUsedAt })
            .HasDatabaseName("ix_vocabulary_items_learning_profile_id_last_used_at");

        builder.HasOne(i => i.LearningProfile)
            .WithMany(p => p.VocabularyItems)
            .HasForeignKey(i => i.LearningProfileId)
            .HasConstraintName("fk_vocabulary_items_learning_profiles")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.LearningProfile)
            .HasField("_learningProfile")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
