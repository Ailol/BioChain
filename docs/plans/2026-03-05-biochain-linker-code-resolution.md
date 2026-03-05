# BioChain Linker: FK to Code-Based Resolution

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Eliminate silent drops in ComponentLinker by storing LLM-emitted codes directly instead of demanding resolved integer FKs at write time. Rename `protocol` table to `analysis` (audit log only, no component FKs).

**Architecture:** Store the code the LLM emits. Resolve later, not at write time. The DSL already provides fully qualified codes (`NT:DA`, `DA.D2`, `DAT`, `CORT[↑↑]@ADR → BDNF[↓]@HPC`). These codes ARE the graph. Edges join on `(subject_id, code)` instead of integer IDs. The `protocol` table becomes `analysis` — a lean audit log of raw LLM output with no component FKs.

**Tech Stack:** C# / .NET 9, EF Core 9, PostgreSQL 17, pgvector

---

## Current State Summary

**Active codebase:** `src/BioChain.Repository/` (NOT `src/Libraries/`)
**Solution:** `BioChain.sln` — projects: Server, Repository, Models, Utils, Service, Kernel, AppHost, ServiceDefaults

**ComponentLinker** (`src/BioChain.Repository/Linking/ComponentLinker.cs`):
- Uses `BioChainDbContext` directly for most entities
- Uses repository interfaces only for: `IGateRepository`, `IModuleRepository`, `IStimuliRepository`
- Has `GetOrCreateSignalAsync` (auto-creates missing signals for FORMULA/FEEDBACK/DYSREG)
- Has `ConnectOrphanedSignalsAsync` band-aid for post-hoc graph repair
- RECEPTOR and TRANSPORT still silently drop when parent signal is missing

**The 2 remaining chokepoints:**
1. **RECEPTOR (line 86-87):** `GetCurrentSignalByCodeAsync` → `if (parent is null) break;`
2. **TRANSPORT (line 203-207):** `MapTransporterToSignal` (7-item static map) → signal lookup → `if (parent is null) break;`

**Already fixed in active code:**
- FORMULA/FEEDBACK/DYSREG use `GetOrCreateSignalAsync` — no drops
- But they still require a DB round-trip to get integer IDs for edge creation

**TauConstants.json:** EXISTS at `src/BioChain.Repository/Data/TauConstants.json` (98 chemicals, PubMed-sourced). Not a gate — unknown codes get `(null, null)`. Correct pattern, keep as-is.

---

## Task 1: Add code-based columns to ReceptorEntity

**Files:**
- Modify: `src/BioChain.Repository/Entities/ReceptorEntity.cs`
- Modify: `src/BioChain.Repository/Data/BioChainDbContext.cs` (ConfigureReceptor section)

**Step 1: Write the failing test**

```csharp
// tests/BioChain.Repository.Tests/Entities/ReceptorEntityTests.cs
using BioChain.Repository.Entities;

namespace BioChain.Repository.Tests.Entities;

public class ReceptorEntityTests
{
    [Fact]
    public void ReceptorEntity_Has_SignalCode_And_SignalType_Properties()
    {
        var receptor = new ReceptorEntity
        {
            Code = "D2",
            SignalCode = "DA",
            SignalType = "NT",
            State = "active",
        };

        Assert.Equal("DA", receptor.SignalCode);
        Assert.Equal("NT", receptor.SignalType);
    }

    [Fact]
    public void ReceptorEntity_SignalId_Is_Nullable()
    {
        var receptor = new ReceptorEntity { Code = "D2", SignalCode = "DA" };
        Assert.Null(receptor.SignalId);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "ReceptorEntityTests" -v minimal`
Expected: FAIL — `SignalCode` and `SignalType` properties don't exist yet

**Step 3: Add properties to ReceptorEntity**

```csharp
// src/BioChain.Repository/Entities/ReceptorEntity.cs
namespace BioChain.Repository.Entities;

public class ReceptorEntity
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public int? SignalId { get; set; }          // ← nullable now
    public string? SignalCode { get; set; }     // ← NEW: "DA", "5HT", etc.
    public string? SignalType { get; set; }     // ← NEW: "NT", "H", "P", etc.
    public string Code { get; set; } = string.Empty;
    public string? Subtype { get; set; }
    public string State { get; set; } = "active";
    public int? ModuleId { get; set; }
    public string? Cause { get; set; }
    public int? ProtocolId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity Subject { get; set; } = null!;
    public SignalEntity? Signal { get; set; }   // ← nullable nav prop
    public ModuleEntity? Module { get; set; }
    public ProtocolEntity? Protocol { get; set; }
}
```

**Step 4: Update EF Core configuration in BioChainDbContext**

In the `ConfigureReceptor` method, add column mappings for the new properties and make `signal_id` nullable. Add index on `(subject_id, signal_code)`.

**Step 5: Run test to verify it passes**

Run: `dotnet test --filter "ReceptorEntityTests" -v minimal`
Expected: PASS

**Step 6: Commit**

