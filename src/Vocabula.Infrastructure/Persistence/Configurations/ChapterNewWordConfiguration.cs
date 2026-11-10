using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vocabula.Core.Domain;

namespace Vocabula.Infrastructure.Persistence.Configurations;

public sealed class ChapterNewWordConfiguration : IEntityTypeConfiguration<ChapterNewWord>
{
    public void Configure(EntityTypeBuilder<ChapterNewWord> builder)
    {
        builder.ToTable("chapter_new_words");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(w => w.ChapterId)
            .HasColumnName("chapter_id")
            .IsRequired();

        builder.Property(w => w.Word)
            .HasColumnName("word")
            .IsRequired();

        builder.Property(w => w.Kind)
            .HasColumnName("kind")
            .IsRequired();

        builder.Property(w => w.Definition)
            .HasColumnName("definition");

        builder.Property(w => w.Position)
            .HasColumnName("position")
            .IsRequired();

        builder.HasIndex(w => w.ChapterId)
            .HasDatabaseName("ix_chapter_new_words_chapter_id");

        builder.HasOne(w => w.Chapter)
            .WithMany(c => c.NewWords)
            .HasForeignKey(w => w.ChapterId)
            .HasConstraintName("fk_chapter_new_words_story_chapters")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(w => w.Chapter)
            .HasField("_chapter")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
