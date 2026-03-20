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

1.  **[AI_RULES.md](AI_RULES.md)**: Defines the tech stack (.NET 9.0, C# 13, Console Application), coding standards, and language rules.
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

### Version 1.1 - Security Hardening (2026-02-17)

**Implemented Security Improvements:**

1. **DoS Prevention**
   - Input limits: `MaxWordsPerText = 10,000`, `MaxWordLength = 256 characters`
   - Similarity threshold validation: `MaxSimilarityThreshold = 50`
   - Automatic validation on text entry with clear `ArgumentException` messages

2. **Cancellation Support**
   - All `Scorer.GetScoringWords()` methods now accept `CancellationToken` parameter
   - Prevents indefinite hangs in async/server contexts
   - Backwards compatible: existing code continues to work (uses `CancellationToken.None` default)

3. **Input Normalization**
   - Removed interpunctuation and non-alphanumeric noise via regex normalization
   - Consistent whitespace handling across platforms (handles `\t`, `\n`, `\r`)
   - Uses `ToLowerInvariant()` instead of `ToLower()` for culture-safe operations

4. **Immutable WordScore Objects**
   - Changed `Text` and `Score` properties from settable to read-only
   - Added constructor input validation (null check, non-negative score)
   - Prevents accidental/malicious object mutation after creation

5. **Cleanup**
   - Removed obsolete `App.config` (targeted outdated .NET Framework 4.6.1)
   - Project now strictly targets .NET 9.0

**Testing:**
- All 6 existing unit tests pass without modification
- Zero breaking changes to public API (overloads added, originals remain)
- Build: Clean (0 warnings, 0 errors)

**Migration Guide for Existing Code:**
No action required for existing consumers—all changes are backwards compatible.
To use new features (cancellation), simply pass `CancellationToken`:
```csharp
// Old code (still works)
var results = Scorer.GetScoringWords(text);

// New code with cancellation
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
var results = Scorer.GetScoringWords(text, 1, cts.Token);
```