using Microsoft.EntityFrameworkCore;
using Vocabula.Core.Domain;

namespace Vocabula.Infrastructure.Persistence;

public sealed class VocabulaDbContext : DbContext
{
    public VocabulaDbContext(DbContextOptions<VocabulaDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> AppUsers { get; set; } = null!;
    public DbSet<LearningProfile> LearningProfiles { get; set; } = null!;
    public DbSet<LearningSource> LearningSources { get; set; } = null!;
    public DbSet<VocabularyItem> VocabularyItems { get; set; } = null!;
    public DbSet<GrammarPoint> GrammarPoints { get; set; } = null!;
    public DbSet<Story> Stories { get; set; } = null!;
    public DbSet<StoryChapter> StoryChapters { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VocabulaDbContext).Assembly);
    }
}
