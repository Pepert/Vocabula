using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vocabula.Core.Domain;

namespace Vocabula.Infrastructure.Persistence.Configurations;

public sealed class GrammarPointConfiguration : IEntityTypeConfiguration<GrammarPoint>
{
    public void Configure(EntityTypeBuilder<GrammarPoint> builder)
    {
        builder.ToTable("grammar_points");

        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(g => g.LearningProfileId)
            .HasColumnName("learning_profile_id")
            .IsRequired();

        builder.Property(g => g.Name)
            .HasColumnName("name")
            .IsRequired();

        builder.Property(g => g.Explanation)
            .HasColumnName("explanation")
            .IsRequired();

        builder.Property(g => g.FirstSeenAt)
            .HasColumnName("first_seen_at")
            .IsRequired();

        builder.Property(g => g.LastUsedAt)
            .HasColumnName("last_used_at");

        builder.Property(g => g.TimesUsed)
            .HasColumnName("times_used")
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasOne(g => g.LearningProfile)
            .WithMany(p => p.GrammarPoints)
            .HasForeignKey(g => g.LearningProfileId)
            .HasConstraintName("fk_grammar_points_learning_profiles")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(g => g.LearningProfile)
            .HasField("_learningProfile")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