```bash
git add src/BioChain.Repository/Entities/ReceptorEntity.cs src/BioChain.Repository/Data/BioChainDbContext.cs tests/
git commit -m "feat: add signal_code/signal_type to ReceptorEntity, make signal_id nullable"
```

---

## Task 2: Add code-based columns to TransporterEntity

**Files:**
- Modify: `src/BioChain.Repository/Entities/TransporterEntity.cs`
- Modify: `src/BioChain.Repository/Data/BioChainDbContext.cs` (ConfigureTransporter section)

**Step 1: Write the failing test**

```csharp
// tests/BioChain.Repository.Tests/Entities/TransporterEntityTests.cs
using BioChain.Repository.Entities;

namespace BioChain.Repository.Tests.Entities;

public class TransporterEntityTests
{
    [Fact]
    public void TransporterEntity_Has_SignalCode_And_SignalType_Properties()
    {
        var transporter = new TransporterEntity
        {
            Code = "DAT",
            SignalCode = "DA",
            SignalType = "NT",
        };

        Assert.Equal("DA", transporter.SignalCode);
        Assert.Equal("NT", transporter.SignalType);
    }

    [Fact]
    public void TransporterEntity_SignalId_Is_Nullable()
    {
        var transporter = new TransporterEntity { Code = "DAT", SignalCode = "DA" };
        Assert.Null(transporter.SignalId);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "TransporterEntityTests" -v minimal`
Expected: FAIL

**Step 3: Add properties to TransporterEntity**

```csharp
// src/BioChain.Repository/Entities/TransporterEntity.cs
namespace BioChain.Repository.Entities;

public class TransporterEntity
{
    public int Id { get; set; }
    public Guid SubjectId { get; set; }
    public int? SignalId { get; set; }          // ← nullable now
    public string? SignalCode { get; set; }     // ← NEW
    public string? SignalType { get; set; }     // ← NEW
    public string Code { get; set; } = string.Empty;
    public string State { get; set; } = "active";
    public string Clearance { get; set; } = "\u2248";
    public int? ModuleId { get; set; }
    public string? Cause { get; set; }
    public int? ProtocolId { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity Subject { get; set; } = null!;
    public SignalEntity? Signal { get; set; }   // ← nullable nav prop
    public ModuleEntity? Module { get; set; }
    public ProtocolEntity? Protocol { get; set; }
}
```

**Step 4: Update EF Core configuration**

Same pattern as Task 1. Add column mappings and index on `(subject_id, signal_code)`.

**Step 5: Run test to verify it passes**

Run: `dotnet test --filter "TransporterEntityTests" -v minimal`
Expected: PASS

**Step 6: Commit**

```bash
git add src/BioChain.Repository/Entities/TransporterEntity.cs src/BioChain.Repository/Data/BioChainDbContext.cs tests/
git commit -m "feat: add signal_code/signal_type to TransporterEntity, make signal_id nullable"
```

---

## Task 3: Add code-based columns to EdgeEntity

**Files:**
- Modify: `src/BioChain.Repository/Entities/EdgeEntity.cs`
- Modify: `src/BioChain.Repository/Data/BioChainDbContext.cs` (ConfigureEdge section)

**Step 1: Write the failing test**

```csharp
// tests/BioChain.Repository.Tests/Entities/EdgeEntityTests.cs
using BioChain.Repository.Entities;

namespace BioChain.Repository.Tests.Entities;

public class EdgeEntityTests
{
    [Fact]
    public void EdgeEntity_Has_Code_Based_Endpoint_Properties()
    {
        var edge = new EdgeEntity
        {
            SourceCode = "DA",
            SourceSignalType = "NT",
            SourceRegion = "VTA",
            TargetCode = "BDNF",
            TargetSignalType = "P",
            TargetRegion = "HPC",
            RelationshipKind = "causal",
            Operator = "\u2192",
            OperatorClass = "causal",
        };

        Assert.Equal("DA", edge.SourceCode);
        Assert.Equal("NT", edge.SourceSignalType);
        Assert.Equal("VTA", edge.SourceRegion);
        Assert.Equal("BDNF", edge.TargetCode);
        Assert.Equal("P", edge.TargetSignalType);
        Assert.Equal("HPC", edge.TargetRegion);
        Assert.Equal("causal", edge.RelationshipKind);
    }

    [Fact]
    public void EdgeEntity_SourceId_And_TargetId_Are_Nullable()
    {
        var edge = new EdgeEntity
        {
            SourceCode = "DA",
            TargetCode = "5HT",
            Operator = "\u2192",
            OperatorClass = "causal",
        };

        Assert.Equal(0, edge.SourceId); // default int, will be nullable after migration
        Assert.Equal(0, edge.TargetId);
    }

    [Fact]
    public void EdgeEntity_Has_Gate_Code_Properties()
    {
        var edge = new EdgeEntity
        {
            SourceCode = "DA",
            TargetCode = "GABA",
            GateCode = "DA@NAc",
            GateType = "threshold",
            GateCondition = "DA@NAc >= \u2191",
            Operator = "\u2192",
            OperatorClass = "causal",
        };

        Assert.Equal("DA@NAc", edge.GateCode);
        Assert.Equal("threshold", edge.GateType);
        Assert.Equal("DA@NAc >= \u2191", edge.GateCondition);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "EdgeEntityTests" -v minimal`
