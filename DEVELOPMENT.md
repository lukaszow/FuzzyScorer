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

### Version 1.2 - Async API & Error Detection (2026-05-29)

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
- All 26 unit tests pass (16 existing + 10 new)
- Build: Clean (0 warnings, 0 errors)

**Migration Guide for Existing Code:**
No action required for existing consumers—all `WordScorer` static methods remain unchanged.
To use the new async API:
```csharp
IFuzzyScorer scorer = new FuzzyScorer();
var result = await scorer.ScoreAsync(inputText, sensitivity: 0.02, CancellationToken.None);
```