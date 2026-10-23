# Development Notes & AI Guidelines

This document provides guidelines for developers and AI agents working on FuzzyScorer project.

## 🤖 AI Session Bootstrap

To maintain perfect context and architectural consistency while saving LLM token limits, always start a new AI session (or project-wide task) by pasting the following prompt:

> **Bootstrap Prompt:**
> "Start session. I have created the configuration files (`AI_RULES.md`, `STRUCTURE.md`). Please analyze them first to understand the project architecture, tech stack.
> 
> Then, based on `STRUCTURE.md`, verify or maintain the directory tree. Let me know if you are ready to continue."

## Project Configuration Files

The following files define the core project governance:

1.  **[AI_RULES.md](AI_RULES.md)**: Defines the tech stack (.NET 10.0, C# 13, Library), coding standards, and language rules.
2.  **[STRUCTURE.md](STRUCTURE.md)**: Defines the strict directory hierarchy and data flow (Processing -> State -> Display).
3.  **[SECURITY.md](SECURITY.md)**: Security policy, threat model, usage guidelines, and audit checklist.

## Universal Compatibility

These instructions are intended to be universal. Whether you are using:
- **VS Code with Copilot/Claude Dev/Roo Code**
- **Cursor**
- **Antigravity**
- **Web-based LLMs (ChatGPT, Claude.ai)**

Simply mentioning or pasting the bootstrap prompt ensures the agent is fully aligned with the project's "Business Logic" and "Type Safety" goals.

## 🔒 Security & Changelog

### Version 2.3.0 - Options & Naming (2026-10-23)

Additive release: the options API is new, old property names remain as `[Obsolete]`
aliases. Full details in [`CHANGELOG.md`](CHANGELOG.md).

**Options:**

1. `FuzzyScorerOptions` — edit distance, stricter limits, `MinWordLengthForFuzzy`,
   `IgnoreDiacritics`, `StripEdgeHyphens`, `TieBreak`, `Grouping`.
2. Static methods and `FuzzyScorer` accept options; `IFuzzyScorer` is unchanged apart from
   an optional `CancellationToken`, so existing implementations keep compiling.
3. `TieBreakMode`: `FirstOccurrence`, `LongestThenOrdinal` (default), `ReportAllTies`.
4. `GroupingMode`: `Leader` (default) or `ConnectedComponents` (transitive, pairwise,
   budget-bounded).

**Result enrichments:**

- `FuzzyScorerResult`: `TotalWords`, `UniqueGroups`, `MaxEditDistance`, `WordGroups`.
- `ErrorEntry`: `Text`, `Occurrences`, `SuggestedText`, `IsCertain`.
- `WordGroup`: `Representative`, distinct `Members`, total `Count`.
- `FuzzyScorer.Score(...)` synchronous overloads; `ScoreAsync` accepts a default token.

**Compatibility:**

- Old names (`Score`, `OriginalSize`, `CompressedSize`, `ErrorText`, `RepetitionCount`)
  forward through `[Obsolete]` aliases.
- `sensitivity` now rounds with `MidpointRounding.AwayFromZero`.
- `MinWordLengthForFuzzy` defaults to 0 in 2.3; the planned default is 4 in 3.0.

**Testing:** 114 unit tests; build and `dotnet format` clean (0 warnings).

### Version 2.2.0 - Grouping Performance (2026-10-23)

Performance release; no public API surface changes. Full details in
[`CHANGELOG.md`](CHANGELOG.md) and [`benchmarks/RESULTS.md`](benchmarks/RESULTS.md).

**Grouping:**

1. Distinct words are compared with group leaders once; repeated occurrences reuse the
   assigned group and do not consume `MaxSimilarityComparisons`.
2. Thresholds 1–2 use a deletion-variant index (64-bit hash keys; candidates verified).
3. Threshold 3 uses a BK-tree for inputs ≥ 3,000 words, with subtree length pruning.
4. Larger thresholds keep the linear scan, which is fast when matches are plentiful.
5. The banded Levenshtein routine uses stack/pooled buffers instead of two allocations
   per comparison.

**Counting:**

- `GetWordFrequencies` counts while tokenizing (single pass).
- `FuzzyScorer.ScoreAsync` builds the frequency and line maps in one loop and no longer
  materializes an intermediate word list.

**Results (10,000 unique words, 4–10 characters):**

| Scenario | 2.1.0 | 2.2.0 |
|---|---:|---:|
| `maxEditDistance: 1` | budget exceeded | 32 ms / 11 MB |
| `maxEditDistance: 2` | budget exceeded | 104 ms / 34 MB |

1,000-word grouping is ~6× faster and allocates ~10× less than 2.1.0.

**Testing:** 95 unit tests (differential tests against a reference implementation plus a
10,000-word scalability test); build and `dotnet format` clean (0 warnings).

### Version 2.1.0 - Normalization & Typo-Selection Correctness (2026-10-23)

Behavioral release on top of 2.0.0; no API surface changes. Full details in
[`CHANGELOG.md`](CHANGELOG.md).

**Normalization:**

1. Unicode NFC; punctuation becomes a separator (was deleted), so `"hello,world"`
   now yields two words.
2. All Unicode whitespace splits words (NBSP, `\v`, `\f`, U+2028/2029) — previously
   only space, `\t`, `\n`, `\r` did.
3. Apostrophes are removed without splitting (`"don't"` → `"dont"`).
4. Leading/trailing hyphens are trimmed; interior hyphens preserved.

**Typo detection:**

- Deterministic tie-break: most frequent → longest → ordinal (was: first occurrence).
- One `ErrorEntry` per distinct spelling; `RepetitionCount` aggregates occurrences.
- Errors sorted by line, then text; distinct line numbers per entry.

**Validation:**

- `maxEditDistance` out of range now throws `ArgumentOutOfRangeException`
  (still an `ArgumentException`, so existing handlers keep working).
- Null elements in `GroupWordsBySimilarity` throw `ArgumentException` instead of
  `NullReferenceException`.

**Testing:** 78 unit tests (21 new regression tests in `CorrectnessTests.cs`);
build and `dotnet format` clean (0 warnings).

### Version 2.0.0 - Security Hardening & API Cleanup (2026-09-28)

**Breaking Changes:**

1. **Return types** — static list-returning APIs now expose `IReadOnlyList<T>` instead of
   `List<T>` (`GetWordFrequencies`, `GroupSimilarWords`, `GroupWordsBySimilarity`).
2. **Constructor signatures** — `FuzzyScorerResult` and `ErrorEntry` accept `IEnumerable<T>`.
3. **`GroupWordsBySimilarity`** now takes `IEnumerable<string>` and validates its inputs.
4. **`AreWordsSimilar`** now throws on null arguments and invalid `maxEditDistance`.
5. **`WordScore`** throws `ArgumentNullException` (was `ArgumentException`) for null text.

**Security Fixes:**

- **Banded Levenshtein with cutoff** — replaces the full-matrix implementation; returns
  early when the length difference or minimum row cost exceeds the threshold, and uses
  two rows (`O(min(n,m))` memory) instead of a `(n+1) × (m+1)` matrix.
- **Comparison budget** — new `WordScorer.MaxSimilarityComparisons` (10,000,000) caps
  pairwise comparisons in `BuildSimilarityGroups`; exceeding it throws instead of
  allowing quadratic-time degradation.
- Validation moved before empty-input early returns in `GroupSimilarWords`/`GetWordGroups`.

**Architecture:**

- Single normalization source of truth: `NormalizeAndExtractWordsWithLines` is now used by
  both the static API and `FuzzyScorer.Analyze` (removed the duplicated parser and the
  second regex pass in `BuildWordLineMap`).
- Renamed `Scorer.cs` → `WordScorer.cs` to match the public type.

**Documentation:**

- README translated to English; Polish kept as `README.pl.md`.
- Corrected normalization examples and complexity claims in `SECURITY.md`.

**Testing:**
- 57 unit tests pass (8 new security/validation tests)
- Build: Clean (0 warnings, 0 errors)

### Version 1.1.2 - Packaging & Docs (2026-08-25)

- Polish README and package metadata refinements; no code changes.

### Version 1.1.1 - Release-based Publishing (2026-07)

- Added release-triggered GitHub Packages publishing workflow.

### Version 1.1.0 - Async API & Error Detection (2026-07-10)

**Implemented Features:**

1. **New Instance API (`IFuzzyScorer`)**
   - `FuzzyScorer : IFuzzyScorer` with `Task<FuzzyScorerResult> ScoreAsync(string, double, CancellationToken)`
   - Supports DI and mocking via interface

2. **Error Detection**
   - `FuzzyScorerResult` with `OriginalSize`, `CompressedSize`, `Errors`
   - `ErrorEntry` captures potential typos: `ErrorText`, `RepetitionCount`, `LineNumbers`
   - Sensitivity (0.0–1.0) maps linearly to Levenshtein edit distance

3. **Line Tracking**
   - `ErrorEntry.LineNumbers` provides 1-based line numbers for each detected typo

4. **Internal Refactoring**
   - Extracted `BuildSimilarityGroups()` for reuse between `GroupSimilarWords` and `GetWordGroups`
   - Exposed `WordNormalizationRegex` and `GetWordGroups` as `internal` for `FuzzyScorer`

**Testing:**
- All 49 unit tests pass
- Build: Clean (0 warnings, 0 errors)

**Migration Guide for Existing Code:**
No action required for existing consumers—all `WordScorer` static methods remain unchanged.
To use the new async API:
```csharp
IFuzzyScorer scorer = new FuzzyScorer();
var result = await scorer.ScoreAsync(inputText, sensitivity: 0.02, CancellationToken.None);
```