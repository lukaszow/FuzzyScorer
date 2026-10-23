# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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

[Unreleased]: https://github.com/lukaszow/FuzzyScorer/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/lukaszow/FuzzyScorer/compare/v1.1.2...v2.0.0
[1.1.2]: https://github.com/lukaszow/FuzzyScorer/compare/v1.1.1...v1.1.2
[1.1.0]: https://github.com/lukaszow/FuzzyScorer/compare/v1.0.0...v1.1.0
