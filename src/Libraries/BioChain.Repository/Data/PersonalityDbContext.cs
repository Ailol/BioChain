using Microsoft.EntityFrameworkCore;
using BioChain.Repository.Entities;

namespace BioChain.Repository;

public class PersonalityDbContext(DbContextOptions<PersonalityDbContext> options) : DbContext(options)
{
    // ── CORE IDENTITY ──
    public DbSet<PersonEntity> Persons => Set<PersonEntity>();
    public DbSet<PersonShareEntity> PersonShares => Set<PersonShareEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<PersonalityEntity> Personalities => Set<PersonalityEntity>();

    // ── INPUT ──
    public DbSet<AnalyzedDataEntity> AnalyzedData => Set<AnalyzedDataEntity>();

    // ── DOMAIN REGISTRY ──
    public DbSet<DomainEntity> Domains => Set<DomainEntity>();

    // ── LAYER 0 — LEXICON ──
    public DbSet<SignalEntity> Signals => Set<SignalEntity>();
    public DbSet<ReceptorEntity> Receptors => Set<ReceptorEntity>();
    public DbSet<EnzymeEntity> Enzymes => Set<EnzymeEntity>();
    public DbSet<TransporterEntity> Transporters => Set<TransporterEntity>();
    public DbSet<SecondMessengerEntity> SecondMessengers => Set<SecondMessengerEntity>();
    public DbSet<BrainRegionEntity> BrainRegions => Set<BrainRegionEntity>();

    // ── LAYER 2 — OPERATORS ──
    public DbSet<SignalInteractionEntity> SignalInteractions => Set<SignalInteractionEntity>();

    // ── LAYER 3 — LOGIC GATES ──
    public DbSet<GateEntity> Gates => Set<GateEntity>();
    public DbSet<GateInstanceEntity> GateInstances => Set<GateInstanceEntity>();

    // ── LAYER 4 — LIFECYCLE ──
    public DbSet<LifecycleStageEntity> LifecycleStages => Set<LifecycleStageEntity>();

    // ── LAYER 5 — PATHWAYS ──
    public DbSet<PathwayEntity> Pathways => Set<PathwayEntity>();
    public DbSet<PathwayStepEntity> PathwaySteps => Set<PathwayStepEntity>();

    // ── LAYER 6 — CIRCUITS ──
    public DbSet<CircuitEntity> Circuits => Set<CircuitEntity>();
    public DbSet<CircuitPathwayEntity> CircuitPathways => Set<CircuitPathwayEntity>();
    public DbSet<CircuitPhaseEntity> CircuitPhases => Set<CircuitPhaseEntity>();
    public DbSet<DoseResponseEntity> DoseResponses => Set<DoseResponseEntity>();

    // ── DIMENSION ──
    public DbSet<DimensionEntity> Dimensions => Set<DimensionEntity>();
    public DbSet<DimensionSignalAffinityEntity> DimensionSignalAffinities => Set<DimensionSignalAffinityEntity>();

    // ── ANALYSIS ──
    public DbSet<AnalysisTypeEntity> AnalysisTypes => Set<AnalysisTypeEntity>();
    public DbSet<AnalysisDimensionEntity> AnalysisDimensions => Set<AnalysisDimensionEntity>();
    public DbSet<AnalysisRunEntity> AnalysisRuns => Set<AnalysisRunEntity>();

    // ── OBSERVATIONS ──
    public DbSet<ObservationEntity> Observations => Set<ObservationEntity>();

    // ── TAGS ──
    public DbSet<TagEntity> Tags => Set<TagEntity>();
    public DbSet<EntityTagEntity> EntityTags => Set<EntityTagEntity>();

    // ── TRAJECTORIES ──
    public DbSet<TrajectoryEntity> Trajectories => Set<TrajectoryEntity>();
    public DbSet<TrajectoryPhaseEntity> TrajectoryPhases => Set<TrajectoryPhaseEntity>();

    // ── ACTIVE LOOPS ──
    public DbSet<ActiveLoopEntity> ActiveLoops => Set<ActiveLoopEntity>();

    // ── PROFILE ──
    public DbSet<ProfileSnapshotEntity> ProfileSnapshots => Set<ProfileSnapshotEntity>();

    // ── EMBEDDING CACHE ──
    public DbSet<EmbeddingCacheEntity> EmbeddingCache => Set<EmbeddingCacheEntity>();

    // ── AGENT SYSTEM ──
    public DbSet<AgentTemplateEntity> AgentTemplates => Set<AgentTemplateEntity>();
    public DbSet<AgentGroupEntity> AgentGroups => Set<AgentGroupEntity>();
    public DbSet<AgentEntity> Agents => Set<AgentEntity>();