Expected: FAIL

**Step 3: Add properties to EdgeEntity**

```csharp
// src/BioChain.Repository/Entities/EdgeEntity.cs
namespace BioChain.Repository.Entities;

public class EdgeEntity
{
    public int Id { get; set; }
    public Guid? SubjectId { get; set; }

    // Existing ID-based endpoints (nullable during migration)
    public string SourceType { get; set; } = string.Empty;
    public int? SourceId { get; set; }         // ← nullable now
    public string TargetType { get; set; } = string.Empty;
    public int? TargetId { get; set; }         // ← nullable now

    // NEW: Code-based endpoints
    public string? SourceCode { get; set; }
    public string? SourceSignalType { get; set; }
    public string? SourceRegion { get; set; }
    public string? TargetCode { get; set; }
    public string? TargetSignalType { get; set; }
    public string? TargetRegion { get; set; }
    public string? RelationshipKind { get; set; }  // causal | negative_feedback | positive_feedback | dysregulation | parallel | gate_dep | downstream

    // NEW: Gate code-based (instead of just GateId FK)
    public string? GateCode { get; set; }
    public string? GateType { get; set; }
    public string? GateCondition { get; set; }

    // Existing
    public string Operator { get; set; } = string.Empty;
    public string OperatorClass { get; set; } = string.Empty;
    public string? Properties { get; set; }
    public decimal? Gain { get; set; }
    public decimal? NoiseSigma { get; set; }
    public string? TransferFn { get; set; }
    public long? DelayMs { get; set; }
    public decimal? ClampLo { get; set; }
    public decimal? ClampHi { get; set; }
    public int? GateId { get; set; }
    public int? LoopId { get; set; }
    public int? PathwayId { get; set; }
    public string? DysregType { get; set; }
    public int? ModuleId { get; set; }
    public int? ToolId { get; set; }
    public int? ProtocolId { get; set; }
    public bool Active { get; set; } = true;
    public DateTimeOffset CreatedOnUtc { get; set; }

    public SubjectEntity? Subject { get; set; }
    public GateEntity? Gate { get; set; }
    public LoopEntity? Loop { get; set; }
    public PathwayEntity? Pathway { get; set; }
    public ModuleEntity? Module { get; set; }
    public ToolEntity? Tool { get; set; }
    public ProtocolEntity? Protocol { get; set; }
}
```

**Step 4: Update EF Core configuration**

Add column mappings for all new properties. Add indexes:
- `ix_edges_source_code` on `(subject_id, source_code)`
- `ix_edges_target_code` on `(subject_id, target_code)`
- `ix_edges_relationship` on `(subject_id, relationship_kind)`

**Step 5: Run test to verify it passes**

Run: `dotnet test --filter "EdgeEntityTests" -v minimal`
Expected: PASS

**Step 6: Commit**

```bash
git add src/BioChain.Repository/Entities/EdgeEntity.cs src/BioChain.Repository/Data/BioChainDbContext.cs tests/
git commit -m "feat: add code-based endpoint columns to EdgeEntity, make SourceId/TargetId nullable"
```

---

## Task 4: Create EF Core migration

**Files:**
- Create: EF Core migration file (auto-generated)

**Step 1: Generate migration**

Run: `dotnet ef migrations add AddCodeBasedResolution --project src/BioChain.Repository --startup-project src/BioChain.Server`

Verify the migration includes:
- `receptor`: add `signal_code VARCHAR(32)`, `signal_type VARCHAR(8)`, make `signal_id` nullable
- `transporter`: add `signal_code VARCHAR(32)`, `signal_type VARCHAR(8)`, make `signal_id` nullable
- `edge`: add `source_code`, `source_signal_type`, `source_region`, `target_code`, `target_signal_type`, `target_region`, `relationship_kind`, `gate_code`, `gate_type`, `gate_condition`; make `source_id`/`target_id` nullable
- New indexes on code columns

**Step 2: Review migration SQL**

Run: `dotnet ef migrations script --project src/BioChain.Repository --startup-project src/BioChain.Server`
Verify no destructive changes. All new columns are nullable (additive only).

**Step 3: Commit**

```bash
git add src/BioChain.Repository/Migrations/
git commit -m "feat: migration AddCodeBasedResolution — add code columns, make FKs nullable"
```

---

## Task 5: Backfill existing data

**Files:**
- Create: `src/BioChain.Repository/Data/backfill-code-columns.sql`

**Step 1: Write backfill SQL**

