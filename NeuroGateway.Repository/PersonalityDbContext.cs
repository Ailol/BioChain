using Microsoft.EntityFrameworkCore;
using NeuroGateway.Repository.Entities;

namespace NeuroGateway.Repository;

public class PersonalityDbContext : DbContext
{
    public PersonalityDbContext(DbContextOptions<PersonalityDbContext> options) : base(options) { }

    public DbSet<Person> Persons => Set<Person>();
    public DbSet<Neurotransmitter> Neurotransmitters => Set<Neurotransmitter>();
    public DbSet<Hormone> Hormones => Set<Hormone>();
    public DbSet<Peptide> Peptides => Set<Peptide>();
    public DbSet<Personality> Personalities => Set<Personality>();
    public DbSet<AnalyzedData> AnalyzedDataSet => Set<AnalyzedData>();
    public DbSet<NeurotransmitterProfile> NeurotransmitterProfiles => Set<NeurotransmitterProfile>();
    public DbSet<HormoneProfile> HormoneProfiles => Set<HormoneProfile>();
    public DbSet<PeptideProfile> PeptideProfiles => Set<PeptideProfile>();
    public DbSet<AgentGroup> AgentGroups => Set<AgentGroup>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<RelationshipType> RelationshipTypes => Set<RelationshipType>();
    public DbSet<AgentTemplate> AgentTemplates => Set<AgentTemplate>();
    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<Layer> Layers => Set<Layer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PersonalityDbContext).Assembly);
    }
}
