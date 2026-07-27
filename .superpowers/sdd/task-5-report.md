# Task 5 Completion Report: SegmentationService + Quotas + VisualizationRequest

## Summary

Task 5 implemented the orchestrating segmentation service (`SegmentationService`), quota/telemetry entity (`VisualizationRequest`), and configuration/gating infrastructure for the Natural Stone Impex product visualizer feature. All components follow TDD discipline: tests written first, then implementation, all 6 new tests passing plus full suite at 15/15.

---

## Files Created

1. **`src/NaturalStoneImpex.Api/Models/Entities/VisualizationRequest.cs`**
   - Quota/telemetry entity storing per-photo upload metrics
   - Contains: `Id`, `IpHash` (SHA-256 of IP + day, no PII), `Status` (enum: Succeeded/Failed), `DurationMs`, `CreatedAt`
   - Designed for privacy: no photos or results stored, only aggregated quota data

2. **`src/NaturalStoneImpex.Api/Services/Segmentation/VisualizerOptions.cs`**
   - Configuration class with 9 properties:
     - `Enabled` (default: true)
     - `EncoderPath`, `DecoderPath` (ONNX model paths)
     - `MaxUploadBytes` (10 MB), `MaxImageDimension` (2048)
     - `MaxConcurrentEncodes` (2), `EmbeddingCacheMinutes` (15)
     - `PerIpDailyLimit` (20), `GlobalDailyLimit` (500)

3. **`src/NaturalStoneImpex.Api/Services/Segmentation/EncodeGate.cs`**
   - Singleton semaphore bounding concurrent CPU-heavy ONNX encoder runs
   - Initialized from `VisualizerOptions.MaxConcurrentEncodes`

4. **`src/NaturalStoneImpex.Api/Services/Segmentation/ISegmentationService.cs`**
   - Public interface and contract records:
     - `SegmentResult(SessionToken, MaskPng as base64, Width, Height)`
     - `SegmentOutcome(StatusCode, Error?, Result?)` with static `Ok`/`Fail` factory methods
     - `ISegmentationService` interface with `SegmentNewAsync` and `RefineAsync`

5. **`src/NaturalStoneImpex.Api/Services/Segmentation/SegmentationService.cs`**
   - Full orchestration service implementing quota checks → encode → cache → decode → post-process pipeline
   - Key features:
     - Feature disabled check (503 error)
     - Per-IP and global daily quota enforcement (429 errors)
     - Fully in-memory photo processing (no disk writes)
     - Automatic image dimension resizing
     - Semaphore-gated ONNX encoding with 30s timeout
     - Embedding caching with configurable TTL
     - Mask post-processing pipeline (threshold → component filtering → morphological ops)
     - No-surface detection (400 error when mask is empty)
     - VisualizationRequest telemetry logging with IpHash and timing

6. **`tests/NaturalStoneImpex.Api.Tests/SegmentationServiceTests.cs`**
   - 6 passing integration tests with `FakeSamModel`
   - Tests cover:
     - Happy path: new photo → token + mask (base64) + dimensions
     - Refine: embedding cache reuse without re-encoding
     - Invalid token: 404 with exact error string
     - Per-IP quota: 429 after limit exceeded
     - Disabled feature: 503 error
     - Invalid image: 400 with exact error string

7. **Migration: `20260712172941_AddVisualizationRequests.cs`**
   - Creates `VisualizationRequests` table with columns: `Id`, `IpHash` (varchar 64), `Status`, `DurationMs`, `CreatedAt`
   - Two indexes:
     - Composite: `(IpHash, CreatedAt)` for efficient daily quota queries
     - Single: `CreatedAt` for global quota queries

## DbContext Updates

Modified `src/NaturalStoneImpex.Api/Data/AppDbContext.cs`:
- Added `DbSet<VisualizationRequest>` after `InvoiceItems`
- Configured entity in `OnModelCreating` with index definitions

---

## TDD Verification

### RED Phase (Step 3)
```
$ dotnet test tests/NaturalStoneImpex.Api.Tests --filter SegmentationServiceTests
error CS0246: The type or namespace name 'VisualizerOptions' could not be found
error CS0246: The type or namespace name 'SegmentationService' could not be found
```
**Result:** Tests failed as expected (classes not yet implemented).

