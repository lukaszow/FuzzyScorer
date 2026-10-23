# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [2.3.0] - 2026-10-23

### Added

- `FuzzyScorerOptions` with `MaxEditDistance`, stricter limits, `MinWordLengthForFuzzy`,
  `IgnoreDiacritics`, `StripEdgeHyphens`, `TieBreak`, and `Grouping`. Every static method
  and the `FuzzyScorer` class accept an options overload.
- `TieBreakMode` (`FirstOccurrence`, `LongestThenOrdinal`, `ReportAllTies`) and
  `GroupingMode` (`Leader`, `ConnectedComponents`).
- `WordGroup` and `FuzzyScorerResult.WordGroups` (representative, distinct members, count).
- `FuzzyScorerResult.MaxEditDistance`.
- `ErrorEntry.SuggestedText` (representative) and `ErrorEntry.IsCertain` (`false` on ties).
- Synchronous `FuzzyScorer.Score(...)` overloads; `CancellationToken` is now optional in
  `ScoreAsync`.
- `MinWordLengthForFuzzy` prevents short words such as `"cat"`/`"cot"` from merging.
- `IgnoreDiacritics` merges `"cafe"` with `"café"`.

### Changed

- Property names are now clearer; the old names remain as `[Obsolete]` aliases:
  `WordScore.Score` → `Count`, `FuzzyScorerResult.OriginalSize` → `TotalWords`,
  `CompressedSize` → `UniqueGroups`, `ErrorEntry.ErrorText` → `Text`,
  `RepetitionCount` → `Occurrences`.
- `sensitivity` rounds with `MidpointRounding.AwayFromZero` instead of banker's rounding.
- Options limits can only make processing stricter; values above the hard caps on
  `WordScorer` throw `ArgumentOutOfRangeException`.
- `MinWordLengthForFuzzy` defaults to `0` in 2.3 to preserve behavior; 3.0 will default to 4.

### Notes

- `IFuzzyScorer` only gained the optional `CancellationToken` parameter. The options and
  synchronous overloads are on the `FuzzyScorer` class, so existing interface
  implementations keep compiling.

### Tests

- 114 tests (19 new options/result tests).

## [2.2.0] - 2026-10-23

### Performance

- **Distinct words are compared once.** Repeated occurrences reuse the group assigned to
  their first occurrence instead of being compared again, so the comparison budget is
  spent on unique words only.
- **Deletion-variant index for thresholds 1–2.** Candidate leaders are found through
  hash-keyed deletion variants and verified with the exact bounded distance. 10,000 unique
  words at `maxEditDistance: 1` now take ~32 ms instead of throwing; at 2, ~104 ms.
- **BK-tree for threshold 3** on inputs of 3,000+ words, with subtree length pruning.
- **Allocation-free distance:** the banded Levenshtein routine uses stack/pooled buffers
  instead of allocating two arrays per comparison.
- **Single-pass counting:** `GetWordFrequencies` builds counts while tokenizing, and
  `FuzzyScorer` no longer materializes an intermediate word list.
- 1,000-word grouping is now ~6× faster and allocates ~10× less than 2.1.0; see
  `benchmarks/RESULTS.md` for methodology and before/after numbers.

### Changed

- `MaxSimilarityComparisons` now counts comparisons of **distinct** words; repeated
  occurrences no longer consume the budget.

### Added

- `benchmarks/FuzzyScorer.Benchmarks` (BenchmarkDotNet) and `benchmarks/RESULTS.md`.
- Differential tests (`GroupingEquivalenceTests`) against a reference occurrence-based
  implementation, including forced indexed paths.
- Internal comparison-budget overload for exercising the guard without millions of
  comparisons.

### Tests

- 95 tests (17 new: differential/equivalence and scalability).

## [2.1.0] - 2026-10-23

### Changed behavior

- **Punctuation now separates words.** It is replaced with a space, so `"hello,world"`
  yields two words (`hello`, `world`); previously punctuation was deleted and the
  tokens were glued together.
- **All Unicode whitespace separates words.** Tab, form feed, vertical tab,
  non-breaking space (`U+00A0`), and line/paragraph separators are handled; previously
  only space, `\t`, `\n`, and `\r` acted as separators.
- **Unicode input is normalized to form C (NFC).** Precomposed and decomposed
  diacritics (`"café"` vs `"cafe"` + `U+0301`) are the same word.
- **Apostrophes no longer split words.** They are removed instead
  (`"don't"` → `"dont"`, `"John's"` → `"johns"`).
