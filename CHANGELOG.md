# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
