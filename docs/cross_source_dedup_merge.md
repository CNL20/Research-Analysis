# Cross-source Dedup & Merge

## Overview

Replaces the legacy `ResearchPaper.ExternalId` + `ExternalSource` columns with a proper
`PaperSources` (many-to-one) table. Every ResearchPaper can now be referenced from
multiple bibliographic sources at once.

```
ResearchPapers  ──1──∞──  PaperSources
   Id, Title, Doi, Abstract...    PaperId, SourceName, ExternalId, SourceDoi, SourceUrl,
   PublicationYear, CitationCount, SourceCitationCount, RawMetadataJson (jsonb), ...
                                  FetchedAt, LastSeenAt
```

## Resolution & Merge Flow

```
                       ┌───────────────────────────────────────┐
                       │  ExternalPaperDto from any source     │
                       └────────────────┬──────────────────────┘
                                        │
                       ┌────────────────▼──────────────────────┐
                       │  STEP 1: Resolve canonical DOI       │
                       │  - if DOI present → normalize it      │
                       │  - if ArXiv (no DOI) → call          │
                       │    ArxivDoiResolver → OpenAlex        │
                       └────────────────┬──────────────────────┘
                                        │
                       ┌────────────────▼──────────────────────┐
                       │  STEP 2: Find existing paper          │
                       │  - match by DOI (PaperSources)        │
                       │  - else match by ArXiv ID             │
                       └────────────────┬──────────────────────┘
                                        │
                ┌───────────────────────┴──────────────────────┐
                │                                              │
   not found   │                              found             │
                ▼                                              ▼
   ┌────────────────────────┐                  ┌────────────────────────┐
   │ STEP 3A: INSERT NEW    │                  │ STEP 3B: UPDATE        │
   │ + 1 PaperSource row    │                  │ + upsert PaperSource   │
   │ + enqueue enrich-job   │                  │ (DOI, PDF, abstract)   │
   └────────────┬───────────┘                  └────────────┬───────────┘
                │                                           │
                └──────────────────────┬────────────────────┘
                                       ▼
                  ┌────────────────────────────────────────┐
                  │ STEP 4 (background, ~5s later):         │
                  │ EnrichPaperSourcesJob fills the         │
                  │ remaining 3 sources, updates metadata   │
                  └────────────────────────────────────────┘
```

## Merge Policy (Q7)

For each field, the first non-null value wins, in this order:

| Priority | Source          | Why                                      |
|----------|-----------------|------------------------------------------|
| 1        | Crossref        | Most authoritative metadata for journals  |
| 2        | OpenAlex        | Rich metadata, abstracts reconstructed    |
| 3        | SemanticScholar | Good citation counts, social data        |
| 4        | ArXiv           | Pre-prints, reliable PDF URLs            |

- **Title / Abstract / Journal / Year / Doi**: first non-empty in priority order
- **CitationCount**: `Max()` across sources (more accurate than any single source)
- **PdfUrl**: ArXiv > OpenAlex > SemanticScholar (ArXiv PDFs are most reliable)
- **Authors**: pick from the first priority source with non-empty authors list
- **Keywords**: union across Crossref + OpenAlex + SemanticScholar, distinct, take 8

See `Application/Services/Aggregation/MergedPaperBuilder.cs`.

## Background Enrichment (Q5)

When a paper is first imported (Step 3A above), a Hangfire background job is enqueued
with a 5-second delay. The job:

1. Reads cached `RawMetadataJson` from any existing `PaperSource` rows (no re-fetch).
2. Calls the 3 missing sources in parallel via `IEnrichmentFetcher`, which applies
   rate-limits per source:
   - OpenAlex: 1 req/sec (with `polite pool` email).
   - SemanticScholar: 1 req/3 sec.
   - Crossref: ~10 req/sec (with polite User-Agent).
3. Polly retry policy: 3 attempts, exponential backoff (2s, 4s, 8s).
4. Updates `ResearchPaper` fields **only when currently empty** (never overwrites).
5. Upserts `PaperSources` rows for each newly-fetched source.

## ArXiv → DOI Lookup

ArXiv papers usually do not have a DOI at the API level. `ArxivDoiResolver` calls
OpenAlex's native endpoint `GET /works/arXiv:{arxivId}` to retrieve the corresponding
DOI (if any). Results are cached in-memory for 7 days.

## Files Added / Modified

### New
- `Domain/Entities/PaperSource.cs`
- `Domain/Interfaces/IArxivDoiResolver.cs`
- `Application/Interfaces/IEnrichPaperSourcesEnqueuer.cs`
- `Application/Interfaces/IEnrichmentFetcher.cs`
- `Infrastructure/Configurations/PaperSourceConfiguration.cs` (in `Data/Configurations/`)
- `Infrastructure/ExternalApis/ArxivDoiResolver.cs`
- `Infrastructure/ExternalApis/EnrichmentFetcher.cs`
- `Infrastructure/Services/EnrichPaperSourcesEnqueuer.cs`
- `Infrastructure/Jobs/EnrichPaperSourcesJob.cs`
- `Infrastructure/Migrations/20260711153208_AddPaperSourcesAndDropExternalColumns.cs`
- `scripts/MergeVerification/Program.cs` — read-only verification script
- `scripts/MergeVerification/ScholarTrend.MergeVerification.csproj`
- `Tests/Services/Aggregation/MergedPaperBuilderTests.cs` — 12 unit tests

### Modified
- `Domain/Entities/ResearchPaper.cs` — removed `ExternalId` + `ExternalSource`,
  added `ICollection<PaperSource> PaperSources`
- `Infrastructure/Configurations/ResearchPaperConfiguration.cs` — removed unique index
- `Infrastructure/Repositories/PaperImportRepository.cs` — DOI-based resolution,
  cross-source dedup, enqueue enrich job
- `Infrastructure/Repositories/ResearchPaperRepository.cs` — `GetByExternalIdAsync`
  now queries `PaperSources`
- `Infrastructure/Repositories/SyncProposalRepository.cs` — dedup check now
  queries `PaperSources`
- `Infrastructure/Data/Seeders/ResearchPaperSeeder.cs` — writes to `PaperSources`
- `Application/DTOs/Aggregation/PaperSourceMetadataDto.cs` — added `Url`
- `Application/Interfaces/Repositories/IPaperImportRepository.cs` — `ct` parameter,
  optional `journalId`
- `API/Program.cs` — registered new DI services
- `ScholarTrend.Infrastructure.csproj` — added `Microsoft.Extensions.Caching.Memory`

## Verification

### Console script

```bash
dotnet run --project scripts/MergeVerification -- \
  "Host=localhost;Database=scholartrend;Username=postgres;Password=xxx"
```

Reports:
- How many papers have a real DOI vs fake/test DOI vs missing DOI
- How many of those real-DOI papers can be resolved by each external API
- Latency per source

### Unit tests

```bash
dotnet test --filter "FullyQualifiedName~MergedPaperBuilderTests"
```

12 tests, all passing. Covers Q7 priority, citation max, PDF priority, DOI normalization,
URL building, source-name selection, and edge cases (all-missing, empty title).

## Migration Notes

The migration:

1. Drops the unique index `IX_ResearchPaper_ExternalId`.
2. Creates `PaperSources` with `(PaperId, SourceName)` as composite PK.
3. **Backfills** existing data into `PaperSources` (one row per `ResearchPaper`,
   carrying the legacy `ExternalSource` / `ExternalId` over).
4. **Then** drops the legacy columns.

Down-migration restores the columns from `PaperSources` (takes the earliest
`FetchedAt` row per paper).