```sql
-- Backfill receptors: copy signal code/type from parent signal
UPDATE receptor r
SET signal_code = s.code,
    signal_type = s.type
FROM signal s
WHERE r.signal_id = s.id
  AND r.signal_code IS NULL;

-- Backfill transporters: copy signal code/type from parent signal
UPDATE transporter t
SET signal_code = s.code,
    signal_type = s.type
FROM signal s
WHERE t.signal_id = s.id
  AND t.signal_code IS NULL;

-- Backfill edges: copy source/target codes from signal table
UPDATE edge e
SET source_code = ss.code,
    source_signal_type = ss.type,
    source_region = sr.code,
    target_code = ts.code,
    target_signal_type = ts.type,
    target_region = tr.code,
    relationship_kind = CASE
        WHEN e.operator_class = 'feedback' THEN 'negative_feedback'
        WHEN e.operator_class = 'dysreg' THEN 'dysregulation'
        WHEN e.operator_class = 'causal' THEN 'causal'
        ELSE e.operator_class
    END
FROM signal ss
LEFT JOIN region sr ON ss.region_id = sr.id
JOIN signal ts ON e.target_id = ts.id
LEFT JOIN region tr ON ts.region_id = tr.id
WHERE e.source_id = ss.id
  AND e.source_code IS NULL;

-- Backfill gate info on edges that have gate_id
UPDATE edge e
SET gate_code = g.code,
    gate_type = g.type,
    gate_condition = g.expression
FROM gate g
WHERE e.gate_id = g.id
  AND e.gate_code IS NULL;
```

**Step 2: Verify backfill**

```sql
-- Should return 0 rows after backfill
SELECT count(*) FROM receptor WHERE signal_id IS NOT NULL AND signal_code IS NULL;
SELECT count(*) FROM transporter WHERE signal_id IS NOT NULL AND signal_code IS NULL;
SELECT count(*) FROM edge WHERE source_id IS NOT NULL AND source_code IS NULL;
```

**Step 3: Commit**

```bash
git add src/BioChain.Repository/Data/backfill-code-columns.sql
git commit -m "feat: add backfill script for code-based columns"
```

---

## Task 6: Refactor ComponentLinker — eliminate RECEPTOR chokepoint

**Files:**
- Modify: `src/BioChain.Repository/Linking/ComponentLinker.cs`

**Step 1: Write the failing test**

```csharp
// tests/BioChain.Repository.Tests/Linking/ComponentLinkerReceptorTests.cs
using BioChain.Repository.Linking;
using BioChain.Utils.Parsing;

namespace BioChain.Repository.Tests.Linking;

public class ComponentLinkerReceptorTests
{
    [Fact]
    public async Task Receptor_Stored_Without_Parent_Signal_Lookup()
    {
        // Arrange: in-memory DbContext with NO signals pre-seeded
        using var db = TestDbContextFactory.Create();
        var linker = CreateLinker(db);
        var protocol = await SeedProtocol(db);
        var subjectId = Guid.NewGuid();

        // Parse a receptor line where parent signal DA does NOT exist in DB
        var line = new BioChainParser.ParsedLine("RECEPTOR", "DA.D2(Gi)[active]", null, null);

        // Act
        await linker.LinkAsync(protocol, line, subjectId);

        // Assert: receptor was stored with signal_code, NOT silently dropped
        var receptor = db.Receptors.Single();
        Assert.Equal("D2", receptor.Code);
        Assert.Equal("DA", receptor.SignalCode);
        Assert.Equal("NT", receptor.SignalType);
        Assert.Equal("Gi", receptor.Subtype);
        Assert.Null(receptor.SignalId); // no FK resolution needed
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test --filter "ComponentLinkerReceptorTests" -v minimal`
Expected: FAIL — current code does `if (parent is null) break;`

**Step 3: Modify RECEPTOR case in ComponentLinker**

Replace lines 82-99:

```csharp
case "RECEPTOR":
{
    var rec = BioChainParser.ExtractReceptor(line.Formula);
    if (rec is null) break;

    // Store signal code directly — no parent lookup needed
    db.Receptors.Add(new ReceptorEntity
    {
        SubjectId = subjectId,
        SignalCode = rec.Value.SignalCode,
        SignalType = BioChainParser.InferSignalType(rec.Value.SignalCode),
        Code = rec.Value.Code,
        State = rec.Value.State ?? "active",
        Subtype = rec.Value.Subtype,
        ProtocolId = protocol.Id,
    });
    await db.SaveChangesAsync(ct);
    break;
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test --filter "ComponentLinkerReceptorTests" -v minimal`
Expected: PASS

**Step 5: Commit**

```bash
git add src/BioChain.Repository/Linking/ComponentLinker.cs tests/
git commit -m "fix: receptor stored with signal_code, no longer silently dropped"
```

---

## Task 7: Refactor ComponentLinker — eliminate TRANSPORT chokepoint + delete static map

**Files:**
- Modify: `src/BioChain.Repository/Linking/ComponentLinker.cs`
- Modify: `src/BioChain.Utils/Parsing/BioChainParser.cs` — delete `MapTransporterToSignal`

**Step 1: Write the failing test**

