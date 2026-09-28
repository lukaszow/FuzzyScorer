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