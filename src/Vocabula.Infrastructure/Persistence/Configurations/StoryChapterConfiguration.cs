using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vocabula.Core.Domain;

namespace Vocabula.Infrastructure.Persistence.Configurations;

public sealed class StoryChapterConfiguration : IEntityTypeConfiguration<StoryChapter>
{
    public void Configure(EntityTypeBuilder<StoryChapter> builder)
    {
        builder.ToTable("story_chapters");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(c => c.StoryId)
            .HasColumnName("story_id")
            .IsRequired();

        builder.Property(c => c.ChapterNumber)
            .HasColumnName("chapter_number")
            .IsRequired();

        builder.Property(c => c.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(c => c.ResultingSummary)
            .HasColumnName("resulting_summary");

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(c => new { c.StoryId, c.ChapterNumber })
            .HasDatabaseName("ix_story_chapters_story_id_chapter_number")
            .IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("ck_story_chapters_chapter_number_gt_0", "\"chapter_number\" > 0"));

        builder.HasOne(c => c.Story)
            .WithMany(s => s.Chapters)
            .HasForeignKey(c => c.StoryId)
            .HasConstraintName("fk_story_chapters_stories")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Story)
            .HasField("_story")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
