using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace BioChain.Repository.Data;

public class BioChainDbContext(DbContextOptions<BioChainDbContext> options) : DbContext(options)
{
    public DbSet<SubjectEntity> Subjects => Set<SubjectEntity>();
    public DbSet<StimuliEntity> Stimuli => Set<StimuliEntity>();
    public DbSet<ModuleEntity> Modules => Set<ModuleEntity>();
    public DbSet<RegionEntity> Regions => Set<RegionEntity>();
    public DbSet<SignalEntity> Signals => Set<SignalEntity>();
    public DbSet<ReceptorEntity> Receptors => Set<ReceptorEntity>();
    public DbSet<TransporterEntity> Transporters => Set<TransporterEntity>();
    public DbSet<GateEntity> Gates => Set<GateEntity>();
    public DbSet<LimiterEntity> Limiters => Set<LimiterEntity>();
    public DbSet<InterfaceEntity> Interfaces => Set<InterfaceEntity>();
    public DbSet<ConstraintDefEntity> Constraints => Set<ConstraintDefEntity>();
    public DbSet<ToolEntity> Tools => Set<ToolEntity>();
    public DbSet<AnalysisEntity> Analyses => Set<AnalysisEntity>();
    public DbSet<LoopEntity> Loops => Set<LoopEntity>();
    public DbSet<PlasticityEntity> Plasticities => Set<PlasticityEntity>();
    public DbSet<PathwayEntity> Pathways => Set<PathwayEntity>();
    public DbSet<EdgeEntity> Edges => Set<EdgeEntity>();
    public DbSet<PersonShareEntity> PersonShares => Set<PersonShareEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<QuestionnaireItemEntity> QuestionnaireItems => Set<QuestionnaireItemEntity>();
    public DbSet<QuestionnaireEntity> Questionnaires => Set<QuestionnaireEntity>();
    public DbSet<QuestionnaireAnswerEntity> QuestionnaireAnswers => Set<QuestionnaireAnswerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pg_trgm");