    // ── PIPELINE ──
    public DbSet<PipelineEntity> Pipelines => Set<PipelineEntity>();
    public DbSet<LayerEntity> Layers => Set<LayerEntity>();

    // ── QUESTIONNAIRE ──
    public DbSet<QuestionnaireItemEntity> QuestionnaireItems => Set<QuestionnaireItemEntity>();
    public DbSet<QuestionnaireEntity> Questionnaires => Set<QuestionnaireEntity>();
    public DbSet<QuestionnaireAnswerEntity> QuestionnaireAnswers => Set<QuestionnaireAnswerEntity>();

    // ── RELATIONSHIP ──
    public DbSet<RelationshipTypeEntity> RelationshipTypes => Set<RelationshipTypeEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.HasPostgresExtension("uuid-ossp");
        modelBuilder.HasPostgresExtension("pg_trgm");

        // ═══════════════════════════════════════════
        // CORE IDENTITY
        // ═══════════════════════════════════════════

        modelBuilder.Entity<PersonEntity>(e =>
        {
            e.ToTable("person");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("uuid_generate_v4()");
            e.HasIndex(x => new { x.OwnerId, x.FirstName }).IsUnique();
        });

        modelBuilder.Entity<PersonShareEntity>(e =>
        {
            e.ToTable("person_share");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PersonId, x.SharedWithEmail }).IsUnique();
        });

        modelBuilder.Entity<UserRoleEntity>(e =>
        {
            e.ToTable("user_role");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.Role }).IsUnique();
        });

        modelBuilder.Entity<PersonalityEntity>(e =>
        {
            e.ToTable("personality");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PersonId).IsUnique();
        });

        modelBuilder.Entity<AnalyzedDataEntity>(e =>
        {
            e.ToTable("analyzed_data");
            e.HasKey(x => x.Id);
            e.Property(x => x.Embedding).HasColumnType("vector(1536)");
            e.Property(x => x.Metadata).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // DOMAIN REGISTRY
        // ═══════════════════════════════════════════

        modelBuilder.Entity<DomainEntity>(e =>
        {
            e.ToTable("domain");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // LAYER 0 — LEXICON
        // ═══════════════════════════════════════════

        modelBuilder.Entity<SignalEntity>(e =>
        {
            e.ToTable("signal");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.DomainId);
            e.HasIndex(x => x.Layer);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<ReceptorEntity>(e =>
        {
            e.ToTable("receptor");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.SignalId);
            e.HasIndex(x => x.GProtein);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<EnzymeEntity>(e =>
        {
            e.ToTable("enzyme");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.SubstrateSignalId);
            e.HasIndex(x => x.ProductSignalId);
            e.HasIndex(x => x.Function);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<TransporterEntity>(e =>
        {
            e.ToTable("transporter");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.SignalId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<SecondMessengerEntity>(e =>
        {
            e.ToTable("second_messenger");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<BrainRegionEntity>(e =>
        {
            e.ToTable("brain_region");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.ParentRegionId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // LAYER 2 — OPERATORS / INTERACTIONS
        // ═══════════════════════════════════════════

        modelBuilder.Entity<SignalInteractionEntity>(e =>
        {
            e.ToTable("signal_interaction");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SourceSignalId, x.TargetSignalId, x.Operator, x.RegionId }).IsUnique();
            e.HasIndex(x => x.SourceSignalId);
            e.HasIndex(x => x.TargetSignalId);
            e.HasIndex(x => x.Operator);
            e.HasIndex(x => x.RegionId);
            e.HasIndex(x => x.ViaEnzymeId);
            e.HasIndex(x => x.ViaReceptorId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // LAYER 3 — LOGIC GATES
        // ═══════════════════════════════════════════

        modelBuilder.Entity<GateEntity>(e =>
        {
            e.ToTable("gate");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.GateType);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<GateInstanceEntity>(e =>
        {
            e.ToTable("gate_instance");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.GateId);
            e.HasIndex(x => x.OutputSignalId);
            e.HasIndex(x => x.RegionId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // LAYER 4 — LIFECYCLE
        // ═══════════════════════════════════════════

        modelBuilder.Entity<LifecycleStageEntity>(e =>
        {
            e.ToTable("lifecycle_stage");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SignalId, x.Stage }).IsUnique();
            e.HasIndex(x => x.SignalId);
            e.HasIndex(x => x.Stage);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // LAYER 5 — PATHWAYS
        // ═══════════════════════════════════════════

        modelBuilder.Entity<PathwayEntity>(e =>
        {
            e.ToTable("pathway");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.DomainId);
            e.HasIndex(x => x.PrimarySignalId);
            e.HasIndex(x => x.TemplateType);
            e.HasIndex(x => x.SourceRegionId);
            e.HasIndex(x => x.TargetRegionId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<PathwayStepEntity>(e =>
        {
            e.ToTable("pathway_step");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PathwayId, x.StepOrder }).IsUnique();
            e.HasIndex(x => x.PathwayId);
            e.HasIndex(x => x.SignalId);
            e.HasIndex(x => x.RegionId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // LAYER 6 — CIRCUITS
        // ═══════════════════════════════════════════

        modelBuilder.Entity<CircuitEntity>(e =>
        {
            e.ToTable("circuit");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.DomainId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<CircuitPathwayEntity>(e =>
        {
            e.ToTable("circuit_pathway");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CircuitId, x.PathwayId }).IsUnique();
            e.HasIndex(x => x.CircuitId);
            e.HasIndex(x => x.PathwayId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<CircuitPhaseEntity>(e =>
        {
            e.ToTable("circuit_phase");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CircuitId, x.PhaseOrder }).IsUnique();
            e.HasIndex(x => x.CircuitId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<DoseResponseEntity>(e =>
        {
            e.ToTable("dose_response");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SignalId);
            e.HasIndex(x => x.Pattern);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // DIMENSION
        // ═══════════════════════════════════════════

        modelBuilder.Entity<DimensionEntity>(e =>
        {
            e.ToTable("dimension");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<DimensionSignalAffinityEntity>(e =>
        {
            e.ToTable("dimension_signal_affinity");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.DimensionId, x.SignalId }).IsUnique();
            e.HasIndex(x => x.DimensionId);
            e.HasIndex(x => x.SignalId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // ANALYSIS
        // ═══════════════════════════════════════════

        modelBuilder.Entity<AnalysisTypeEntity>(e =>
        {
            e.ToTable("analysis_type");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.DomainId);
            e.HasIndex(x => x.Category);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<AnalysisDimensionEntity>(e =>
        {
            e.ToTable("analysis_dimension");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AnalysisTypeId, x.Key }).IsUnique();
            e.HasIndex(x => x.AnalysisTypeId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<AnalysisRunEntity>(e =>
        {
            e.ToTable("analysis_run");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("uuid_generate_v4()");
            e.HasIndex(x => x.PersonId);
            e.HasIndex(x => x.AnalysisTypeId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ParentRunId);
            e.Property(x => x.Summary).HasColumnType("jsonb");
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // OBSERVATIONS
        // ═══════════════════════════════════════════

        modelBuilder.Entity<ObservationEntity>(e =>
        {
            e.ToTable("observation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Embedding).HasColumnType("vector(1536)");
            e.Property(x => x.Metadata).HasColumnType("jsonb");

            // Core indexes
            e.HasIndex(x => x.PersonId);
            e.HasIndex(x => x.PersonalityId);
            e.HasIndex(x => x.AnalysisRunId);
            e.HasIndex(x => x.AnalyzedDataId);

            // Signal/target (Layer 0)
            e.HasIndex(x => x.SignalId);
            e.HasIndex(x => x.SubjectReceptorId);
            e.HasIndex(x => x.TargetSignalId);
            e.HasIndex(x => x.TargetReceptorId);

            // Operator/region/temporal (Layer 2)
            e.HasIndex(x => x.Operator);
            e.HasIndex(x => x.RegionId);
            e.HasIndex(x => x.Temporal);

            // Gate/lifecycle (Layer 3-4)
            e.HasIndex(x => x.GateInstanceId);
            e.HasIndex(x => x.LifecycleStage);

            // Pathway/circuit (Layer 5-6)
            e.HasIndex(x => x.PathwayId);
            e.HasIndex(x => x.CircuitId);

            // Failure/confidence/context (Layer 7-8)
            e.HasIndex(x => x.FailureMode);
            e.HasIndex(x => x.Confidence);
            e.HasIndex(x => x.Context);
            e.HasIndex(x => x.SubjectDoseRange);
        });

        // ═══════════════════════════════════════════
        // TAGS
        // ═══════════════════════════════════════════

        modelBuilder.Entity<TagEntity>(e =>
        {
            e.ToTable("tag");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Key).IsUnique();
            e.HasIndex(x => x.TagType);
            e.HasIndex(x => x.DomainId);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<EntityTagEntity>(e =>
        {
            e.ToTable("entity_tag");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TagId, x.EntityType, x.EntityId }).IsUnique();
            e.HasIndex(x => x.TagId);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.Severity);
        });

        // ═══════════════════════════════════════════
        // TRAJECTORIES
        // ═══════════════════════════════════════════

        modelBuilder.Entity<TrajectoryEntity>(e =>
        {
            e.ToTable("trajectory");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PersonId);
            e.HasIndex(x => x.PersonalityId);
            e.HasIndex(x => x.DomainId);
            e.HasIndex(x => x.CircuitId);
            e.HasIndex(x => x.TrajectoryType);
            e.HasIndex(x => x.Status);
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<TrajectoryPhaseEntity>(e =>
        {
            e.ToTable("trajectory_phase");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TrajectoryId, x.PhaseNumber }).IsUnique();
            e.HasIndex(x => x.TrajectoryId);
            e.HasIndex(x => x.CircuitPhaseId);
            e.Property(x => x.StateEmbedding).HasColumnType("vector(1536)");
            e.Property(x => x.Metadata).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // ACTIVE LOOPS
        // ═══════════════════════════════════════════

        modelBuilder.Entity<ActiveLoopEntity>(e =>
        {
            e.ToTable("active_loop");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.PersonId);
            e.HasIndex(x => x.PersonalityId);
            e.HasIndex(x => x.DomainId);
            e.HasIndex(x => x.PathwayId);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.LoopType);
            e.HasIndex(x => x.FailureMode);
            e.Property(x => x.Embedding).HasColumnType("vector(1536)");
            e.Property(x => x.Metadata).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // PROFILE SNAPSHOTS
        // ═══════════════════════════════════════════

        modelBuilder.Entity<ProfileSnapshotEntity>(e =>
        {
            e.ToTable("profile_snapshot");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PersonalityId, x.SignalId }).IsUnique();
            e.HasIndex(x => x.PersonId);
            e.HasIndex(x => x.PersonalityId);
            e.HasIndex(x => x.SignalId);
            e.HasIndex(x => x.Trend);
            e.HasIndex(x => x.LatestFailureMode);
            e.Property(x => x.Metadata).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // EMBEDDING CACHE
        // ═══════════════════════════════════════════

        modelBuilder.Entity<EmbeddingCacheEntity>(e =>
        {
            e.ToTable("embedding_cache");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.CacheType, x.LookupKey }).IsUnique();
            e.HasIndex(x => x.CacheType);
            e.HasIndex(x => x.DomainId);
            e.HasIndex(x => x.LookupKey);
            e.Property(x => x.Embedding).HasColumnType("vector(1536)");
            e.Property(x => x.Metadata).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // AGENT SYSTEM
        // ═══════════════════════════════════════════

        modelBuilder.Entity<AgentTemplateEntity>(e =>
        {
            e.ToTable("agent_template");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Category, x.GroupName, x.Name }).IsUnique();
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<AgentGroupEntity>(e =>
        {
            e.ToTable("agent_group");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("uuid_generate_v4()");
            e.HasMany(x => x.Agents).WithOne().HasForeignKey(a => a.GroupId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AgentEntity>(e =>
        {
            e.ToTable("agent");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.GroupId, x.Name }).IsUnique();
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // PIPELINE
        // ═══════════════════════════════════════════

        modelBuilder.Entity<PipelineEntity>(e =>
        {
            e.ToTable("pipeline");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PersonId, x.Name }).IsUnique();
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<LayerEntity>(e =>
        {
            e.ToTable("layer");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PipelineId, x.SortOrder }).IsUnique();
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        // ═══════════════════════════════════════════
        // QUESTIONNAIRE
        // ═══════════════════════════════════════════

        modelBuilder.Entity<QuestionnaireItemEntity>(e =>
        {
            e.ToTable("questionnaire_item");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SortOrder, x.Label }).IsUnique();
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<QuestionnaireEntity>(e =>
        {
            e.ToTable("questionnaire");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("uuid_generate_v4()");
            e.HasIndex(x => x.Token).IsUnique();
            e.Property(x => x.Config).HasColumnType("jsonb");
        });

        modelBuilder.Entity<QuestionnaireAnswerEntity>(e =>
        {
            e.ToTable("questionnaire_answer");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.QuestionnaireId, x.ItemId }).IsUnique();
        });

        // ═══════════════════════════════════════════
        // RELATIONSHIP
        // ═══════════════════════════════════════════

        modelBuilder.Entity<RelationshipTypeEntity>(e =>
        {
            e.ToTable("relationship_type");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Config).HasColumnType("jsonb");
        });
    }
}