```csharp
// tests/BioChain.Repository.Tests/Linking/ComponentLinkerTransportTests.cs
public class ComponentLinkerTransportTests
{
    [Fact]
    public async Task Transporter_Stored_Without_Static_Map_Or_Signal_Lookup()
    {
        using var db = TestDbContextFactory.Create();
        var linker = CreateLinker(db);
        var protocol = await SeedProtocol(db);
        var subjectId = Guid.NewGuid();

        // A transporter NOT in the static MapTransporterToSignal list
        var line = new BioChainParser.ParsedLine("TRANSPORT", "PMAT[\u2248] @PFC", null, null);

        await linker.LinkAsync(protocol, line, subjectId);

        // Should be stored, not dropped
        var transporter = db.Transporters.Single();
        Assert.Equal("PMAT", transporter.Code);
        Assert.NotNull(transporter.SignalCode); // parsed or inferred
        Assert.Null(transporter.SignalId);
    }

    [Fact]
    public async Task Known_Transporter_DAT_Gets_SignalCode_DA()
    {
        using var db = TestDbContextFactory.Create();
        var linker = CreateLinker(db);
        var protocol = await SeedProtocol(db);
        var subjectId = Guid.NewGuid();

        var line = new BioChainParser.ParsedLine("TRANSPORT", "DAT[\u2248] @NAc", null, null);

        await linker.LinkAsync(protocol, line, subjectId);

        var transporter = db.Transporters.Single();
        Assert.Equal("DAT", transporter.Code);
        Assert.Equal("DA", transporter.SignalCode);
        Assert.Equal("NT", transporter.SignalType);
    }
}
```

**Step 2: Run test to verify it fails**

Expected: FAIL — current code drops unknown transporters via `MapTransporterToSignal` returning null

**Step 3: Modify TRANSPORT case in ComponentLinker**

Replace lines 199-219. Infer the signal code from the transporter code or the DSL context. For common conventions (DAT→DA, SERT→5HT, NET→NE), parse the prefix. For unknown transporters, store the transporter code as-is and set signal_code to null (not dropped — just unresolved).

```csharp
case "TRANSPORT":
{
    var tr = BioChainParser.ExtractTransporter(line.Formula);
    if (tr is null) break;

    // Infer signal code: try convention first (DAT→DA), fallback to null
    var signalCode = BioChainParser.InferTransporterSignalCode(tr.Value.Code);
    var signalType = signalCode is not null ? BioChainParser.InferSignalType(signalCode) : null;

    db.Transporters.Add(new TransporterEntity
    {
        SubjectId = subjectId,
        SignalCode = signalCode,
        SignalType = signalType,
        Code = tr.Value.Code,
        State = tr.Value.State ?? "active",
        Clearance = tr.Value.Clearance ?? "\u2248",
        ProtocolId = protocol.Id,
    });
    await db.SaveChangesAsync(ct);
    break;
}
```

**Step 4: Add `InferTransporterSignalCode` to BioChainParser (replaces `MapTransporterToSignal`)**

```csharp
/// <summary>
/// Infers the parent signal code from a transporter code using naming conventions.
/// Returns null for unrecognized codes (NOT a gate — still stored).
/// </summary>
public static string? InferTransporterSignalCode(string code) => code.ToUpperInvariant() switch
{
    "DAT" or "VMAT2" => "DA",
    "SERT" => "5HT",
    "NET" => "NE",
    "GAT" or "GAT1" or "GAT3" => "GABA",
    "EAAT" or "EAAT1" or "EAAT2" or "EAAT3" => "GLU",
    "CHT" or "CHT1" => "ACH",
    "PMAT" => null,  // polyspecific — multiple signals
    _ => null         // unknown — store anyway, resolve later
};
```

**Step 5: Delete `MapTransporterToSignal` from BioChainParser**

Remove the old static method. Grep for any callers and update them.

**Step 6: Run tests**

Run: `dotnet test --filter "ComponentLinkerTransportTests" -v minimal`
Expected: PASS

**Step 7: Commit**

```bash
git add src/BioChain.Repository/Linking/ComponentLinker.cs src/BioChain.Utils/Parsing/BioChainParser.cs tests/
git commit -m "fix: transporter stored with signal_code, delete MapTransporterToSignal static map"
```

---

## Task 8: Refactor ComponentLinker — store codes on edges (FORMULA/FEEDBACK/DYSREG)

**Files:**
- Modify: `src/BioChain.Repository/Linking/ComponentLinker.cs`

**Step 1: Write the failing test**

