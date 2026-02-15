using Microsoft.EntityFrameworkCore;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository;

public class PersonalityDbContext(DbContextOptions<PersonalityDbContext> options) : DbContext(options)
{
    public DbSet<PersonEntity> Persons => Set<PersonEntity>();
    public DbSet<PersonalityEntity> Personalities => Set<PersonalityEntity>();
    public DbSet<AnalyzedDataEntity> AnalyzedData => Set<AnalyzedDataEntity>();
    public DbSet<BiochemicalProfileEntity> BiochemicalProfiles => Set<BiochemicalProfileEntity>();
    public DbSet<RelationshipTypeEntity> RelationshipTypes => Set<RelationshipTypeEntity>();
    public DbSet<AgentTemplateEntity> AgentTemplates => Set<AgentTemplateEntity>();
    public DbSet<AgentGroupEntity> AgentGroups => Set<AgentGroupEntity>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();
    public DbSet<PipelineEntity> Pipelines => Set<PipelineEntity>();
    public DbSet<LayerEntity> Layers => Set<LayerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pg_trgm");

        // Person
        modelBuilder.Entity<PersonEntity>(e =>
        {
            e.ToTable("person");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("uuid_generate_v4()");
        });

        // Personality
        modelBuilder.Entity<PersonalityEntity>(e =>
        {
            e.ToTable("personality");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PersonId).IsUnique();
        });

        // AnalyzedData
        modelBuilder.Entity<AnalyzedDataEntity>(e =>
        {
            e.ToTable("analyzed_data");
            e.HasKey(x => x.Id);
            e.Property(x => x.Embedding).HasColumnType("vector(4096)");
        });

        // BiochemicalProfile
        modelBuilder.Entity<BiochemicalProfileEntity>(e =>
        {
            e.ToTable("biochemical_profile");
            e.HasKey(x => x.Id);
            e.Property(x => x.Embedding).HasColumnType("vector(4096)");
            e.HasIndex(x => new { x.PersonalityId, x.AnalyzedDataId, x.Chemical }).IsUnique();
        });

        // RelationshipType
        modelBuilder.Entity<RelationshipTypeEntity>(e =>
        {
            e.ToTable("relationship_type");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
        });

        // AgentTemplate
        modelBuilder.Entity<AgentTemplateEntity>(e =>
        {
            e.ToTable("agent_template");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Category, x.GroupName, x.Name }).IsUnique();
        });

        // AgentGroup
        modelBuilder.Entity<AgentGroupEntity>(e =>
        {
            e.ToTable("agent_group");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("uuid_generate_v4()");
            e.HasMany(x => x.Agents).WithOne().HasForeignKey(a => a.GroupId).OnDelete(DeleteBehavior.Cascade);
        });

        // Agent
        modelBuilder.Entity<AgentEntity>(e =>
        {
            e.ToTable("agent");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.GroupId, x.Name }).IsUnique();
        });

        // Pipeline
        modelBuilder.Entity<PipelineEntity>(e =>
        {
            e.ToTable("pipeline");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PersonId, x.Name }).IsUnique();
        });

        // Layer
        modelBuilder.Entity<LayerEntity>(e =>
        {
            e.ToTable("layer");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PipelineId, x.SortOrder }).IsUnique();
        });
    }
}
