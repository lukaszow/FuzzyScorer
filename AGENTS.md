# AGENTS.md — FuzzyScorer

## Quick reference

- **.NET 10.0**, **C# 13**, class library shipped as NuGet package `FuzzyScorer` (zero runtime dependencies)
- **xUnit** test project, 114 tests, `InternalsVisibleTo` exposes `internal` members to tests
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
dotnet test                           # Run all 114 tests
dotnet test --filter "FullyQualifiedName~ScoringTests.Frequency_TypicalCase_ShouldCountCorrectly"  # single test
dotnet pack -c Release -o ./nupkgs    # Package (see pack.ps1)
dotnet run -c Release --project benchmarks/FuzzyScorer.Benchmarks  # BenchmarkDotNet suite
```

## Gotchas

- No custom NuGet sources: `nuget.config` is intentionally empty, so restore uses the default nuget.org source. `nupkgs/` is only the gitignored pack output (`dotnet pack -o ./nupkgs`), not a registered feed.
- `WordScorer.cs` holds the public class **`WordScorer`** (renamed from `Scorer.cs` in v2.0.0).
- `ScoreAsync`/`Score` map `sensitivity` (0.0–1.0) to edit distance via **`Math.Round(sensitivity × 50, MidpointRounding.AwayFromZero)`** — not `ceil`.
- Similarity grouping is bounded by `MaxSimilarityComparisons` (10,000,000); inputs with many distinct words throw `ArgumentException` instead of degrading. The budget counts distinct-word comparisons — repeated occurrences are free.
- Thresholds 1–2 use a deletion-variant index (hash-keyed, verified), threshold 3 uses a BK-tree for inputs ≥ 3,000 words, larger thresholds use the linear scan. `benchmarks/RESULTS.md` records before/after numbers.
- `FuzzyScorerOptions` is validated up front; its limits may only lower the hard constants on `WordScorer`.
- The 2.2.0 property names are `[Obsolete]` aliases: `Score`→`Count`, `OriginalSize`→`TotalWords`, `CompressedSize`→`UniqueGroups`, `ErrorText`→`Text`, `RepetitionCount`→`Occurrences`.
- `maxEditDistance` outside 0–50 throws **`ArgumentOutOfRangeException`** (it derives from `ArgumentException`); the comparison-budget overflow still throws plain `ArgumentException`.

## Project structure

```
FuzzyScorer.slnx
FuzzyScorer/                              # library — namespace `FuzzyScorer`
  WordScorer.cs                           # static `WordScorer` (scoring/normalization logic)
  FuzzyScorer.cs / IFuzzyScorer.cs        # async instance API + DI interface
  FuzzyScorerOptions.cs                   # options + validation
  TieBreakMode.cs / GroupingMode.cs       # option enums
  WordScore.cs / FuzzyScorerResult.cs / ErrorEntry.cs / WordGroup.cs  # immutable POCOs
FuzzyScorer.Tests/ScoringTests.cs         # xUnit — namespace `FuzzyScorer.Tests`
FuzzyScorer.Tests/CorrectnessTests.cs     # Normalization/validation/tie-break regressions
FuzzyScorer.Tests/GroupingEquivalenceTests.cs  # Differential tests vs reference implementation
FuzzyScorer.Tests/OptionsTests.cs         # Options, result enrichments, naming aliases
benchmarks/FuzzyScorer.Benchmarks/        # BenchmarkDotNet suite (RESULTS.md holds numbers)
nupkgs/                                   # local pack output (gitignored; not a package source)
```

## Conventions

- XML docs (`///`) required on all public members; nullable enabled — never return `null` from collection-returning methods
- `OrdinalIgnoreCase` comparisons; `ToLowerInvariant()` for culture-safe normalization
- Prefer LINQ over manual loops; result types are immutable (get-only props, validated constructors)
- `internal` helpers (`NormalizeUnicode`, `TokenizeLine`, `NormalizeAndExtractWords`, `NormalizeAndExtractWordsWithLines`, `CountWords`, `BuildSimilarityGroups`, `GetWordGroups`, `WordNormalizationRegex`) are test entry points via `InternalsVisibleTo`

## Security limits (public constants on `WordScorer`)

| Constant | Value | Enforced in |
|---|---|---|
| `MaxInputLength` | 1,000,000 | `NormalizeAndExtractWordsWithLines` (throws) |
| `MaxWordsPerText` | 10,000 | `NormalizeAndExtractWordsWithLines` (throws) |
| `MaxWordLength` | 256 | `TokenizeLine` (silently drops longer words) |
| `MaxEditDistanceLimit` | 50 | `GroupSimilarWords`, `GetWordGroups`, `GroupWordsBySimilarity`, `AreWordsSimilar`, `BuildSimilarityGroups` (throws) |
| `MaxSimilarityComparisons` | 10,000,000 | `BuildSimilarityGroups` (throws) |

## Architecture

- **Two APIs**: static `WordScorer` (quick use) + instance `IFuzzyScorer`/`FuzzyScorer` (async, typo detection, DI)
- `GetWordFrequencies` = exact case-insensitive counts; `GroupSimilarWords` = fuzzy merge via Levenshtein
- `IFuzzyScorer.ScoreAsync(string, double sensitivity, CancellationToken)` — sensitivity 0.0–1.0 maps linearly to `round(sensitivity × 50)` edit distance
- Grouping is **order-dependent**: the first occurrence becomes the group leader/representative
- Normalization: NFC, punctuation/control → space, all Unicode whitespace splits words, apostrophes removed, edge hyphens trimmed
- Error representative = most frequent → longest → ordinal (configurable via `TieBreak`); one `ErrorEntry` per distinct spelling, sorted by line; `SuggestedText`/`IsCertain` describe the suggestion
- `GroupingMode.ConnectedComponents` is opt-in and transitive; `Leader` is the default
- Old result/word property names remain as `[Obsolete]` aliases (see Gotchas)
- Long words (>256 chars) are silently dropped (not an error)
- Grouping complexity is bounded by `MaxSimilarityComparisons` (10,000,000); distinct words are compared once, the Levenshtein routine is banded, and k≤2 uses a deletion-variant index
- CI: `.github/workflows/ci.yml` (push & PR) and `.github/workflows/publish.yml` (tag `v*.*.*` or manual) build → test → pack → push to NuGet + GitHub Packages
- Related docs: `README.md` (API), `SECURITY.md` (threat model), `AI_RULES.md` (coding standards), `STRUCTURE.md` (dir rules — no new top-level folders without permission)