```csharp
// tests/BioChain.Repository.Tests/Linking/ComponentLinkerEdgeTests.cs
public class ComponentLinkerEdgeTests
{
    [Fact]
    public async Task Formula_Edge_Stores_Source_And_Target_Codes()
    {
        using var db = TestDbContextFactory.Create();
        var linker = CreateLinker(db);
        var protocol = await SeedProtocol(db);
        var subjectId = Guid.NewGuid();

        var line = new BioChainParser.ParsedLine("FORMULA", "DA[↑↑] @VTA → BDNF[↓] @HPC", null, null);

        await linker.LinkAsync(protocol, line, subjectId);

        var edge = db.Edges.Single();
        Assert.Equal("DA", edge.SourceCode);
        Assert.Equal("NT", edge.SourceSignalType);
        Assert.Equal("VTA", edge.SourceRegion);
        Assert.Equal("BDNF", edge.TargetCode);
        Assert.Equal("P", edge.TargetSignalType);
        Assert.Equal("HPC", edge.TargetRegion);
        Assert.Equal("causal", edge.RelationshipKind);
    }

    [Fact]
    public async Task Feedback_Edge_Stores_Feedback_Kind()
    {
        using var db = TestDbContextFactory.Create();
        var linker = CreateLinker(db);
        var protocol = await SeedProtocol(db);
        var subjectId = Guid.NewGuid();

        var line = new BioChainParser.ParsedLine("FEEDBACK", "DA[↑] @NAc ⟳⁻ DA[↓] @VTA", null, null);

        await linker.LinkAsync(protocol, line, subjectId);

        var edge = db.Edges.Single();
        Assert.Equal("negative_feedback", edge.RelationshipKind);
    }

    [Fact]
    public async Task Dysreg_Edge_Stores_Dysregulation_Kind()
    {
        using var db = TestDbContextFactory.Create();
        var linker = CreateLinker(db);
        var protocol = await SeedProtocol(db);
        var subjectId = Guid.NewGuid();

        var line = new BioChainParser.ParsedLine("DYSREG", "CORT[↑↑] @ADR ⚡ BDNF[↓] @HPC", null, null);

        await linker.LinkAsync(protocol, line, subjectId);

        var edge = db.Edges.Single();
        Assert.Equal("CORT", edge.SourceCode);
        Assert.Equal("BDNF", edge.TargetCode);
        Assert.Equal("dysregulation", edge.RelationshipKind);
    }

    [Fact]
    public async Task Formula_With_Gate_Stores_Gate_Info_On_Edge()
    {
        using var db = TestDbContextFactory.Create();
        var linker = CreateLinker(db);
        var protocol = await SeedProtocol(db);
        var subjectId = Guid.NewGuid();

        // Formula with gate condition
        var line = new BioChainParser.ParsedLine("FORMULA", "{⊨(DA@NAc >= ↑)} DA[↑↑] @VTA → GLU[↑] @PFC", null, null);

        await linker.LinkAsync(protocol, line, subjectId);

        var edge = db.Edges.Single();
        Assert.NotNull(edge.GateCode);
        Assert.Equal("threshold", edge.GateType);
        Assert.NotNull(edge.GateCondition);
    }
}
```

**Step 2: Run test to verify it fails**

Expected: FAIL — current code doesn't populate code-based columns

**Step 3: Modify FORMULA/FEEDBACK/DEF case**

The key change: after `GetOrCreateSignalAsync` (which still creates signals and edges with IDs for backward compat), ALSO populate the code-based columns on the edge:

```csharp
case "FORMULA":
case "FEEDBACK":
case "DEF":
{
    var (gateInfo, cleanFormula) = BioChainParser.ExtractFormulaGateCondition(line.Formula);
    var (src, tgt) = BioChainParser.ExtractFormulaSignalRefs(cleanFormula);
    SignalEntity? srcSignal = null, tgtSignal = null;

    if (src is not null)
    {
        int? srcRegionId = null;
        if (src.Value.Region is not null)
        {
            var r = await GetOrCreateRegionAsync(src.Value.Region, subjectId, ct);
            srcRegionId = r.Id;
        }
        srcSignal = await GetOrCreateSignalAsync(subjectId, protocol.Id, src.Value.Code, srcRegionId, ct);
    }
    if (tgt is not null && tgt != src)
    {
        int? tgtRegionId = null;
        if (tgt.Value.Region is not null)
        {
            var r = await GetOrCreateRegionAsync(tgt.Value.Region, subjectId, ct);
            tgtRegionId = r.Id;
        }
        tgtSignal = await GetOrCreateSignalAsync(subjectId, protocol.Id, tgt.Value.Code, tgtRegionId, ct);
    }

    if (srcSignal is not null && tgtSignal is not null)
    {
        var opClass = line.Tag == "FEEDBACK" ? "feedback" : "causal";
        var op = line.Tag == "FEEDBACK" ? "\u27f3\u207b" : "\u2192";
        var kind = line.Tag == "FEEDBACK" ? "negative_feedback" : "causal";

        int? gateId = null;
        string? gateCode = null, gateType = null, gateCond = null;
        if (gateInfo is not null)
        {
            var structuredExpr = BioChainParser.ParseGateExpression(gateInfo.Value.Expression);
            var gateEntity = await gates.CreateAsync(new GateEntity
            {
                SubjectId = subjectId,
                Code = gateInfo.Value.Expression,
                Type = gateInfo.Value.Type,
                Expression = structuredExpr,
                ProtocolId = protocol.Id,
            }, ct);
            gateId = gateEntity.Id;
            gateCode = gateInfo.Value.Expression;
            gateType = gateInfo.Value.Type;
            gateCond = structuredExpr;
        }

        db.Edges.Add(new EdgeEntity
        {
            SubjectId = subjectId,
            // Legacy ID-based (still populated for backward compat)
            SourceType = "signal",
            SourceId = srcSignal.Id,
            TargetType = "signal",
            TargetId = tgtSignal.Id,
            // NEW: Code-based
            SourceCode = src!.Value.Code,
            SourceSignalType = BioChainParser.InferSignalType(src.Value.Code),
            SourceRegion = src.Value.Region,
            TargetCode = tgt!.Value.Code,
            TargetSignalType = BioChainParser.InferSignalType(tgt.Value.Code),
            TargetRegion = tgt.Value.Region,
            RelationshipKind = kind,
            // Gate
            Operator = op,
            OperatorClass = opClass,
            GateId = gateId,
            GateCode = gateCode,
            GateType = gateType,
            GateCondition = gateCond,
            ProtocolId = protocol.Id,
        });
        await db.SaveChangesAsync(ct);
    }
    break;
}
```

