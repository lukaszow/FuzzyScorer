# AGENTS.md — FuzzyScorer

## Quick reference

- **.NET 10.0**, **C# 13**, class library shipped as NuGet package `FuzzyScorer` (zero runtime dependencies)
- **xUnit** test project, 49 tests, `InternalsVisibleTo` exposes `internal` members to tests
- Solution uses **`.slnx`** format (not `.sln`)
- `GenerateDocumentationFile>true` — every public member needs `///` or it triggers CS1591 (keep the build at 0 warnings)
- Releasing = bump `<Version>` in `FuzzyScorer/FuzzyScorer.csproj` and push a `v*.*.*` tag (or publish a GitHub release)

## Commands

```bash
dotnet build                          # Debug build (0 warnings expected)
dotnet build -c Release               # Release build
dotnet test                           # Run all 49 tests
dotnet test --filter "FullyQualifiedName~ScoringTests.Frequency_TypicalCase_ShouldCountCorrectly"  # single test
dotnet pack -c Release -o ./nupkgs    # Package (see pack.ps1)
```

## Gotchas

- `nuget.config` registers a **local package source at `./nupkgs`** (plus GitHub Packages). A fresh clone has no `nupkgs/`, so `dotnet restore`/`build`/`test` fail with **NU1301** until you `mkdir nupkgs` (CI does this).
- `Scorer.cs` holds the public class **`WordScorer`** — filename ≠ class name.
- `ScoreAsync` maps `sensitivity` (0.0–1.0) to edit distance via **`Math.Round(sensitivity × 50)`** — not `ceil`.

## Project structure

```
FuzzyScorer.slnx
FuzzyScorer/                              # library — namespace `FuzzyScorer`
  Scorer.cs                               # static `WordScorer` (all scoring/normalization logic)
  FuzzyScorer.cs / IFuzzyScorer.cs        # async instance API + DI interface
  WordScore.cs / FuzzyScorerResult.cs / ErrorEntry.cs  # immutable POCOs
FuzzyScorer.Tests/ScoringTests.cs         # xUnit — namespace `FuzzyScorer.Tests`
nupkgs/                                   # local NuGet feed (gitignored; must exist for restore)
```

## Conventions

- XML docs (`///`) required on all public members; nullable enabled — never return `null` from collection-returning methods
- `OrdinalIgnoreCase` comparisons; `ToLowerInvariant()` for culture-safe normalization
- Prefer LINQ over manual loops; result types are immutable (get-only props, validated constructors)
- `internal` helpers (`NormalizeAndExtractWords`, `BuildSimilarityGroups`, `GetWordGroups`, `WordNormalizationRegex`) are test entry points via `InternalsVisibleTo`

## Security limits (public constants on `WordScorer`)

| Constant | Value | Enforced in |
|---|---|---|
| `MaxInputLength` | 1,000,000 | `NormalizeAndExtractWords` (throws) |
| `MaxWordsPerText` | 10,000 | `NormalizeAndExtractWords` (throws) |
| `MaxWordLength` | 256 | `NormalizeAndExtractWords` (silently drops longer words) |
| `MaxEditDistanceLimit` | 50 | `GroupSimilarWords`, `GetWordGroups` (throws) |

## Architecture

- Two APIs: static `WordScorer` (sync, fire-and-forget) + `IFuzzyScorer`/`FuzzyScorer` (async typo detection, DI-friendly)
- `GetWordFrequencies` = exact case-insensitive counts; `GroupSimilarWords` = fuzzy merge via Levenshtein
- Grouping is **order-dependent**: the first occurrence becomes the group leader/representative
- `ErrorEntry` = any group member that isn't the most-frequent word in its group (1-based line numbers)
- CI: `.github/workflows/publish.yml` (tag `v*.*.*` or manual) and `publish-github.yml` (published release) both build → test → pack → push to NuGet + GitHub Packages
- Related docs: `README.md` (API), `SECURITY.md` (threat model), `AI_RULES.md` (coding standards), `STRUCTURE.md` (dir rules — no new top-level folders without permission)
