using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace BioChain.Repository.Data;

public class BioChainDbContext(DbContextOptions<BioChainDbContext> options) : DbContext(options)
{
    public DbSet<PersonEntity> Persons => Set<PersonEntity>();
    public DbSet<DataEntity> Events => Set<DataEntity>();
    public DbSet<SignalEntity> Signals => Set<SignalEntity>();
    public DbSet<ReceptorEntity> Receptors => Set<ReceptorEntity>();
    public DbSet<TransporterEntity> Transporters => Set<TransporterEntity>();
    public DbSet<GateEntity> Gates => Set<GateEntity>();
    public DbSet<LimiterEntity> Limiters => Set<LimiterEntity>();
    public DbSet<InterfaceEntity> Interfaces => Set<InterfaceEntity>();
    public DbSet<ProtocolEntity> Protocols => Set<ProtocolEntity>();
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

        ConfigurePerson(modelBuilder);
        ConfigureData(modelBuilder);
        ConfigureSignal(modelBuilder);
        ConfigureReceptor(modelBuilder);
        ConfigureTransporter(modelBuilder);
        ConfigureGate(modelBuilder);
        ConfigureLimiter(modelBuilder);
        ConfigureInterface(modelBuilder);
        ConfigureProtocol(modelBuilder);
        ConfigurePersonShare(modelBuilder);
        ConfigureUserRole(modelBuilder);
        ConfigureQuestionnaireItem(modelBuilder);
        ConfigureQuestionnaire(modelBuilder);
        ConfigureQuestionnaireAnswer(modelBuilder);
    }

    private static void ConfigurePerson(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PersonEntity>(entity =>
        {
            entity.ToTable("person");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.OwnerId).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Meta).HasColumnName("data").HasColumnType("jsonb");
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.OwnerId);
            entity.HasIndex(e => new { e.OwnerId, e.Name }).IsUnique();
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");

            entity.HasMany(e => e.Events)
                .WithOne(d => d.Person)
                .HasForeignKey(d => d.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Signals)
                .WithOne(s => s.Person)
                .HasForeignKey(s => s.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Receptors)
                .WithOne(r => r.Person)
                .HasForeignKey(r => r.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Transporters)
                .WithOne(t => t.Person)
                .HasForeignKey(t => t.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Gates)
                .WithOne(g => g.Person)
                .HasForeignKey(g => g.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Limiters)
                .WithOne(l => l.Person)
                .HasForeignKey(l => l.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Interfaces)
                .WithOne(i => i.Person)
                .HasForeignKey(i => i.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Shares)
                .WithOne(s => s.Person)
                .HasForeignKey(s => s.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Questionnaires)
                .WithOne(q => q.Person)
                .HasForeignKey(q => q.PersonId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DataEntity>(entity =>
        {
            entity.ToTable("data");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Kind).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Content).HasColumnType("jsonb");
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
            entity.Property(e => e.Analyzed).HasDefaultValue(false);
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.PersonId);
            entity.HasIndex(e => e.Kind);
            entity.HasIndex(e => e.Analyzed);
            entity.HasIndex(e => e.CreatedOnUtc);
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
        });
    }

    private static void ConfigureSignal(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SignalEntity>(entity =>
        {
            entity.ToTable("signal");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Type).HasMaxLength(5).IsRequired();
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Region).HasMaxLength(20);
            entity.Property(e => e.State).HasMaxLength(5).IsRequired();
            entity.Property(e => e.Baseline).HasMaxLength(5);
            entity.Property(e => e.TauMin).HasMaxLength(10);
            entity.Property(e => e.TauMax).HasMaxLength(10);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedOnUtc).HasDefaultValueSql("now()");

            // Conditional unique: (person_id, code, region) when region not null, else (person_id, code)
            entity.HasIndex(e => new { e.PersonId, e.Code, e.Region }).IsUnique().HasFilter("region IS NOT NULL");
            entity.HasIndex(e => new { e.PersonId, e.Code }).IsUnique().HasFilter("region IS NULL");
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Code);
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
        });
    }

    private static void ConfigureReceptor(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReceptorEntity>(entity =>
        {
            entity.ToTable("receptor");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Subtype).HasMaxLength(20);
            entity.Property(e => e.State).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");

            entity.HasIndex(e => new { e.PersonId, e.Code }).IsUnique();
            entity.HasIndex(e => e.SignalId);
            entity.HasIndex(e => e.State);
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");

            entity.HasOne(e => e.Signal)
                .WithMany(s => s.Receptors)
                .HasForeignKey(e => e.SignalId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureTransporter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TransporterEntity>(entity =>
        {
            entity.ToTable("transporter");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.State).HasMaxLength(10).IsRequired();
            entity.Property(e => e.Clearance).HasMaxLength(5);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");

            entity.HasIndex(e => new { e.PersonId, e.Code }).IsUnique();
            entity.HasIndex(e => e.SignalId);
            entity.HasIndex(e => e.State);
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");

            entity.HasOne(e => e.Signal)
                .WithMany(s => s.Transporters)
                .HasForeignKey(e => e.SignalId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureGate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GateEntity>(entity =>
        {
            entity.ToTable("gate");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Code).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Type).HasMaxLength(15).IsRequired();
            entity.Property(e => e.Threshold).HasMaxLength(5);
            entity.Property(e => e.Latched).HasDefaultValue(false);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");

            entity.HasIndex(e => new { e.PersonId, e.Code }).IsUnique();
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.ParentId);
            entity.HasIndex(e => e.Latched);
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");

            entity.HasOne(e => e.Parent)
                .WithMany(g => g.Children)
                .HasForeignKey(e => e.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLimiter(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LimiterEntity>(entity =>
        {
            entity.ToTable("limiter");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Code).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Activity).HasMaxLength(5);
            entity.Property(e => e.RateLimiting).HasDefaultValue(false);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");

            entity.HasIndex(e => new { e.PersonId, e.Code }).IsUnique();
            entity.HasIndex(e => e.TargetId);
            entity.HasIndex(e => e.RateLimiting);
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");

            entity.HasOne(e => e.Target)
                .WithMany(s => s.Limiters)
                .HasForeignKey(e => e.TargetId)
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
            entity.Property(e => e.Code).HasMaxLength(30).IsRequired();
            entity.Property(e => e.SourceRegion).HasMaxLength(20);
            entity.Property(e => e.TargetRegion).HasMaxLength(20);
            entity.Property(e => e.Pathway).HasMaxLength(50);
            entity.Property(e => e.Active).HasDefaultValue(true);
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");

            entity.HasIndex(e => new { e.PersonId, e.Code }).IsUnique();
            entity.HasIndex(e => e.SourceRegion);
            entity.HasIndex(e => e.TargetRegion);
            entity.HasIndex(e => e.Pathway);
            entity.HasIndex(e => e.Active);
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");
        });
    }

    private static void ConfigureProtocol(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProtocolEntity>(entity =>
        {
            entity.ToTable("protocol");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).UseIdentityAlwaysColumn();
            entity.Property(e => e.Formula).IsRequired();
            entity.Property(e => e.Embedding).HasColumnType("vector(1536)");
            entity.Property(e => e.CreatedOnUtc).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedOnUtc).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.PersonId);
            entity.HasIndex(e => e.DataId);
            entity.HasIndex(e => e.SignalSourceId);
            entity.HasIndex(e => e.SignalTargetId);
            entity.HasIndex(e => e.ReceptorId);
            entity.HasIndex(e => e.TransporterId);
            entity.HasIndex(e => e.GateId);
            entity.HasIndex(e => e.LimiterId);
            entity.HasIndex(e => e.InterfaceId);
            entity.HasIndex(e => new { e.SignalSourceId, e.PersonId });
            entity.HasIndex(e => e.Embedding).HasMethod("hnsw").HasOperators("vector_cosine_ops");

            entity.HasOne(e => e.Person)
                .WithMany()
                .HasForeignKey(e => e.PersonId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Data)
                .WithMany()
                .HasForeignKey(e => e.DataId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.SignalSource)
                .WithMany()
                .HasForeignKey(e => e.SignalSourceId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.SignalTarget)
                .WithMany()
                .HasForeignKey(e => e.SignalTargetId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Receptor)
                .WithMany()
                .HasForeignKey(e => e.ReceptorId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Transporter)
                .WithMany()
                .HasForeignKey(e => e.TransporterId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Gate)
                .WithMany()
                .HasForeignKey(e => e.GateId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Limiter)
                .WithMany()
                .HasForeignKey(e => e.LimiterId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.Interface)
                .WithMany()
                .HasForeignKey(e => e.InterfaceId)
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
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(e => new { e.PersonId, e.SharedWithEmail }).IsUnique();
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
            entity.Property(e => e.Token).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Data).HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");

            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasIndex(e => e.PersonId);

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
