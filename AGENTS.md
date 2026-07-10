# AGENTS.md — FuzzyScorer

## Quick reference

- **.NET 10.0**, **C# 13**, **library** (NuGet package: `FuzzyScorer`)
- **xUnit** test project, 49 tests, `InternalsVisibleTo` grants access to `internal` members
- Solution uses **`.slnx`** format (not `.sln`)
- **No CI workflows** — build/test must be run locally
- `GenerateDocumentationFile>true` — missing XML docs become compiler warnings

## Commands

```bash
dotnet build                          # Debug build (0 warnings expected)
dotnet build --configuration Release  # Release build
dotnet test                           # Run all 49 tests
dotnet test --filter "FullyQualifiedName~ScoringTests.Frequency_TypicalCase_ShouldCountCorrectly"  # Single test
dotnet pack -c Release -o ./nupkgs    # Package (see also pack.ps1)
```

## Project structure

```
FuzzyScorer/
├── FuzzyScorer.slnx
├── FuzzyScorer/                     # Library — namespace `FuzzyScorer`
│   ├── Scorer.cs                    # Static API: class `WordScorer` (note: filename ≠ class name)
│   ├── WordScore.cs                 # Immutable POCO (Text, Score)
│   ├── IFuzzyScorer.cs              # Async interface (DI-friendly)
│   ├── FuzzyScorer.cs               # Instance impl of IFuzzyScorer
│   ├── FuzzyScorerResult.cs         # Result: OriginalSize, CompressedSize, Errors
│   └── ErrorEntry.cs                # ErrorEntry: ErrorText, RepetitionCount, LineNumbers
├── FuzzyScorer.Tests/               # xUnit — namespace `FuzzyScorer.Tests`
│   └── ScoringTests.cs
└── nupkgs/                          # Local NuGet feed (see nuget.config)
```

## Conventions

- **Naming**: PascalCase for public, `_camelCase` for private fields, camelCase for locals
- **XML docs**: all public members MUST have `///` (generates CS1591 otherwise)
- **Null safety**: `<Nullable>enable</Nullable>` — never return `null` from collection-returning methods
- **Collections**: use LINQ (`.GroupBy`, `.Select`, `.ToList()`) over manual loops
- **Strings**: `OrdinalIgnoreCase` comparisons, `ToLowerInvariant()` for culture-safety
- **Immutability**: `WordScore`, `FuzzyScorerResult`, `ErrorEntry` are immutable (get-only properties, validated constructors)

## Security limits (on `WordScorer`)

| Constant | Value | Checked in |
|---|---|---|
| `MaxInputLength` | 1,000,000 | `NormalizeAndExtractWords` |
| `MaxWordsPerText` | 10,000 | `NormalizeAndExtractWords` |
| `MaxWordLength` | 256 | `NormalizeAndExtractWords` (silently drops longer words) |
| `MaxEditDistanceLimit` | 50 | `GroupSimilarWords`, `GetWordGroups` |

## Key architecture notes

- **Two APIs**: static `WordScorer` (quick use) + instance `IFuzzyScorer`/`FuzzyScorer` (async, typo detection, DI)
- `WordScorer.GetWordFrequencies` — exact case-insensitive counts
- `WordScorer.GroupSimilarWords` — fuzzy grouping by Levenshtein distance
- `IFuzzyScorer.ScoreAsync(string, double sensitivity, CancellationToken)` — sensitivity 0.0–1.0 maps linearly to `ceil(sensitivity × 50)` edit distance
- `BuildSimilarityGroups`, `GetWordGroups`, `WordNormalizationRegex`, `NormalizeAndExtractWords` are `internal` (exposed to tests via `InternalsVisibleTo`)
- Long words (>256 chars) are silently dropped (not an error)
- `ErrorEntry` reports typos as any group member that is not the most-frequent word in its group
