using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vocabula.Core.Domain;

namespace Vocabula.Infrastructure.Persistence.Configurations;

public sealed class LearningSourceConfiguration : IEntityTypeConfiguration<LearningSource>
{
    public void Configure(EntityTypeBuilder<LearningSource> builder)
    {
        builder.ToTable("learning_sources");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(s => s.LearningProfileId)
            .HasColumnName("learning_profile_id")
            .IsRequired();

        builder.Property(s => s.ProcessingStatus)
            .HasColumnName("processing_status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne(s => s.LearningProfile)
            .WithMany(p => p.LearningSources)
            .HasForeignKey(s => s.LearningProfileId)
            .HasConstraintName("fk_learning_sources_learning_profiles")
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(s => s.LearningProfile)
            .HasField("_learningProfile")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
