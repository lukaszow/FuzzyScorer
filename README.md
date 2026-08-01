# FuzzyScorer

[![NuGet](https://img.shields.io/nuget/v/FuzzyScorer)](https://www.nuget.org/packages/FuzzyScorer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FuzzyScorer)](https://www.nuget.org/packages/FuzzyScorer)
[![License: MIT](https://img.shields.io/github/license/lukaszow/FuzzyScorer)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

FuzzyScorer is a .NET NuGet library that counts words in messy text and merges typos with their correct forms — so "aple" and "Apple" both count as "apple" — giving you accurate frequencies in one call.

Use the static WordScorer for fire-and-forget frequency/similarity analysis, or inject IFuzzyScorer for async typo detection — no config, no dependencies.

## Table of Contents

- [The Problem](#the-problem)
- [Quick Start](#quick-start)
- [API Reference](#api-reference)
- [Security & Input Limits](#security--input-limits)
- [Quality](#quality)
- [FAQ](#faq)
- [License](#license)

## The Problem

Raw text from users, surveys, or OCR is full of noisy variants — `"Excellent"`, `"Excelent"`, `"excelent"`, `"Excelleent"`. A naive word counter treats each as a separate word, fragmenting your frequency counts. You either live with the noise or write fragile custom normalization.

FuzzyScorer gives you accurate word counts by merging exact duplicates (case-insensitive) and structurally similar variants (via Levenshtein edit distance) in a single call.

**Structural similarity, not semantic.** Unlike AI models that understand meaning (knowing "cat" and "dog" are both pets), FuzzyScorer looks at how a word is *built* — so `"TIGER"` and `"TlGER"` (a common OCR error) are recognized as the same word, even though no semantic model would confuse them.

### Real-World Use Cases

- **Live Event Feedback**: Merge typos in survey results (e.g., `"Excelent"` and `"Excellent"`) to show true consensus in word clouds.
- **OCR Data Cleaning**: Repair text where `"l"` (lowercase L) is mistaken for `"I"` (capital I) in scanned documents.
- **Word-Level Deduplication**: Spot duplicate entries like `"John"` and `"Jon"` in customer records.
- **Spam Filtering**: Catch obfuscated words designed to bypass simple filters (e.g., `"M0ney"`, `"W4tch"`).

## Quick Start

### Installation

```bash
dotnet add package FuzzyScorer
```

Requires [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The package includes XML documentation files for full IntelliSense support.

### Count words with typo merging

```csharp
using FuzzyScorer;

string text = "apple aple Apple";
var results = WordScorer.GroupSimilarWords(text, maxEditDistance: 1);

foreach (var word in results)
    Console.WriteLine($"{word.Text}: {word.Score}");

// Output:
// apple: 3
```

That's the whole library in one call — no config, no setup.

## API Reference

### Static API (`WordScorer`)

All static methods return immutable `WordScore` instances (word text + score) and never return `null`.

#### `GetWordFrequencies(string? text[, CancellationToken ct])`

Exact case-insensitive frequency counting. No fuzzy merging.

```csharp
var results = WordScorer.GetWordFrequencies("Apple apple APPLE");
// results: [ apple: 3 ]
```

#### `GroupSimilarWords(string? text, int maxEditDistance[, CancellationToken ct])`

Case-insensitive counting **plus** fuzzy merging via Levenshtein edit distance. Words within `maxEditDistance` edits of the group's first occurrence are merged.

- The **first occurrence** of a word becomes the group's representative — results depend on input order.
- `maxEditDistance` must be between 0 and 50.

```csharp
var results = WordScorer.GroupSimilarWords("apple aple apple", maxEditDistance: 1);
// results: [ apple: 3 ]
```

#### `AreWordsSimilar(string a, string b, int maxEditDistance)`

Quick boolean check — is `b` within `maxEditDistance` edits of `a`? Case-insensitive.

```csharp
bool isTypo = WordScorer.AreWordsSimilar("aple", "apple", maxEditDistance: 1); // true
```

#### `GroupWordsBySimilarity(List<string> words, int maxEditDistance, CancellationToken ct)`

Groups a **pre-normalized word list** into clusters (no text parsing or normalization applied). Each inner list is one group; the first occurrence is the leader. Use it when you already have tokens.

```csharp
var groups = WordScorer.GroupWordsBySimilarity(
    new List<string> { "apple", "aple", "banana" },
    maxEditDistance: 1,
    CancellationToken.None);
// groups: [ [apple, aple], [banana] ]
```

### Instance API (`IFuzzyScorer` / `FuzzyScorer`)

DI-friendly async API with typo detection. Register `IFuzzyScorer` → `FuzzyScorer` in your container, or use `new FuzzyScorer()` directly.

#### `ScoreAsync(string text, double sensitivity, CancellationToken ct)`

```csharp
using FuzzyScorer;

IFuzzyScorer scorer = new FuzzyScorer();
string text = "apple aple apple\nbanana cherry";

var result = await scorer.ScoreAsync(text, sensitivity: 0.02, CancellationToken.None);

Console.WriteLine($"Original words: {result.OriginalSize}");   // 5
Console.WriteLine($"Compressed:     {result.CompressedSize}");  // 4 (aple merged with apple)

foreach (var error in result.Errors)
    Console.WriteLine($"Typo '{error.ErrorText}' (x{error.RepetitionCount}) on lines: {string.Join(",", error.LineNumbers)}");
// Output:
// Typo 'aple' (x1) on lines: 1
```

`sensitivity` (0.0–1.0) maps linearly to the max edit distance: `maxEditDistance = round(sensitivity × 50)`.

| sensitivity | max edit distance | behavior |
|---|---|---|
| 0.0 | 0 | exact match only (case-insensitive) |
| 0.02 | 1 | catches common typos (`"aple"` → `"apple"`) |
| 0.5 | 25 | aggressive merging |
| 1.0 | 50 | maximum fuzziness |

Returns a `FuzzyScorerResult`:
- **OriginalSize** — total word count after normalization
- **CompressedSize** — unique groups after fuzzy merging
- **Errors** — detected potential typos: every group member that is not the group's most frequent word, reported as `ErrorEntry` (`ErrorText`, `RepetitionCount`, 1-based `LineNumbers`)

### Normalization Rules

Before any analysis, input text is:
- Stripped of everything that is **not** a Unicode letter, Unicode digit, hyphen, or whitespace (`"hello!"` → `"hello"`, `"café"` stays intact, `"well-known"` stays intact)
- Split into words on whitespace
- Matched case-insensitively

## Security & Input Limits

- Input is validated before processing: max **1,000,000** characters, **10,000** words per text, **256** characters per word, edit distance capped at **50**.
- Words longer than 256 characters are **silently dropped** (not an error).
- Limits are public constants on `WordScorer` (`MaxInputLength`, `MaxWordsPerText`, `MaxWordLength`, `MaxEditDistanceLimit`) — adjustable, not magic numbers.
- No unsafe code, no unmanaged memory, no dependencies beyond the .NET BCL.
- Cancellation: every long-running path accepts a `CancellationToken` — via overloads on the static methods and as a required parameter on `ScoreAsync` / `GroupWordsBySimilarity`.
- Results are immutable (`WordScore`, `FuzzyScorerResult`, `ErrorEntry`) — validated at construction, read-only thereafter.

## Quality

- .NET 10.0 with nullable reference types enabled.
- 49 xUnit tests, all passing; public API covered.
- Every public member has XML documentation (IntelliSense).
- Pure, stateless static methods and a stateless instance — safe for concurrent use.
- Zero external runtime dependencies; results are trivially serializable to JSON, DTOs, or database rows.

## FAQ

**Does FuzzyScorer understand meaning?**
No. It's structural (Levenshtein), not semantic. `"TIGER"` and `"TlGER"` match; `"cat"` and `"dog"` never do.

**How do I detect typos?**
Use `IFuzzyScorer.ScoreAsync` — it reports every group member that isn't the group's most frequent word, with 1-based line numbers.

**What happens to words over 256 characters?**
They are dropped silently.

**Is any configuration or DI setup required?**
No. Static methods work out of the box; the instance API is just `new FuzzyScorer()`.

## License

MIT — see [LICENSE](LICENSE).
