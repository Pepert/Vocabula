using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vocabula.Core.Domain;

namespace Vocabula.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(u => u.Email)
            .HasColumnName("email")
            .HasMaxLength(320)
            .IsRequired(false);

        builder.Property(u => u.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasMaxLength(320)
            .IsRequired(false);

        builder.Property(u => u.IdentityProvider)
            .HasColumnName("identity_provider")
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(u => u.ExternalSubject)
            .HasColumnName("external_subject")
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(u => u.Language)
            .HasColumnName("language")
            .HasMaxLength(5)
            .IsRequired(false);

        builder.Property(u => u.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasIndex(u => u.NormalizedEmail)
            .HasDatabaseName("ix_app_users_normalized_email")
            .IsUnique();

        builder.HasIndex(u => new { u.IdentityProvider, u.ExternalSubject })
            .HasDatabaseName("ix_app_users_identity_provider_external_subject")
            .IsUnique()
            .HasFilter("identity_provider IS NOT NULL AND external_subject IS NOT NULL");

        builder.Navigation(u => u.LearningProfiles)
            .HasField("_learningProfiles")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
