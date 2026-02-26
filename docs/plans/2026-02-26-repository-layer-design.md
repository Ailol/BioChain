# Repository Layer Design — BioChain v5.0

## Decisions
- 14 flat repos (one per table), service layer composes
- Materialized returns (Task<T?>, Task<List<T>>), no IQueryable exposure
- Vector search (pgvector <=>) co-located in each repo's FindSimilar method
- Plain C# classes with public setters for entities

## Entities

### BaseEntity (shared by 7 biochem component tables + data + protocol)
- `int Id`, `Guid PersonId`, `Vector? Embedding`, `DateTime CreatedOnUtc`, `DateTime UpdatedOnUtc`

### Exceptions
- **PersonEntity**: Guid PK, OwnerId, Name, Data (JSONB), Embedding, CreatedOnUtc, UpdatedOnUtc — no PersonId FK
- **DataEntity**: int PK, append-only (no UpdatedOnUtc). Kind, SourceText, Formula, Analyzed, Content (JSONB)
- **App-layer entities** (PersonShare, UserRole): CreatedAt/UpdatedAt naming, no embedding
- **Questionnaire entities**: mixed — QuestionnaireItem has no PersonId/Embedding

### Biochem Entities (all extend BaseEntity)
| Entity | Extra Columns |
|--------|--------------|
| SignalEntity | Type, Code, Region, State, Baseline, TauMin, TauMax |
| ReceptorEntity | SignalId, Code, Subtype, State |
| TransporterEntity | SignalId, Code, State, Clearance |
| GateEntity | Code, Type, Threshold, Expression, ParentId, History (string[]), Latched |
| LimiterEntity | TargetId, Code, Reaction, RateLimiting, Activity |
| InterfaceEntity | Code, SourceRegion, TargetRegion, Pathway, Active |
| ProtocolEntity | PersonId nullable, Formula, DataId, SignalSourceId, SignalTargetId, ReceptorId, TransporterId, GateId, LimiterId, InterfaceId |

## Repositories

### BaseRepository<T> where T : BaseEntity
```
GetByIdAsync(int id) → T?
GetByPersonAsync(Guid personId) → List<T>
CreateAsync(T entity) → T
UpdateAsync(T entity) → void
DeleteAsync(int id) → void
FindSimilarAsync(Guid personId, float[] embedding, int limit) → List<T>
```

### Specialized Queries per Repo
| Repository | Specialized Methods |
|---|---|
| PersonRepository | GetByOwnerAsync, GetByOwnerAndNameAsync, FindSimilarAsync (no person scope) |
| DataRepository | GetUnanalyzedAsync, GetByKindAsync, GetByTimeRangeAsync, MarkAnalyzedAsync |
| SignalRepository | GetByTypeAsync, GetByCodeAsync, GetByRegionAsync, GetByStateAsync, GetOrCreateAsync |
| ReceptorRepository | GetBySignalAsync, GetByStateAsync, GetByCodeAsync |
| TransporterRepository | GetBySignalAsync, GetByStateAsync |
| GateRepository | GetLatchedAsync, GetByTypeAsync, GetChildrenAsync |
| LimiterRepository | GetBottlenecksAsync, GetByTargetAsync |
| InterfaceRepository | GetActiveAsync, GetByRegionAsync, GetByPathwayAsync |
| ProtocolRepository | GetBySourceSignalAsync, GetByPersonOrGlobalAsync |
| PersonShareRepository | GetByPersonAsync, GetBySharedWithAsync |
| UserRoleRepository | GetByUserAsync, GetActiveByUserAsync |
| QuestionnaireItemRepository | GetAllOrderedAsync |
| QuestionnaireRepository | GetByPersonAsync, GetByTokenAsync, GetPendingAsync |
| QuestionnaireAnswerRepository | GetByQuestionnaireAsync |

### Non-BaseEntity Repos
PersonRepository, PersonShareRepository, UserRoleRepository, QuestionnaireItemRepository, QuestionnaireRepository, QuestionnaireAnswerRepository — implement CRUD directly (different PK types, different timestamp columns, missing fields).

## DbContext
Replace current stale DbContext entirely. 14 DbSets. Fluent API for:
- Table name mappings (snake_case)
- JSONB column types
- vector(1536) column types
- FK relationships with ON DELETE CASCADE
- Unique indices matching schema
