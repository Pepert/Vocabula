using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vocabula.Core.Domain;

namespace Vocabula.Infrastructure.Persistence.Configurations;

public sealed class StoryConfiguration : IEntityTypeConfiguration<Story>
{
    public void Configure(EntityTypeBuilder<Story> builder)
    {
        builder.ToTable("stories");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.LearningProfileId)
            .HasColumnName("learning_profile_id")
            .IsRequired();

        builder.Property(s => s.Title)
            .HasColumnName("title")
            .IsRequired();

        builder.Property(s => s.CurrentSummary)
            .HasColumnName("current_summary");

        builder.HasOne(s => s.LearningProfile)
            .WithMany(p => p.Stories)
            .HasForeignKey(s => s.LearningProfileId)
            .HasConstraintName("fk_stories_learning_profiles")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.LearningProfile)
            .HasField("_learningProfile")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Navigation(s => s.Chapters)
            .HasField("_chapters")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
