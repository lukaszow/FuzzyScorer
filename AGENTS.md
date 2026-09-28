# AGENTS.md — FuzzyScorer

## Quick reference

- **.NET 10.0**, **C# 13**, class library shipped as NuGet package `FuzzyScorer` (zero runtime dependencies)
- **xUnit** test project, 57 tests, `InternalsVisibleTo` exposes `internal` members to tests
- Solution uses **`.slnx`** format (not `.sln`)
- **CI workflow**: `.github/workflows/ci.yml` (build/format/test on push & PR) and `.github/workflows/publish.yml` (tag `v*.*.*` → build, test, pack, push to NuGet)
- `GenerateDocumentationFile>true` — every public member needs `///` or it triggers CS1591 (keep the build at 0 warnings)
- Analyzers run with `TreatWarningsAsErrors=true` (`Directory.Build.props`)
- Releasing = bump `<Version>` in `FuzzyScorer/FuzzyScorer.csproj` and push a `v*.*.*` tag (or publish a GitHub release)

## Commands

```bash
dotnet build                          # Debug build (0 warnings expected)
dotnet build -c Release               # Release build
dotnet format --verify-no-changes     # Style/analyzer check used in CI
dotnet test                           # Run all 57 tests
dotnet test --filter "FullyQualifiedName~ScoringTests.Frequency_TypicalCase_ShouldCountCorrectly"  # single test
dotnet pack -c Release -o ./nupkgs    # Package (see pack.ps1)
```

## Gotchas

- No custom NuGet sources: `nuget.config` is intentionally empty, so restore uses the default nuget.org source. `nupkgs/` is only the gitignored pack output (`dotnet pack -o ./nupkgs`), not a registered feed.
- `WordScorer.cs` holds the public class **`WordScorer`** (renamed from `Scorer.cs` in v2.0.0).
- `ScoreAsync` maps `sensitivity` (0.0–1.0) to edit distance via **`Math.Round(sensitivity × 50)`** — not `ceil`.
- Similarity grouping is bounded by `MaxSimilarityComparisons` (10,000,000); inputs with many distinct words throw `ArgumentException` instead of degrading.

## Project structure

```
FuzzyScorer.slnx
FuzzyScorer/                              # library — namespace `FuzzyScorer`
  WordScorer.cs                           # static `WordScorer` (scoring/normalization logic)
  FuzzyScorer.cs / IFuzzyScorer.cs        # async instance API + DI interface
  WordScore.cs / FuzzyScorerResult.cs / ErrorEntry.cs  # immutable POCOs
FuzzyScorer.Tests/ScoringTests.cs         # xUnit — namespace `FuzzyScorer.Tests`
nupkgs/                                   # local pack output (gitignored; not a package source)
```

## Conventions

- XML docs (`///`) required on all public members; nullable enabled — never return `null` from collection-returning methods
- `OrdinalIgnoreCase` comparisons; `ToLowerInvariant()` for culture-safe normalization
- Prefer LINQ over manual loops; result types are immutable (get-only props, validated constructors)
- `internal` helpers (`NormalizeAndExtractWords`, `NormalizeAndExtractWordsWithLines`, `BuildSimilarityGroups`, `GetWordGroups`, `WordNormalizationRegex`) are test entry points via `InternalsVisibleTo`

## Security limits (public constants on `WordScorer`)

| Constant | Value | Enforced in |
|---|---|---|
| `MaxInputLength` | 1,000,000 | `NormalizeAndExtractWordsWithLines` (throws) |
| `MaxWordsPerText` | 10,000 | `NormalizeAndExtractWordsWithLines` (throws) |
| `MaxWordLength` | 256 | `NormalizeAndExtractWordsWithLines` (silently drops longer words) |
| `MaxEditDistanceLimit` | 50 | `GroupSimilarWords`, `GetWordGroups`, `GroupWordsBySimilarity`, `AreWordsSimilar`, `BuildSimilarityGroups` (throws) |
| `MaxSimilarityComparisons` | 10,000,000 | `BuildSimilarityGroups` (throws) |

## Architecture

- **Two APIs**: static `WordScorer` (quick use) + instance `IFuzzyScorer`/`FuzzyScorer` (async, typo detection, DI)
- `GetWordFrequencies` = exact case-insensitive counts; `GroupSimilarWords` = fuzzy merge via Levenshtein
- `IFuzzyScorer.ScoreAsync(string, double sensitivity, CancellationToken)` — sensitivity 0.0–1.0 maps linearly to `round(sensitivity × 50)` edit distance
- Grouping is **order-dependent**: the first occurrence becomes the group leader/representative
- `ErrorEntry` = any group member that isn't the most-frequent word in its group (1-based line numbers)
- Long words (>256 chars) are silently dropped (not an error)
- Grouping complexity is bounded by `MaxSimilarityComparisons` (10,000,000); the Levenshtein routine is banded and short-circuits on threshold
- CI: `.github/workflows/ci.yml` (push & PR) and `.github/workflows/publish.yml` (tag `v*.*.*` or manual) build → test → pack → push to NuGet + GitHub Packages
- Related docs: `README.md` (API), `SECURITY.md` (threat model), `AI_RULES.md` (coding standards), `STRUCTURE.md` (dir rules — no new top-level folders without permission)