- **Leading and trailing hyphens are trimmed** (`"--foo--"` → `"foo"`); interior
  hyphens are preserved (`"well-known"`).
- **Deterministic representative on frequency ties.** The group representative is the
  most frequent word; ties are broken by the longest variant, then ordinal order.
  Previously the first occurrence won, so a typo could be reported as the correct form.
- **Each distinct misspelling is reported once** in `FuzzyScorerResult.Errors`;
  `RepetitionCount` aggregates all occurrences. Previously a variant occurring N times
  produced N identical entries.
- **Errors are sorted by line number** (then by text).
- `maxEditDistance` outside 0–50 now throws `ArgumentOutOfRangeException` (was
  `ArgumentException`). It derives from `ArgumentException`, so existing catch blocks
  keep working. The comparison-budget overflow still throws `ArgumentException`.

### Fixed

- `GroupWordsBySimilarity` / `BuildSimilarityGroups` threw `NullReferenceException`
  for a null element in the word collection; they now throw `ArgumentException`.
- `ErrorEntry.LineNumbers` contained duplicate line numbers when a word appeared more
  than once on the same line.

### Tests

- Added `FuzzyScorer.Tests/CorrectnessTests.cs` (21 regression tests); total 78.

## [2.0.0] - 2026-09-28

### Changed (breaking)

- Static list-returning APIs now expose `IReadOnlyList<T>` instead of `List<T>`:
  `GetWordFrequencies`, `GroupSimilarWords`, `GroupWordsBySimilarity`.
- `FuzzyScorerResult` and `ErrorEntry` constructors accept `IEnumerable<T>`.
- `GroupWordsBySimilarity` accepts `IEnumerable<string>` and validates its arguments.
- `AreWordsSimilar` throws `ArgumentNullException` for null input and `ArgumentException`
  for an out-of-range `maxEditDistance`.
- `WordScore` throws `ArgumentNullException` (was `ArgumentException`) for null text.
- Renamed source file `Scorer.cs` to `WordScorer.cs` (type name unchanged).

### Security

- Replaced the full-matrix Levenshtein implementation with a banded, cutoff-aware
  version using `O(min(n, m))` memory.
- Added `WordScorer.MaxSimilarityComparisons` (10,000,000) as a hard budget on pairwise
  edit-distance comparisons; exceeding it now throws `ArgumentException`.
- Moved `maxEditDistance` validation before empty-input early returns.

### Added

- SourceLink, symbol packages (`.snupkg`), and deterministic builds.
- CI workflow (build, format verification, tests) on push and pull requests.
- Community files: `CHANGELOG.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, issue and PR
  templates, Dependabot configuration.
- English `README.md`; Polish documentation moved to `README.pl.md`.

### Fixed

- Removed duplicated text-normalization logic; `FuzzyScorer` now shares the parser with
  the static API, so line tracking and the word-length filter stay consistent.
- Removed the local `./nupkgs` package source from `nuget.config`; fresh clones and CI no
  longer fail restore with **NU1301**.
- Bumped GitHub Actions (`actions/checkout@v7`, `actions/setup-dotnet@v6`).

## [1.1.2] - 2026-08-25

### Changed

- Polish README and package metadata refinements; no code changes.

## [1.1.1] - 2026-07

### Added

- Release-triggered publishing workflow (NuGet and GitHub Packages).

## [1.1.0] - 2026-07-10

### Added

- Instance API `IFuzzyScorer` / `FuzzyScorer` with `ScoreAsync`.
- Typo detection via `FuzzyScorerResult` and `ErrorEntry` (with 1-based line numbers).
- Sensitivity (0.0–1.0) mapping to Levenshtein edit distance.

## [1.0.0]

- Initial public release: `WordScorer` frequency counting and fuzzy grouping.

[Unreleased]: https://github.com/lukaszow/FuzzyScorer/compare/v2.3.0...HEAD
[2.3.0]: https://github.com/lukaszow/FuzzyScorer/compare/v2.2.0...v2.3.0
[2.2.0]: https://github.com/lukaszow/FuzzyScorer/compare/v2.1.0...v2.2.0
[2.1.0]: https://github.com/lukaszow/FuzzyScorer/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/lukaszow/FuzzyScorer/compare/v1.1.2...v2.0.0
[1.1.2]: https://github.com/lukaszow/FuzzyScorer/compare/v1.1.1...v1.1.2
[1.1.0]: https://github.com/lukaszow/FuzzyScorer/compare/v1.0.0...v1.1.0