**Step 4: Apply same pattern to DYSREG case**

Same modification — populate code-based columns alongside existing ID-based columns. Set `RelationshipKind = "dysregulation"`.

**Step 5: Run tests**

Run: `dotnet test --filter "ComponentLinkerEdgeTests" -v minimal`
Expected: PASS

**Step 6: Commit**

```bash
git add src/BioChain.Repository/Linking/ComponentLinker.cs tests/
git commit -m "feat: edges now store code-based endpoints alongside legacy IDs"
```

---

## Task 9: Rename protocol → analysis

**Files:**
- Create: `src/BioChain.Repository/Entities/AnalysisEntity.cs`
- Modify: `src/BioChain.Repository/Data/BioChainDbContext.cs`
- Modify: All entities referencing `ProtocolId` / `ProtocolEntity`
- Modify: `src/BioChain.Repository/Linking/ComponentLinker.cs`
- Delete: `src/BioChain.Repository/Entities/ProtocolEntity.cs` (after migration)

**Design:** The `analysis` table is a lean audit log:
- `id`, `person_id`, `tag`, `formula`, `status`, `phase`, `data_id`, `embedding`, timestamps
- NO component FKs (signal_source_id, signal_target_id, receptor_id, etc. are removed)
- All other entities keep `analysis_id` FK (renamed from `protocol_id`)

**Step 1: Write test**

```csharp
// tests/BioChain.Repository.Tests/Entities/AnalysisEntityTests.cs
public class AnalysisEntityTests
{
    [Fact]
    public void AnalysisEntity_Has_Audit_Fields_Only()
    {
        var analysis = new AnalysisEntity
        {
            Tag = "FORMULA",
            Formula = "DA[↑↑] @VTA → BDNF[↓] @HPC",
            Status = "active",
            Phase = "ONSET",
        };

        Assert.Equal("FORMULA", analysis.Tag);
        Assert.Equal("DA[↑↑] @VTA → BDNF[↓] @HPC", analysis.Formula);
    }
}
```

**Step 2: Create AnalysisEntity**

```csharp
// src/BioChain.Repository/Entities/AnalysisEntity.cs
using Pgvector;

namespace BioChain.Repository.Entities;

public class AnalysisEntity
{
    public int Id { get; set; }
    public Guid? PersonId { get; set; }
    public string? Tag { get; set; }
    public string Formula { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Phase { get; set; }
    public int? DataId { get; set; }
    public Vector? Embedding { get; set; }
    public DateTimeOffset CreatedOnUtc { get; set; }
    public DateTimeOffset UpdatedOnUtc { get; set; }

    // Navigation
    public PersonEntity? Person { get; set; }
    public DataEntity? Data { get; set; }
}
```

**Step 3: Rename ProtocolId → AnalysisId on all entities**

Rename the property on: SignalEntity, ReceptorEntity, TransporterEntity, GateEntity, LimiterEntity, InterfaceEntity, EdgeEntity, ConstraintDefEntity, ToolEntity, ModuleEntity, LoopEntity, PlasticityEntity, PathwayEntity.

Use EF Core column mapping to keep the DB column as `analysis_id` (renamed from `protocol_id` via migration).

**Step 4: Update BioChainDbContext**

- Rename `ConfigureProtocol` → `ConfigureAnalysis`
- Remove component FK mappings (signal_source_id, signal_target_id, receptor_id, etc.)
- Map to table name `analysis`
- Update all ConfigureXxx methods to use `analysis_id` column name

**Step 5: Update ComponentLinker**

- Change method signature from `ProtocolEntity protocol` to `AnalysisEntity analysis`
- Rename `protocol.Id` → `analysis.Id` throughout
- Rename `ProtocolId = protocol.Id` → `AnalysisId = analysis.Id`

**Step 6: Generate migration**

Run: `dotnet ef migrations add RenameProtocolToAnalysis --project src/BioChain.Repository --startup-project src/BioChain.Server`

This migration should:
- Rename table `protocol` → `analysis`
- Rename column `protocol_id` → `analysis_id` on all child tables
- Drop the 8 component FK columns from the analysis table
- Update indexes

**Step 7: Run tests**

Run: `dotnet test -v minimal`
Expected: ALL PASS

**Step 8: Commit**

```bash
git add -A
git commit -m "refactor: rename protocol → analysis, remove component FKs from audit table"
```

---