        ConfigureSubject(modelBuilder);
        ConfigureStimuli(modelBuilder);
        ConfigureModule(modelBuilder);
        ConfigureRegion(modelBuilder);
        ConfigureSignal(modelBuilder);
        ConfigureReceptor(modelBuilder);
        ConfigureTransporter(modelBuilder);
        ConfigureGate(modelBuilder);
        ConfigureLimiter(modelBuilder);
        ConfigureInterface(modelBuilder);
        ConfigureLoop(modelBuilder);
        ConfigurePlasticity(modelBuilder);
        ConfigurePathway(modelBuilder);
        ConfigureConstraintDef(modelBuilder);
        ConfigureTool(modelBuilder);
        ConfigureAnalysis(modelBuilder);
        ConfigureEdge(modelBuilder);
        ConfigurePersonShare(modelBuilder);
        ConfigureUserRole(modelBuilder);
        ConfigureQuestionnaireItem(modelBuilder);
        ConfigureQuestionnaire(modelBuilder);
        ConfigureQuestionnaireAnswer(modelBuilder);
    }

    private static void ConfigureSubject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SubjectEntity>(entity =>
        {
            entity.ToTable("entity");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.OwnerId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Namespace).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Kind).HasMaxLength(30).IsRequired().HasDefaultValueSql("'person'");
            entity.Property(e => e.Meta).HasColumnName("data").HasColumnType("jsonb");
            entity.Property(e => e.Embedding).HasColumnType("vector(2560)");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => new { e.OwnerId, e.Namespace, e.Name }).IsUnique();
            entity.HasIndex(e => e.Namespace);
            entity.HasIndex(e => e.Kind);
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");

            entity.HasMany(e => e.Stimuli)
                .WithOne(d => d.Subject)
                .HasForeignKey(d => d.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Modules)
                .WithOne(m => m.Subject)
                .HasForeignKey(m => m.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Signals)
                .WithOne(s => s.Subject)
                .HasForeignKey(s => s.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Receptors)
                .WithOne(r => r.Subject)
                .HasForeignKey(r => r.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Transporters)
                .WithOne(t => t.Subject)
                .HasForeignKey(t => t.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Gates)
                .WithOne(g => g.Subject)
                .HasForeignKey(g => g.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Limiters)
                .WithOne(l => l.Subject)
                .HasForeignKey(l => l.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Interfaces)
                .WithOne(i => i.Subject)
                .HasForeignKey(i => i.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Regions)
                .WithOne(r => r.Subject)
                .HasForeignKey(r => r.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Loops)
                .WithOne(l => l.Subject)
                .HasForeignKey(l => l.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Plasticities)
                .WithOne(p => p.Subject)
                .HasForeignKey(p => p.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Pathways)
                .WithOne(p => p.Subject)
                .HasForeignKey(p => p.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Edges)
                .WithOne(edge => edge.Subject)
                .HasForeignKey(edge => edge.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Constraints)
                .WithOne(c => c.Subject)
                .HasForeignKey(c => c.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Tools)
                .WithOne(t => t.Subject)
                .HasForeignKey(t => t.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Shares)
                .WithOne(s => s.Subject)
                .HasForeignKey(s => s.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Questionnaires)
                .WithOne(q => q.Subject)
                .HasForeignKey(q => q.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureStimuli(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StimuliEntity>(entity =>
        {
            entity.ToTable("stimuli");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Kind).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Content).HasColumnType("jsonb");
            entity.Property(e => e.Embedding).HasColumnType("vector(2560)");
            entity.Property(e => e.Analyzed).HasDefaultValue(false);
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.Kind);
            entity.HasIndex(e => e.Analyzed);
            entity.HasIndex(e => e.CreatedOnUtc);
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
        });
    }

    private static void ConfigureModule(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ModuleEntity>(entity =>
        {
            entity.ToTable("module");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Namespace).HasMaxLength(30);
            entity.Property(e => e.AgentType).HasMaxLength(30);
            entity.Property(e => e.Properties).HasColumnType("jsonb");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.SubjectId, e.Code }).IsUnique();
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => e.AgentType);
            entity.HasIndex(e => e.Namespace);

            entity.HasOne(e => e.Parent)
                .WithMany(m => m.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRegion(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RegionEntity>(entity =>
        {
            entity.ToTable("region");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Code).HasMaxLength(30).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.System).HasMaxLength(30);
            entity.Property(e => e.ActivityState).HasMaxLength(15).HasDefaultValueSql("'unknown'");
            entity.Property(e => e.DominantSignal).HasMaxLength(20);
            entity.Property(e => e.StressLoad).HasMaxLength(5).HasDefaultValueSql("'≈'");
            entity.Property(e => e.Properties).HasColumnType("jsonb");
            entity.Property(e => e.Embedding).HasColumnType("vector(2560)");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            // Global template regions (entity_id IS NULL)
            entity.HasIndex(e => new { e.Code, e.SubjectId })
                .IsUnique()
                .HasFilter("entity_id IS NULL");

            // Per-entity observation: temporal for DISTINCT ON
            entity.HasIndex(e => new { e.SubjectId, e.Code, e.CreatedOnUtc })
                .IsDescending(false, false, true);

            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.System);
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => e.ModuleId);

            entity.HasOne(e => e.Parent)
                .WithMany(r => r.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.Signals)
                .WithOne(s => s.Region)
                .HasForeignKey(s => s.RegionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasMany(e => e.SourceInterfaces)
                .WithOne(i => i.SourceRegion)
                .HasForeignKey(i => i.SourceRegionId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.TargetInterfaces)
                .WithOne(i => i.TargetRegion)
                .HasForeignKey(i => i.TargetRegionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureSignal(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SignalEntity>(entity =>
        {
            entity.ToTable("signal");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Type).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(30).IsRequired();
            entity.Property(e => e.State).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.Confidence).HasDefaultValue(1.0m);
            entity.Property(e => e.Distribution).HasMaxLength(30);
            entity.Property(e => e.Trend).HasMaxLength(15);
            entity.Property(e => e.Embedding).HasColumnType("vector(2560)");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            // Append-only: temporal index for DISTINCT ON queries
            entity.HasIndex(e => new { e.SubjectId, e.Code, e.RegionId, e.CreatedOnUtc })
                .IsDescending(false, false, false, true);
            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Code);
            entity.HasIndex(e => e.RegionId);
            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.AnalysisId);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureReceptor(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReceptorEntity>(entity =>
        {
            entity.ToTable("receptor");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Code).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Subtype).HasMaxLength(20);
            entity.Property(e => e.State).HasMaxLength(10).IsRequired();
            entity.Property(e => e.SignalCode).HasMaxLength(30);
            entity.Property(e => e.SignalType).HasMaxLength(10);
            entity.Property(e => e.Embedding).HasColumnType("vector(2560)");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            // Append-only: temporal index
            entity.HasIndex(e => new { e.SubjectId, e.Code, e.CreatedOnUtc })
                .IsDescending(false, false, true);
            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.SignalId);
            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.State);
            entity.HasIndex(e => e.AnalysisId);
            entity.HasIndex(e => new { e.SubjectId, e.SignalCode });

            entity.HasOne(e => e.Signal)
                .WithMany(s => s.Receptors)
                .HasForeignKey(e => e.SignalId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureTransporter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransporterEntity>(entity =>
        {
            entity.ToTable("transporter");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Code).HasMaxLength(30).IsRequired();
            entity.Property(e => e.State).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Clearance).HasMaxLength(5).HasDefaultValueSql("'≈'");
            entity.Property(e => e.SignalCode).HasMaxLength(30);
            entity.Property(e => e.SignalType).HasMaxLength(10);
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            // Append-only: temporal index
            entity.HasIndex(e => new { e.SubjectId, e.Code, e.CreatedOnUtc })
                .IsDescending(false, false, true);
            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.SignalId);
            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.State);
            entity.HasIndex(e => e.AnalysisId);
            entity.HasIndex(e => new { e.SubjectId, e.SignalCode });

            entity.HasOne(e => e.Signal)
                .WithMany(s => s.Transporters)
                .HasForeignKey(e => e.SignalId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureGate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GateEntity>(entity =>
        {
            entity.ToTable("gate");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Code).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(15).IsRequired();
            entity.Property(e => e.Threshold).HasMaxLength(5);
            entity.Property(e => e.Latched).HasDefaultValue(false);
            entity.Property(e => e.Model).HasMaxLength(50);
            entity.Property(e => e.ParseMap).HasColumnType("jsonb");
            entity.Property(e => e.Embedding).HasColumnType("vector(2560)");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            // Append-only: temporal index
            entity.HasIndex(e => new { e.SubjectId, e.Code, e.CreatedOnUtc })
                .IsDescending(false, false, true);
            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => e.Latched);
            entity.HasIndex(e => e.AnalysisId);

            entity.HasOne(e => e.Parent)
                .WithMany(g => g.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureLimiter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LimiterEntity>(entity =>
        {
            entity.ToTable("limiter");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Code).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Activity).HasMaxLength(10).HasDefaultValueSql("'≈'");
            entity.Property(e => e.RateLimiting).HasDefaultValue(false);
            entity.Property(e => e.Embedding).HasColumnType("vector(2560)");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            // Append-only: temporal index
            entity.HasIndex(e => new { e.SubjectId, e.Code, e.CreatedOnUtc })
                .IsDescending(false, false, true);
            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.TargetId);
            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.RateLimiting);
            entity.HasIndex(e => e.AnalysisId);

            entity.HasOne(e => e.Target)
                .WithMany(s => s.Limiters)
                .HasForeignKey(e => e.TargetId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureInterface(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InterfaceEntity>(entity =>
        {
            entity.ToTable("interface");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Code).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Pathway).HasMaxLength(50);
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            // Append-only: temporal index
            entity.HasIndex(e => new { e.SubjectId, e.Code, e.CreatedOnUtc })
                .IsDescending(false, false, true);
            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.SourceRegionId);
            entity.HasIndex(e => e.TargetRegionId);
            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.Pathway);
            entity.HasIndex(e => e.PathwayId).HasFilter("pathway_id IS NOT NULL");
            entity.HasIndex(e => e.Active);
            entity.HasIndex(e => e.AnalysisId);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.PathwayRef)
                .WithMany(p => p.Interfaces)
                .HasForeignKey(e => e.PathwayId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureLoop(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LoopEntity>(entity =>
        {
            entity.ToTable("loop");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Polarity).HasMaxLength(5).IsRequired();
            entity.Property(e => e.Subtype).HasMaxLength(20);
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => new { e.SubjectId, e.ModuleId });
            entity.HasIndex(e => new { e.SubjectId, e.Polarity });
            entity.HasIndex(e => e.SubjectId).HasFilter("active = true").HasDatabaseName("idx_loop_active");
            entity.HasIndex(e => e.SubjectId).HasFilter("gain_product > 1 AND active = true").HasDatabaseName("idx_loop_runaway");
            entity.HasIndex(e => e.AnalysisId);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigurePlasticity(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PlasticityEntity>(entity =>
        {
            entity.ToTable("plasticity");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.PlasticityType).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Timescale).HasMaxLength(20);
            entity.Property(e => e.Consolidation).HasDefaultValue(false);
            entity.Property(e => e.Reversible).HasDefaultValue(true);
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.EdgeId);
            entity.HasIndex(e => e.ReceptorId);
            entity.HasIndex(e => new { e.SubjectId, e.PlasticityType });
            entity.HasIndex(e => e.SubjectId).HasFilter("consolidation = true").HasDatabaseName("idx_plast_solid");
            entity.HasIndex(e => new { e.SubjectId, e.CreatedOnUtc }).IsDescending(false, true);
            entity.HasIndex(e => e.InductionId);
            entity.HasIndex(e => e.AnalysisId);

            entity.HasOne(e => e.Edge)
                .WithMany()
                .HasForeignKey(e => e.EdgeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Receptor)
                .WithMany()
                .HasForeignKey(e => e.ReceptorId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Induction)
                .WithMany()
                .HasForeignKey(e => e.InductionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigurePathway(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PathwayEntity>(entity =>
        {
            entity.ToTable("pathway");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => new { e.SubjectId, e.ModuleId });
            entity.HasIndex(e => e.SourceRegionId);
            entity.HasIndex(e => e.TargetRegionId);
            entity.HasIndex(e => e.SubjectId).HasFilter("active = true").HasDatabaseName("idx_pathway_active");
            entity.HasIndex(e => e.AnalysisId);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.SourceRegion)
                .WithMany(r => r.SourcePathways)
                .HasForeignKey(e => e.SourceRegionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.TargetRegion)
                .WithMany(r => r.TargetPathways)
                .HasForeignKey(e => e.TargetRegionId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureConstraintDef(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConstraintDefEntity>(entity =>
        {
            entity.ToTable("constraint_def");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Type).HasMaxLength(15).IsRequired();
            entity.Property(e => e.Expression).IsRequired();
            entity.Property(e => e.Confidence).HasDefaultValue(1.0m);
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.Active);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureTool(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ToolEntity>(entity =>
        {
            entity.ToTable("tool");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Code).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Invoke).IsRequired();
            entity.Property(e => e.TimeoutMs).HasDefaultValue(10000);
            entity.Property(e => e.RetryCount).HasDefaultValue(0);
            entity.Property(e => e.Fallback).HasColumnType("jsonb");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.Code);
            entity.HasIndex(e => e.ModuleId);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureAnalysis(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AnalysisEntity>(entity =>
        {
            entity.ToTable("analysis");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.Tag).HasMaxLength(30);
            entity.Property(e => e.Formula).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20);
            entity.Property(e => e.Phase).HasMaxLength(20);
            entity.Property(e => e.Embedding).HasColumnType("vector(2560)");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.StimuliId);
            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.Tag);
            entity.HasIndex(e => e.Phase);
            entity.HasIndex(e => new { e.SubjectId, e.CreatedOnUtc })
                .IsDescending(false, true);
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");

            entity.HasOne(e => e.Subject)
                .WithMany()
                .HasForeignKey(e => e.SubjectId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Stimuli)
                .WithMany()
                .HasForeignKey(e => e.StimuliId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigureEdge(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EdgeEntity>(entity =>
        {
            entity.ToTable("edge");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("entity_id");
            entity.Property(e => e.SourceType).HasMaxLength(15).IsRequired();
            entity.Property(e => e.TargetType).HasMaxLength(15).IsRequired();
            entity.Property(e => e.Operator).HasMaxLength(20).IsRequired();
            entity.Property(e => e.OperatorClass).HasMaxLength(15).IsRequired();
            entity.Property(e => e.TransferFn).HasMaxLength(10);
            entity.Property(e => e.DysregType).HasMaxLength(20);
            entity.Property(e => e.SourceCode).HasMaxLength(30);
            entity.Property(e => e.SourceSignalType).HasMaxLength(10);
            entity.Property(e => e.SourceRegion).HasMaxLength(30);
            entity.Property(e => e.TargetCode).HasMaxLength(30);
            entity.Property(e => e.TargetSignalType).HasMaxLength(10);
            entity.Property(e => e.TargetRegion).HasMaxLength(30);
            entity.Property(e => e.RelationshipKind).HasMaxLength(30);
            entity.Property(e => e.GateCode).HasMaxLength(100);
            entity.Property(e => e.GateType).HasMaxLength(15);
            entity.Property(e => e.GateCondition);
            entity.Property(e => e.Properties).HasColumnType("jsonb");
            entity.Property(e => e.Embedding).HasColumnType("vector(2560)");
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            // Walk indexes
            entity.HasIndex(e => new { e.SourceType, e.SourceId, e.Active });
            entity.HasIndex(e => new { e.TargetType, e.TargetId, e.Active });
            entity.HasIndex(e => e.SubjectId);
            entity.HasIndex(e => e.OperatorClass);
            entity.HasIndex(e => e.ModuleId);
            entity.HasIndex(e => e.AnalysisId);
            entity.HasIndex(e => e.GateId).HasFilter("gate_id IS NOT NULL");
            entity.HasIndex(e => e.LoopId).HasFilter("loop_id IS NOT NULL");
            entity.HasIndex(e => e.PathwayId).HasFilter("pathway_id IS NOT NULL");
            entity.HasIndex(e => new { e.SubjectId, e.DysregType }).HasFilter("dysreg_type IS NOT NULL");
            entity.HasIndex(e => e.ToolId).HasFilter("tool_id IS NOT NULL");
            entity.HasIndex(e => new { e.SubjectId, e.SourceCode });
            entity.HasIndex(e => new { e.SubjectId, e.TargetCode });
            entity.HasIndex(e => new { e.SubjectId, e.RelationshipKind }).HasFilter("relationship_kind IS NOT NULL");

            entity.HasOne(e => e.Gate)
                .WithMany()
                .HasForeignKey(e => e.GateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Loop)
                .WithMany(l => l.Edges)
                .HasForeignKey(e => e.LoopId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Pathway)
                .WithMany(p => p.Edges)
                .HasForeignKey(e => e.PathwayId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Module)
                .WithMany()
                .HasForeignKey(e => e.ModuleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Tool)
                .WithMany()
                .HasForeignKey(e => e.ToolId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Analysis)
                .WithMany()
                .HasForeignKey(e => e.AnalysisId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }

    private static void ConfigurePersonShare(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersonShareEntity>(entity =>
        {
            entity.ToTable("person_share");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.SubjectId).HasColumnName("person_id");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.SubjectId, e.SharedWithEmail }).IsUnique();
            entity.HasIndex(e => e.SharedWithUserId);
            entity.HasIndex(e => e.SharedWithEmail);
        });
    }

    private static void ConfigureUserRole(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserRoleEntity>(entity =>
        {
            entity.ToTable("user_role");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.UserId).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(20).IsRequired();
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.UserId, e.Role }).IsUnique();
            entity.HasIndex(e => e.UserId);
        });
    }

    private static void ConfigureQuestionnaireItem(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuestionnaireItemEntity>(entity =>
        {
            entity.ToTable("questionnaire_item");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Label).HasMaxLength(1).IsRequired();
            entity.Property(e => e.PrimarySignal).HasMaxLength(30);
            entity.Property(e => e.SecondarySignal).HasMaxLength(30);
            entity.Property(e => e.Data).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.SortOrder, e.Label }).IsUnique();
            entity.HasIndex(e => e.SortOrder);
        });
    }

    private static void ConfigureQuestionnaire(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuestionnaireEntity>(entity =>
        {
            entity.ToTable("questionnaire");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.SubjectId).HasColumnName("person_id");
            entity.Property(e => e.Token).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Data).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.SubjectId);

            entity.HasMany(e => e.Answers)
                .WithOne(a => a.Questionnaire)
                .HasForeignKey(a => a.QuestionnaireId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureQuestionnaireAnswer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QuestionnaireAnswerEntity>(entity =>
        {
            entity.ToTable("questionnaire_answer");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.QuestionnaireId);
            entity.HasIndex(e => new { e.QuestionnaireId, e.ItemId }).IsUnique();

            entity.HasOne(e => e.Item)
                .WithMany(i => i.Answers)
                .HasForeignKey(e => e.ItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
