# Repository Layer Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the full Repository layer (entities, DbContext, repositories) for BioChain v5.0's 9 biochem + 5 app tables.

**Architecture:** Generic `BaseRepository<T>` for biochem tables (CRUD + vector search). Standalone repos for app-layer tables. All repos take `IDbContextFactory<PersonalityDbContext>`, return materialized results. DbContext uses Fluent API with snake_case naming convention.

**Tech Stack:** EF Core 10, Npgsql, Pgvector.EntityFrameworkCore, PostgreSQL with pgvector extension.

---

### Task 1: Biochem Entity Classes

**Files:**
- Create: `src/Libraries/BioChain.Repository/Entities/BaseEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/PersonEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/DataEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/SignalEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/ReceptorEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/TransporterEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/GateEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/LimiterEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/InterfaceEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/ProtocolEntity.cs`

**Step 1: Create BaseEntity**

```csharp
// BaseEntity.cs
using Pgvector;
namespace BioChain.Repository.Entities;

public abstract class BaseEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public Vector? Embedding { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}
```

**Step 2: Create PersonEntity**

PersonEntity does NOT extend BaseEntity — Guid PK, no PersonId FK, has OwnerId.

```csharp
// PersonEntity.cs
using Pgvector;
namespace BioChain.Repository.Entities;

public class PersonEntity
{
    public Guid Id { get; set; }
    public string OwnerId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Data { get; set; } = "{}";
    public Vector? Embedding { get; set; }
    public DateTime CreatedOnUtc { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
}
```

**Step 3: Create DataEntity**

DataEntity extends BaseEntity but overrides — no UpdatedOnUtc (append-only). Add Kind, SourceText, Formula, Analyzed, Content.

```csharp
// DataEntity.cs
using Pgvector;
namespace BioChain.Repository.Entities;

public class DataEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string Kind { get; set; } = "";
    public string? SourceText { get; set; }
    public string? Formula { get; set; }
    public bool Analyzed { get; set; }
    public string Content { get; set; } = "{}";
    public Vector? Embedding { get; set; }
    public DateTime CreatedOnUtc { get; set; }
}
```

**Step 4: Create SignalEntity**

```csharp
// SignalEntity.cs
namespace BioChain.Repository.Entities;

public class SignalEntity : BaseEntity
{
    public string Type { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Region { get; set; }
    public string State { get; set; } = "≈";
    public string Baseline { get; set; } = "≈";
    public string? TauMin { get; set; }
    public string? TauMax { get; set; }
}
```

**Step 5: Create ReceptorEntity**

```csharp
// ReceptorEntity.cs
namespace BioChain.Repository.Entities;

public class ReceptorEntity : BaseEntity
{
    public int SignalId { get; set; }
    public string Code { get; set; } = "";
    public string? Subtype { get; set; }
    public string State { get; set; } = "active";
}
```

**Step 6: Create TransporterEntity**

```csharp
// TransporterEntity.cs
namespace BioChain.Repository.Entities;

public class TransporterEntity : BaseEntity
{
    public int SignalId { get; set; }
    public string Code { get; set; } = "";
    public string State { get; set; } = "active";
    public string Clearance { get; set; } = "≈";
}
```

**Step 7: Create GateEntity**

```csharp
// GateEntity.cs
namespace BioChain.Repository.Entities;

public class GateEntity : BaseEntity
{
    public string Code { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Threshold { get; set; }
    public string? Expression { get; set; }
    public int? ParentId { get; set; }
    public string[] History { get; set; } = [];
    public bool Latched { get; set; }
}
```

**Step 8: Create LimiterEntity**

```csharp
// LimiterEntity.cs
namespace BioChain.Repository.Entities;

public class LimiterEntity : BaseEntity
{
    public int? TargetId { get; set; }
    public string Code { get; set; } = "";
    public string? Reaction { get; set; }
    public bool RateLimiting { get; set; }
    public string Activity { get; set; } = "≈";
}
```

**Step 9: Create InterfaceEntity**