## Task 10: Update SQL views for code-based joins

**Files:**
- Modify: `src/BioChain.Repository/Data/init/views.sql`

**Step 1: Update `v_cascade_edges` (or equivalent) to join on codes**

The views should now be able to build graph queries using code-based columns instead of (or in addition to) integer joins:

```sql
-- Outgoing edges for a signal by code
CREATE OR REPLACE VIEW v_edges_by_code AS
SELECT
    e.subject_id,
    e.source_code,
    e.source_signal_type,
    e.source_region,
    e.target_code,
    e.target_signal_type,
    e.target_region,
    e.relationship_kind,
    e.operator,
    e.operator_class,
    e.gate_code,
    e.gate_type,
    e.gate_condition,
    e.active
FROM edge e
WHERE e.source_code IS NOT NULL;
```

**Step 2: Add view for full graph by subject**

```sql
CREATE OR REPLACE VIEW v_subject_graph AS
SELECT
    'signal' AS node_type,
    s.code,
    s.type AS signal_type,
    r.code AS region,
    s.state,
    s.subject_id
FROM signal s
LEFT JOIN region r ON s.region_id = r.id
UNION ALL
SELECT
    'receptor',
    rec.code,
    rec.signal_type,
    NULL,
    rec.state,
    rec.subject_id
FROM receptor rec
UNION ALL
SELECT
    'transporter',
    t.code,
    t.signal_type,
    NULL,
    t.state,
    t.subject_id
FROM transporter t;
```

**Step 3: Commit**

```bash
git add src/BioChain.Repository/Data/init/views.sql
git commit -m "feat: add code-based graph views"
```

---

## Task 11: Clean up ConnectOrphanedSignalsAsync

**Files:**
- Modify: `src/BioChain.Repository/Linking/ComponentLinker.cs`

The `ConnectOrphanedSignalsAsync` method (lines 541-614) was a band-aid for the silent-drop problem. With code-based resolution, orphaned signals are less likely (receptors and transporters are no longer dropped). However, the method still has value for graph connectivity.

**Step 1: Review if method is still needed**

Check callers of `ConnectOrphanedSignalsAsync`. If it's called after analysis, consider whether it's still needed now that:
- Receptors always get stored (with signal_code)
- Transporters always get stored (with signal_code)
- Edges always get stored (with code-based endpoints)

**Step 2: If still useful, update to use code-based references**

The method currently queries by integer IDs. Update it to also check for code-based connections when determining orphan status.

**Step 3: Commit**

```bash
git add src/BioChain.Repository/Linking/ComponentLinker.cs
git commit -m "refactor: update ConnectOrphanedSignalsAsync for code-based resolution"
```

---

## Task 12: Verification and build

**Step 1: Run full build**

Run: `dotnet build BioChain.sln`
Expected: 0 errors, 0 warnings (or only pre-existing warnings)

**Step 2: Run all tests**

Run: `dotnet test BioChain.sln -v minimal`
Expected: ALL PASS

**Step 3: Verify no silent drops**

Grep the codebase for the old patterns that silently dropped data:

```bash
# Should find ZERO instances of this pattern in ComponentLinker:
grep -n "if (parent is null) break" src/BioChain.Repository/Linking/ComponentLinker.cs
# Expected: 0 matches

# MapTransporterToSignal should be gone:
grep -rn "MapTransporterToSignal" src/
# Expected: 0 matches
```

**Step 4: Commit final verification**

```bash
git commit --allow-empty -m "chore: verified zero silent drops, build green, all tests pass"
```

---

## Files Touched Summary

| File | Action | Task |
|------|--------|------|
| `src/BioChain.Repository/Entities/ReceptorEntity.cs` | Modify | 1 |
| `src/BioChain.Repository/Entities/TransporterEntity.cs` | Modify | 2 |
| `src/BioChain.Repository/Entities/EdgeEntity.cs` | Modify | 3 |
| `src/BioChain.Repository/Entities/AnalysisEntity.cs` | Create | 9 |
| `src/BioChain.Repository/Entities/ProtocolEntity.cs` | Delete | 9 |
| `src/BioChain.Repository/Data/BioChainDbContext.cs` | Modify | 1,2,3,9 |
| `src/BioChain.Repository/Linking/ComponentLinker.cs` | Modify | 6,7,8,9,11 |
| `src/BioChain.Repository/Linking/IComponentLinker.cs` | Modify | 9 |
| `src/BioChain.Utils/Parsing/BioChainParser.cs` | Modify | 7 |
| `src/BioChain.Repository/Data/init/views.sql` | Modify | 10 |
| `src/BioChain.Repository/Data/backfill-code-columns.sql` | Create | 5 |
| `src/BioChain.Repository/Migrations/*` | Create | 4,9 |
| All entities with ProtocolId | Modify | 9 |
| Test files | Create | 1-8 |

## Non-Goals

- Changing the DSL/prompt format
- Switching to a graph database
- Dropping integer FK columns (Phase 5 — separate future task after verification period)
- Changing the parser's tag extraction logic
- Adding treatment/intervention suggestions