### GREEN Phase (Step 5)
```
$ dotnet test tests/NaturalStoneImpex.Api.Tests --filter SegmentationServiceTests
Passed!  - Failed: 0, Passed: 6, Skipped: 0, Total: 6, Duration: 770 ms
```
**Result:** All 6 tests passing after implementation.

### Full Suite (Step 5)
```
$ dotnet test
Passed!  - Failed: 0, Passed: 15, Skipped: 0, Total: 15, Duration: 2 s
```
**Result:** 6 new SegmentationServiceTests + 9 existing tests (from Tasks 1-4) = 15/15 passing.

### Build (Step 6)
```
$ dotnet build
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:08.03
```
**Result:** Clean build with no warnings.

---

## Implementation Self-Review

✓ **Bulgarian Error Strings:** All 7 error constants match brief exactly:
  - "Визуализаторът е временно недостъпен." (unavailable)
  - "В момента има много заявки. Опитайте отново след малко." (busy)
  - "Достигнахте дневния лимит за визуализации. Опитайте отново утре." (quota)
  - "Визуализаторът е временно недостъпен. Моля, опитайте по-късно." (global quota)
  - "Моля, качете снимка във формат JPG или PNG до 10 MB." (bad image)
  - "Сесията е изтекла. Моля, качете снимката отново." (expired token)
  - "Не разпознахме повърхност тук. Опитайте друго място или използвайте четката." (no surface)

✓ **No Disk Writes:** Photo data flows as stream → `Image.LoadAsync<Rgb24>()` → in-memory processing → base64 PNG mask. Verified: no `File.WriteAllBytes()` or disk I/O anywhere in the service.

✓ **Entity & Decimal Conventions:** `VisualizationRequest` follows EF Core patterns; `IpHash` is string (SHA-256 hex), `DurationMs` is int, `CreatedAt` is DateTime.

✓ **Nullable Reference Types:** Enabled in project; all potential nulls explicitly handled (e.g., `embedding is null` check, `Error?` in outcome).

✓ **Private Fields:** Service uses `_camelCase` naming (`_model`, `_context`, `_cache`, `_options`, `_gate`).

✓ **Async/Await:** No `.Result` or `.Wait()` blocking calls; `SegmentNewAsync` and `RefineAsync` are properly async.

✓ **Memory Cache Configuration:** Uses `MemoryCacheEntryOptions` with `Size = 1` and `SlidingExpiration`.

✓ **Quota Logic:** 
  - Per-IP: `IpHash == hashOfIp_plusDay && CreatedAt >= today` → count check
  - Global: `CreatedAt >= today` → count check
  - Both enforced before and after heavy encoding work

✓ **Semaphore Gating:** `EncodeGate.Semaphore.WaitAsync(30s)` → release in finally block, prevents unbounded concurrent ONNX loads.

✓ **Migration:** Generated migration includes:
  - `CreateTable("VisualizationRequests", ...)`
  - `CreateIndex` on `(IpHash, CreatedAt)`
  - `CreateIndex` on `CreatedAt`

✓ **Task 6 Contract:** All required signatures present:
  - `public record SegmentResult(string SessionToken, string MaskPng, int Width, int Height)`
  - `public record SegmentOutcome(int StatusCode, string? Error, SegmentResult? Result)` with `Ok`/`Fail` statics
  - `public interface ISegmentationService` with `SegmentNewAsync` and `RefineAsync`
  - `public class VisualizerOptions` with exact property names and defaults
  - `public class EncodeGate` with public `Semaphore` property

---

## Commit

```
6eef0d1 feat(visualizer): segmentation service with quotas and embedding cache
```
**Files changed:** 10 (1 migration pair + 1 snapshot update + 8 new source files)

---

## Concerns

None. All acceptance criteria met:
- TDD workflow followed (RED → GREEN → REFACTOR)
- All 6 unit tests passing with exact Bulgarian assertions
- Full suite at 15/15
- Zero disk writes of photo data
- Migration correct and builds cleanly
- Service implements complete quota pipeline with embedding cache
- Public interface contracts match Task 6 requirements

---

## Evidence

- **Test output:** 6/6 SegmentationServiceTests passing (770ms)
- **Full suite:** 15/15 all tests passing (2s)
- **Build:** 0 warnings, 0 errors
- **Migration:** Inspected, contains CreateTable + both indexes
- **Code review:** No violations of CLAUDE.md conventions

Task 5 ready for Task 6 (Controller integration).