```csharp
// InterfaceEntity.cs
namespace BioChain.Repository.Entities;

public class InterfaceEntity : BaseEntity
{
    public string Code { get; set; } = "";
    public string SourceRegion { get; set; } = "";
    public string TargetRegion { get; set; } = "";
    public string? Pathway { get; set; }
    public bool Active { get; set; } = true;
}
```

**Step 10: Create ProtocolEntity**

ProtocolEntity extends BaseEntity. PersonId is nullable (global textbook protocols). Many nullable FKs.

```csharp
// ProtocolEntity.cs
namespace BioChain.Repository.Entities;

public class ProtocolEntity : BaseEntity
{
    public new Guid? PersonId { get; set; }
    public string Formula { get; set; } = "";
    public int? DataId { get; set; }
    public int? SignalSourceId { get; set; }
    public int? SignalTargetId { get; set; }
    public int? ReceptorId { get; set; }
    public int? TransporterId { get; set; }
    public int? GateId { get; set; }
    public int? LimiterId { get; set; }
    public int? InterfaceId { get; set; }
}
```

**Step 11: Build to verify entities compile**

Run: `dotnet build src/Libraries/BioChain.Repository/BioChain.Repository.csproj`
Expected: Build succeeds (DbContext will have errors — that's Task 3)

**Step 12: Commit**

```bash
git add src/Libraries/BioChain.Repository/Entities/
git commit -m "feat: add biochem entity classes for BioChain v5.0 schema"
```

---

### Task 2: App-Layer Entity Classes

**Files:**
- Create: `src/Libraries/BioChain.Repository/Entities/PersonShareEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/UserRoleEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/QuestionnaireItemEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/QuestionnaireEntity.cs`
- Create: `src/Libraries/BioChain.Repository/Entities/QuestionnaireAnswerEntity.cs`

**Step 1: Create PersonShareEntity**

```csharp
// PersonShareEntity.cs
namespace BioChain.Repository.Entities;

public class PersonShareEntity
{
    public int Id { get; set; }
    public Guid PersonId { get; set; }
    public string SharedWithEmail { get; set; } = "";
    public string? SharedWithUserId { get; set; }
    public string SharedByUserId { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
```

**Step 2: Create UserRoleEntity**

```csharp
// UserRoleEntity.cs
namespace BioChain.Repository.Entities;

public class UserRoleEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string? Email { get; set; }
    public string Role { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```

**Step 3: Create QuestionnaireItemEntity**

```csharp
// QuestionnaireItemEntity.cs
namespace BioChain.Repository.Entities;

public class QuestionnaireItemEntity
{
    public int Id { get; set; }
    public int SortOrder { get; set; }
    public string Scenario { get; set; } = "";
    public char Label { get; set; }
    public string OptionText { get; set; } = "";
    public string PrimarySignal { get; set; } = "";
    public string? SecondarySignal { get; set; }
    public bool IsInverted { get; set; }
    public string Data { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
}
```

**Step 4: Create QuestionnaireEntity**

```csharp
// QuestionnaireEntity.cs
namespace BioChain.Repository.Entities;

public class QuestionnaireEntity
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string Token { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string Data { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
```

**Step 5: Create QuestionnaireAnswerEntity**

```csharp
// QuestionnaireAnswerEntity.cs
namespace BioChain.Repository.Entities;

public class QuestionnaireAnswerEntity
{
    public int Id { get; set; }
    public Guid QuestionnaireId { get; set; }
    public int ItemId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

**Step 6: Commit**

```bash
git add src/Libraries/BioChain.Repository/Entities/
git commit -m "feat: add app-layer entity classes"
```

---

### Task 3: Rewrite DbContext

**Files:**
- Modify: `src/Libraries/BioChain.Repository/Data/PersonalityDbContext.cs`

Replace the entire file. 14 DbSets. Fluent API matching biochain_init.sql + init_core.sql exactly.

**Step 1: Rewrite PersonalityDbContext**

Key points:
- 14 DbSets: 9 biochem (Person, Data, Signal, Receptor, Transporter, Gate, Limiter, Interface, Protocol) + 5 app
- Snake_case naming convention handles column names automatically (already in csproj: `EFCore.NamingConventions`)
- Explicit `.ToTable()` for each entity
- `.HasColumnType("jsonb")` for Data/Content/State JSONB columns
- `.HasColumnType("vector(1536)")` for Embedding columns
- FK relationships with `OnDelete(DeleteBehavior.Cascade)`
- Gate self-referencing FK (parent_id) with `OnDelete(DeleteBehavior.Restrict)`
- Protocol has all nullable FKs — no cascade (components shouldn't delete protocols)
- `string[]` History on Gate maps to `text[]` in PostgreSQL

```csharp
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;

namespace BioChain.Repository;

public class PersonalityDbContext(DbContextOptions<PersonalityDbContext> options) : DbContext(options)
{
    // Biochem domain
    public DbSet<PersonEntity> Persons => Set<PersonEntity>();
    public DbSet<DataEntity> Data => Set<DataEntity>();
    public DbSet<SignalEntity> Signals => Set<SignalEntity>();
    public DbSet<ReceptorEntity> Receptors => Set<ReceptorEntity>();
    public DbSet<TransporterEntity> Transporters => Set<TransporterEntity>();
    public DbSet<GateEntity> Gates => Set<GateEntity>();
    public DbSet<LimiterEntity> Limiters => Set<LimiterEntity>();
    public DbSet<InterfaceEntity> Interfaces => Set<InterfaceEntity>();
    public DbSet<ProtocolEntity> Protocols => Set<ProtocolEntity>();

    // App domain
    public DbSet<PersonShareEntity> PersonShares => Set<PersonShareEntity>();
    public DbSet<UserRoleEntity> UserRoles => Set<UserRoleEntity>();
    public DbSet<QuestionnaireItemEntity> QuestionnaireItems => Set<QuestionnaireItemEntity>();
    public DbSet<QuestionnaireEntity> Questionnaires => Set<QuestionnaireEntity>();
    public DbSet<QuestionnaireAnswerEntity> QuestionnaireAnswers => Set<QuestionnaireAnswerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Table names
        modelBuilder.Entity<PersonEntity>().ToTable("person");
        modelBuilder.Entity<DataEntity>().ToTable("data");
        modelBuilder.Entity<SignalEntity>().ToTable("signal");
        modelBuilder.Entity<ReceptorEntity>().ToTable("receptor");
        modelBuilder.Entity<TransporterEntity>().ToTable("transporter");
        modelBuilder.Entity<GateEntity>().ToTable("gate");
        modelBuilder.Entity<LimiterEntity>().ToTable("limiter");
        modelBuilder.Entity<InterfaceEntity>().ToTable("interface");
        modelBuilder.Entity<ProtocolEntity>().ToTable("protocol");
        modelBuilder.Entity<PersonShareEntity>().ToTable("person_share");
        modelBuilder.Entity<UserRoleEntity>().ToTable("user_role");
        modelBuilder.Entity<QuestionnaireItemEntity>().ToTable("questionnaire_item");
        modelBuilder.Entity<QuestionnaireEntity>().ToTable("questionnaire");
        modelBuilder.Entity<QuestionnaireAnswerEntity>().ToTable("questionnaire_answer");

        // Person
        modelBuilder.Entity<PersonEntity>(e =>
        {
            e.Property(p => p.Data).HasColumnType("jsonb");
            e.Property(p => p.Embedding).HasColumnType("vector(1536)");
        });

        // Data
        modelBuilder.Entity<DataEntity>(e =>
        {
            e.Property(d => d.Content).HasColumnType("jsonb");
            e.Property(d => d.Embedding).HasColumnType("vector(1536)");
            e.HasOne<PersonEntity>().WithMany()
                .HasForeignKey(d => d.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        // Signal
        modelBuilder.Entity<SignalEntity>(e =>
        {
            e.Property(s => s.Embedding).HasColumnType("vector(1536)");
            e.HasOne<PersonEntity>().WithMany()
                .HasForeignKey(s => s.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        // Receptor
        modelBuilder.Entity<ReceptorEntity>(e =>
        {
            e.Property(r => r.Embedding).HasColumnType("vector(1536)");
            e.HasOne<PersonEntity>().WithMany()
                .HasForeignKey(r => r.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<SignalEntity>().WithMany()
                .HasForeignKey(r => r.SignalId).OnDelete(DeleteBehavior.Cascade);
        });

        // Transporter
        modelBuilder.Entity<TransporterEntity>(e =>
        {
            e.Property(t => t.Embedding).HasColumnType("vector(1536)");
            e.HasOne<PersonEntity>().WithMany()
                .HasForeignKey(t => t.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<SignalEntity>().WithMany()
                .HasForeignKey(t => t.SignalId).OnDelete(DeleteBehavior.Cascade);
        });

        // Gate
        modelBuilder.Entity<GateEntity>(e =>
        {
            e.Property(g => g.Embedding).HasColumnType("vector(1536)");
            e.HasOne<PersonEntity>().WithMany()
                .HasForeignKey(g => g.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<GateEntity>().WithMany()
                .HasForeignKey(g => g.ParentId).OnDelete(DeleteBehavior.Restrict);
        });

        // Limiter
        modelBuilder.Entity<LimiterEntity>(e =>
        {
            e.Property(l => l.Embedding).HasColumnType("vector(1536)");
            e.HasOne<PersonEntity>().WithMany()
                .HasForeignKey(l => l.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<SignalEntity>().WithMany()
                .HasForeignKey(l => l.TargetId).OnDelete(DeleteBehavior.SetNull);
        });

        // Interface
        modelBuilder.Entity<InterfaceEntity>(e =>
        {
            e.Property(i => i.Embedding).HasColumnType("vector(1536)");
            e.HasOne<PersonEntity>().WithMany()
                .HasForeignKey(i => i.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        // Protocol
        modelBuilder.Entity<ProtocolEntity>(e =>
        {
            e.Property(p => p.Embedding).HasColumnType("vector(1536)");
            e.HasOne<PersonEntity>().WithMany()
                .HasForeignKey(p => p.PersonId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<DataEntity>().WithMany()
                .HasForeignKey(p => p.DataId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<SignalEntity>().WithMany()
                .HasForeignKey(p => p.SignalSourceId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<SignalEntity>().WithMany()
                .HasForeignKey(p => p.SignalTargetId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<ReceptorEntity>().WithMany()
                .HasForeignKey(p => p.ReceptorId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<TransporterEntity>().WithMany()
                .HasForeignKey(p => p.TransporterId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<GateEntity>().WithMany()
                .HasForeignKey(p => p.GateId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<LimiterEntity>().WithMany()
                .HasForeignKey(p => p.LimiterId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<InterfaceEntity>().WithMany()
                .HasForeignKey(p => p.InterfaceId).OnDelete(DeleteBehavior.SetNull);
        });

        // PersonShare
        modelBuilder.Entity<PersonShareEntity>(e =>
        {
            e.HasOne<PersonEntity>().WithMany()
                .HasForeignKey(s => s.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        // Questionnaire chain
        modelBuilder.Entity<QuestionnaireItemEntity>(e =>
        {
            e.Property(i => i.Data).HasColumnType("jsonb");
        });

        modelBuilder.Entity<QuestionnaireEntity>(e =>
        {
            e.Property(q => q.Data).HasColumnType("jsonb");
            e.HasOne<PersonEntity>().WithMany()
                .HasForeignKey(q => q.PersonId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<QuestionnaireAnswerEntity>(e =>
        {
            e.HasOne<QuestionnaireEntity>().WithMany()
                .HasForeignKey(a => a.QuestionnaireId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<QuestionnaireItemEntity>().WithMany()
                .HasForeignKey(a => a.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
```

**Step 2: Build to verify DbContext compiles**

Run: `dotnet build src/Libraries/BioChain.Repository/BioChain.Repository.csproj`
Expected: PASS

**Step 3: Commit**

```bash
git add src/Libraries/BioChain.Repository/Data/PersonalityDbContext.cs
git commit -m "feat: rewrite DbContext for BioChain v5.0 schema"
```

---

### Task 4: BaseRepository

**Files:**
- Create: `src/Libraries/BioChain.Repository/Repositories/BaseRepository.cs`

**Step 1: Create BaseRepository<T>**

Takes `IDbContextFactory<PersonalityDbContext>`. All methods create+dispose their own context. `FindSimilarAsync` uses raw SQL with pgvector `<=>` operator.

```csharp
using BioChain.Repository.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector;

namespace BioChain.Repository.Repositories;

public abstract class BaseRepository<T>(IDbContextFactory<PersonalityDbContext> dbFactory)
    where T : BaseEntity
{
    protected async Task<PersonalityDbContext> CreateDbAsync()
        => await dbFactory.CreateDbContextAsync();

    public async Task<T?> GetByIdAsync(int id)
    {
        await using var db = await CreateDbAsync();
        return await db.Set<T>().AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<List<T>> GetByPersonAsync(Guid personId)
    {
        await using var db = await CreateDbAsync();
        return await db.Set<T>().AsNoTracking()
            .Where(e => e.PersonId == personId).ToListAsync();
    }

    public async Task<T> CreateAsync(T entity)
    {
        await using var db = await CreateDbAsync();
        entity.CreatedOnUtc = DateTime.UtcNow;
        entity.UpdatedOnUtc = DateTime.UtcNow;
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    public async Task UpdateAsync(T entity)
    {
        await using var db = await CreateDbAsync();
        entity.UpdatedOnUtc = DateTime.UtcNow;
        db.Set<T>().Update(entity);
        await db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        await using var db = await CreateDbAsync();
        var entity = await db.Set<T>().FindAsync(id);
        if (entity is not null)
        {
            db.Set<T>().Remove(entity);
            await db.SaveChangesAsync();
        }
    }

    public async Task<List<T>> FindSimilarAsync(Guid personId, Vector embedding, int limit = 10)
    {
        await using var db = await CreateDbAsync();
        return await db.Set<T>().AsNoTracking()
            .Where(e => e.PersonId == personId && e.Embedding != null)
            .OrderBy(e => e.Embedding!.CosineDistance(embedding))
            .Take(limit)
            .ToListAsync();
    }
}
```

**Step 2: Build**

Run: `dotnet build src/Libraries/BioChain.Repository/BioChain.Repository.csproj`
Expected: PASS

**Step 3: Commit**

```bash
git add src/Libraries/BioChain.Repository/Repositories/BaseRepository.cs
git commit -m "feat: add generic BaseRepository with CRUD and vector search"
```

---

### Task 5: Biochem Repositories (Signal, Receptor, Transporter, Gate, Limiter, Interface, Protocol)

**Files:**
- Create: `src/Libraries/BioChain.Repository/Repositories/SignalRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/ReceptorRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/TransporterRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/GateRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/LimiterRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/InterfaceRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/ProtocolRepository.cs`

**Step 1: Create all 7 repos**

Each extends `BaseRepository<T>` and adds specialized queries per the design doc.

SignalRepository: GetByTypeAsync, GetByCodeAsync, GetByRegionAsync, GetByStateAsync, GetOrCreateAsync
ReceptorRepository: GetBySignalAsync, GetByStateAsync, GetByCodeAsync
TransporterRepository: GetBySignalAsync, GetByStateAsync
GateRepository: GetLatchedAsync, GetByTypeAsync, GetChildrenAsync
LimiterRepository: GetBottlenecksAsync, GetByTargetAsync
InterfaceRepository: GetActiveAsync, GetByRegionAsync, GetByPathwayAsync
ProtocolRepository: GetBySourceSignalAsync, GetByPersonOrGlobalAsync

All queries scoped to personId, return materialized List<T> or T?.

**Step 2: Build**

Run: `dotnet build src/Libraries/BioChain.Repository/BioChain.Repository.csproj`
Expected: PASS

**Step 3: Commit**

```bash
git add src/Libraries/BioChain.Repository/Repositories/
git commit -m "feat: add 7 biochem repositories with specialized queries"
```

---

### Task 6: Standalone Repositories (Person, Data, App-Layer)

**Files:**
- Create: `src/Libraries/BioChain.Repository/Repositories/PersonRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/DataRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/PersonShareRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/UserRoleRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/QuestionnaireItemRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/QuestionnaireRepository.cs`
- Create: `src/Libraries/BioChain.Repository/Repositories/QuestionnaireAnswerRepository.cs`

**Step 1: Create all 7 repos**

These don't extend BaseRepository — they implement CRUD + specialized queries directly.

PersonRepository: GetByIdAsync(Guid), GetByOwnerAsync, GetByOwnerAndNameAsync, CreateAsync, UpdateAsync, DeleteAsync, FindSimilarAsync (no person scope)
DataRepository: GetByIdAsync(int), GetByPersonAsync, CreateAsync, GetUnanalyzedAsync, GetByKindAsync, GetByTimeRangeAsync, MarkAnalyzedAsync, FindSimilarAsync
PersonShareRepository: GetByPersonAsync, GetBySharedWithAsync (email or userId), CreateAsync, DeleteAsync
UserRoleRepository: GetByUserAsync, GetActiveByUserAsync, CreateAsync, UpdateAsync
QuestionnaireItemRepository: GetAllOrderedAsync, GetByIdAsync
QuestionnaireRepository: GetByPersonAsync, GetByTokenAsync, GetPendingAsync, CreateAsync, UpdateAsync
QuestionnaireAnswerRepository: GetByQuestionnaireAsync, CreateAsync

**Step 2: Build**

Run: `dotnet build src/Libraries/BioChain.Repository/BioChain.Repository.csproj`
Expected: PASS

**Step 3: Commit**

```bash
git add src/Libraries/BioChain.Repository/Repositories/
git commit -m "feat: add Person, Data, and app-layer repositories"
```

---

### Task 7: Update Program.cs DI Registrations

**Files:**
- Modify: `src/BioChain.Server/Program.cs`

**Step 1: Replace repository registrations**

In `RegisterAll()`, replace old repo registrations (lines 106-113) with the 14 new ones. Remove references to deleted services (EventService, ProfileService, TraceService, GraphQueryService, AnalyzeService, AnalysisQueueService, AnalysisBackgroundWorker, EmbeddingService). Keep PersonService placeholder if it exists, otherwise comment out service registrations that don't exist yet.

Old:
```csharp
services.AddSingleton<PersonRepository>();
services.AddSingleton<EventRepository>();
services.AddSingleton<ProfileRepository>();
services.AddSingleton<TraceRepository>();
services.AddSingleton<GraphRepository>();
services.AddSingleton<QuestionnaireRepository>();
services.AddSingleton<PersonShareRepository>();
services.AddSingleton<UserRoleRepository>();
```

New:
```csharp
// Repositories — biochem domain
services.AddSingleton<SignalRepository>();
services.AddSingleton<ReceptorRepository>();
services.AddSingleton<TransporterRepository>();
services.AddSingleton<GateRepository>();
services.AddSingleton<LimiterRepository>();
services.AddInterfaceRepository>();
services.AddSingleton<ProtocolRepository>();

// Repositories — standalone
services.AddSingleton<PersonRepository>();
services.AddSingleton<DataRepository>();

// Repositories — app layer
services.AddSingleton<PersonShareRepository>();
services.AddSingleton<UserRoleRepository>();
services.AddSingleton<QuestionnaireItemRepository>();
services.AddSingleton<QuestionnaireRepository>();
services.AddSingleton<QuestionnaireAnswerRepository>();
```

Also update `using` statements and comment out / remove references to deleted services + APIs.

**Step 2: Build the whole solution**

Run: `dotnet build BioChain.sln`
Expected: Server project may have errors from deleted API/Service files. Fix by commenting out the missing references. The Repository project itself should build clean.

**Step 3: Commit**

```bash
git add src/BioChain.Server/Program.cs
git commit -m "feat: wire 14 new repositories in DI"
```

---

### Task 8: Build Verification

**Step 1: Clean build of Repository project**

Run: `dotnet build src/Libraries/BioChain.Repository/BioChain.Repository.csproj`
Expected: PASS with 0 warnings related to our code

**Step 2: Full solution build check**

Run: `dotnet build BioChain.sln 2>&1 | head -50`
Note any remaining errors from other projects (expected — Service/Server reference deleted types). Repository project must be clean.

**Step 3: Final commit if any fixups needed**

```bash
git add -A && git commit -m "fix: resolve build issues in repository layer"
```
