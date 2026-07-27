# Task 1 Report: Test project + Product visualizer fields + migration

## What I implemented

Followed the brief (`.superpowers/sdd/task-1-brief.md`) step by step, in TDD order.

1. **Solution file** — no `.sln` existed at the repo root (only `src/NaturalStoneImpex.sln`, which was left untouched). Created a root solution containing `src/NaturalStoneImpex.Api`, `src/NaturalStoneImpex.Client`, and the new test project.
   - Note: `dotnet new sln` on this machine's SDK (10.0.300) defaults to the newer `.slnx` XML format instead of the classic `.sln`. Produced `NaturalStoneImpex.slnx` at the repo root. Functionally equivalent — `dotnet build`/`dotnet test`/`dotnet sln add` all operate on it normally, and it satisfies the brief's intent (a root solution referencing all three projects).

2. **Test project** — scaffolded `tests/NaturalStoneImpex.Api.Tests` via `dotnet new xunit`, added a project reference to `NaturalStoneImpex.Api`, added `Microsoft.EntityFrameworkCore.InMemory`, and added it to the solution.
   - The xunit template defaulted `TargetFramework` to `net10.0` (this SDK's default); changed it to `net8.0` to match the rest of the solution.
   - `dotnet add package` without a version pin resolved `Microsoft.EntityFrameworkCore.InMemory` to 10.0.9, which is incompatible with net8.0 (NU1202). Re-added pinned to `8.0.*`, which resolved to 8.0.28 and restored cleanly.
   - Deleted the template's placeholder `UnitTest1.cs` (not part of the brief; an empty passing test would just be noise in the suite).

3. **Failing test** — created `tests/NaturalStoneImpex.Api.Tests/ProductVisualizerFieldsTests.cs` verbatim from the brief.

4. **Entity fields** — added to `Product.cs` after `IsActive`:
   `IsVisualizerEnabled` (bool), `TextureImagePath` (string?), `TextureWidthMeters` (decimal, default `1.00m`).

5. **EF configuration** — in `AppDbContext.cs`, `Product` entity block: `TextureImagePath` max length 500, `TextureWidthMeters` precision (18,2) with default value 1.00m, index on `IsVisualizerEnabled`.

6. **DTOs**:
   - `ProductDto`: added `IsVisualizerEnabled`, `TextureImagePath`, `TextureWidthMeters` (init-only), after `IsActive`.
   - `CreateProductRequest` / `UpdateProductRequest`: added `IsVisualizerEnabled` (bool) and `TextureWidthMeters` (decimal, default 1.00m, `[Range(0.1, 100)]` with the exact Bulgarian error message from the brief).

7. **ProductService.cs**:
   - `GetByIdAsync`: maps the three new fields into `ProductDto`.
   - `CreateAsync`: sets `TextureWidthMeters` on the new `Product`; added the guard `if (request.IsVisualizerEnabled) throw new InvalidOperationException("За да включите продукта във визуализатора, първо качете текстура.");` right after the duplicate-name check (a brand-new product has no texture yet, so it can never be created with the visualizer already enabled); maps the three fields into the returned `ProductDto`.
   - `UpdateAsync`: after `product.StockQuantity = request.StockQuantity;`, guards `IsVisualizerEnabled` against a missing `TextureImagePath` with the same Bulgarian message, then assigns `product.IsVisualizerEnabled` and `product.TextureWidthMeters`; maps the three fields into the returned `ProductDto`.

8. **Migration** — `dotnet ef migrations add AddProductVisualizerFields --project src/NaturalStoneImpex.Api`, producing `AddColumn` for all three new columns plus `CreateIndex` on `IsVisualizerEnabled`, matching the brief's expected shape exactly (verified the generated migration file content).

## What I tested + results

- Focused test during TDD: `dotnet test tests/NaturalStoneImpex.Api.Tests --filter ProductVisualizerFieldsTests`
- Full suite before commit: `dotnet test` (only test project in repo) — 2/2 passed, 0 warnings.
- Full solution build: `dotnet build` — 0 warnings, 0 errors (Api, Client incl. Blazor wwwroot output, and Tests all built).
- Re-ran build + test again after `git commit` to confirm the committed tree is self-consistent — same pristine results.

## TDD Evidence

### RED

Command:
```
dotnet test tests/NaturalStoneImpex.Api.Tests --filter ProductVisualizerFieldsTests
```
Output (relevant lines):
```
C:\...\ProductVisualizerFieldsTests.cs(25,13): error CS0117: 'Product' does not contain a definition for 'IsVisualizerEnabled' [...]
C:\...\ProductVisualizerFieldsTests.cs(26,13): error CS0117: 'Product' does not contain a definition for 'TextureImagePath' [...]
C:\...\ProductVisualizerFieldsTests.cs(27,13): error CS0117: 'Product' does not contain a definition for 'TextureWidthMeters' [...]
```
Why expected: matches the brief's Step 3 expectation exactly (`'Product' does not contain a definition for 'IsVisualizerEnabled'`) — the entity had no visualizer fields yet at this point.

### GREEN

Command:
```
dotnet test tests/NaturalStoneImpex.Api.Tests --filter ProductVisualizerFieldsTests
```
Output:
```
Passed!  - Failed:     0, Passed:     2, Skipped:     0, Total:     2, Duration: 913 ms - NaturalStoneImpex.Api.Tests.dll (net8.0)
```
Both `Product_persists_visualizer_fields` and `New_product_defaults_visualizer_off_with_one_meter_texture` pass after implementing Steps 4–7.

## Files changed

- `NaturalStoneImpex.slnx` (new, repo-root solution — see note on `.slnx` vs `.sln` above)
- `src/NaturalStoneImpex.Api/Models/Entities/Product.cs` (modified)
- `src/NaturalStoneImpex.Api/Data/AppDbContext.cs` (modified)
- `src/NaturalStoneImpex.Api/Migrations/AppDbContextModelSnapshot.cs` (modified, auto-generated)
- `src/NaturalStoneImpex.Api/Migrations/20260712163554_AddProductVisualizerFields.cs` (new, auto-generated)
- `src/NaturalStoneImpex.Api/Migrations/20260712163554_AddProductVisualizerFields.Designer.cs` (new, auto-generated)
- `src/NaturalStoneImpex.Api/Models/DTOs/ProductDto.cs` (modified)
- `src/NaturalStoneImpex.Api/Models/DTOs/CreateProductRequest.cs` (modified)
- `src/NaturalStoneImpex.Api/Models/DTOs/UpdateProductRequest.cs` (modified)
- `src/NaturalStoneImpex.Api/Services/ProductService.cs` (modified)
- `tests/NaturalStoneImpex.Api.Tests/NaturalStoneImpex.Api.Tests.csproj` (new)
- `tests/NaturalStoneImpex.Api.Tests/ProductVisualizerFieldsTests.cs` (new)

Commit: `e53346f` — "feat(visualizer): add product texture fields and test project"

## Self-review findings

- **Completeness**: all 10 brief steps done. All three DTO-mapping sites in `ProductService` updated (`GetByIdAsync`, `CreateAsync` return block, `UpdateAsync` return block), plus the two business-rule guards (create-time and update-time) exactly as specified.
- **Quality**: names match the brief exactly (`IsVisualizerEnabled`, `TextureImagePath`, `TextureWidthMeters`); Bulgarian error messages copied verbatim; code follows the existing file style (same brace/indentation conventions, same DTO-mapping pattern as pre-existing fields).
- **Discipline**: nothing extra built beyond the brief — no new controller endpoints, no client-side changes (out of scope for Task 1). Removed the xunit template's placeholder `UnitTest1.cs` since it wasn't part of the brief and would otherwise sit in the suite as dead noise.
- **Testing**: both tests exercise real behavior (EF Core InMemory persistence round-trip, and default-value check on a bare `new Product()`) rather than tautologies. Test output is pristine — no warnings, no skipped tests, in both the focused run and the full-suite run.

## Issues or concerns

- **`.slnx` vs `.sln`**: the brief's fallback instructions literally say "create one first" with `dotnet new sln`, but this SDK version's default template output is the newer `.slnx` format, not classic `.sln`. I did not fight the tool default since it's functionally identical for `dotnet build`/`dotnet test`/CI purposes and no downstream task step depends on the literal `.sln` extension. Flagging in case CI or an IDE in this environment specifically expects `.sln`.
- **Pre-existing `src/NaturalStoneImpex.sln`**: left untouched — it doesn't include the new test project, so anyone opening that specific solution file (e.g. in an older Visual Studio session) won't see `NaturalStoneImpex.Api.Tests`. Out of scope for this task since the brief only speaks to the repo-root solution; flagging for awareness.
- No other deviations from the brief. No blockers.
