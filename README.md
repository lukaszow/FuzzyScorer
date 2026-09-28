# FuzzyScorer

[![NuGet](https://img.shields.io/nuget/v/FuzzyScorer)](https://www.nuget.org/packages/FuzzyScorer)
[![NuGet Downloads](https://img.shields.io/nuget/dt/FuzzyScorer)](https://www.nuget.org/packages/FuzzyScorer)
[![License: MIT](https://img.shields.io/github/license/lukaszow/FuzzyScorer)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/10.0)

> [Polska wersja dokumentacji](README.pl.md)

FuzzyScorer is a .NET library that counts words in noisy text and merges typos with their
correct forms — e.g. `"aple"` and `"Apple"` are counted as `"apple"` — giving accurate
frequencies in a single call.

Use the static `WordScorer` class for quick frequency/similarity analysis, or inject
`IFuzzyScorer` for asynchronous typo detection — no configuration and no dependencies.

## Table of contents

- [Quick start](#quick-start)
- [How it works](#how-it-works)
- [API](#api)
- [Limits & security](#limits--security)
- [FAQ](#faq)
- [License](#license)

## Quick start

### Installation

```bash
dotnet add package FuzzyScorer
```

Requires the [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). The package
ships XML documentation for full IntelliSense.

### Counting words with typo merging

```csharp
using FuzzyScorer;

string text = "apple aple Apple";
var results = WordScorer.GroupSimilarWords(text, maxEditDistance: 1);

foreach (var word in results)
    Console.WriteLine($"{word.Text}: {word.Score}");

// Output:
// apple: 3
```

That is the whole library in one call — no configuration or extra setup.

## How it works

1. **Normalization** — the text is cleaned (characters that are not Unicode letters,
   digits, hyphens, or whitespace are removed) and split into words on whitespace.
2. **Counting** — words are compared case-insensitively.
3. **Merging** — words whose Levenshtein distance is ≤ `maxEditDistance` are grouped, and
   the first occurrence becomes the group representative.

**Note:** FuzzyScorer is structural, not semantic. `"TIGER"` and `"TlGER"` will be merged,
but `"cat"` and `"dog"` never will.

### Use cases

- **Surveys and feedback** — merge typos in survey results (e.g. `"Excelent"` and
  `"Excellent"`) to reveal the real consensus in a word cloud.
- **OCR cleaning** — fix text where `"l"` (lowercase L) is confused with `"I"` (uppercase I)
  in scanned documents.
- **Record deduplication** — detect duplicates such as `"John"` and `"Jon"` in a customer
  database.
- **Spam filtering** — catch obfuscated words that bypass simple filters (e.g. `"M0ney"`,
  `"W4tch"`).

## API

### Static methods (`WordScorer`)

All static methods return immutable `WordScore` instances (word text + score) and never
return `null`; collection results are exposed as `IReadOnlyList<T>`.

#### `GetWordFrequencies(string? text[, CancellationToken ct])`

Exact frequency counting without typo merging (case-insensitive).

```csharp
var results = WordScorer.GetWordFrequencies("Apple apple APPLE");
// results: [ apple: 3 ]
```

#### `GroupSimilarWords(string? text, int maxEditDistance[, CancellationToken ct])`

Frequency counting **plus** typo merging based on Levenshtein distance. Words within
distance ≤ `maxEditDistance` of the first occurrence are merged.

- The **first occurrence** of a word becomes the group representative — results are
  order-dependent.
- `maxEditDistance` must be in the range 0–50.

```csharp
var results = WordScorer.GroupSimilarWords("apple aple apple", maxEditDistance: 1);
// results: [ apple: 3 ]
```

#### `AreWordsSimilar(string a, string b, int maxEditDistance)`

Quickly checks whether `b` is within distance ≤ `maxEditDistance` of `a`
(case-insensitive).

```csharp
bool isTypo = WordScorer.AreWordsSimilar("aple", "apple", maxEditDistance: 1); // true
```

#### `GroupWordsBySimilarity(IEnumerable<string> words, int maxEditDistance, CancellationToken ct)`

Groups a **pre-normalized list of words** (no text parsing). Each inner list is one group;
the first occurrence is the leader.

```csharp
var groups = WordScorer.GroupWordsBySimilarity(
    new List<string> { "apple", "aple", "banana" },
    maxEditDistance: 1,
    CancellationToken.None);
// groups: [ [apple, aple], [banana] ]
```

### Instance methods (`IFuzzyScorer` / `FuzzyScorer`)

DI-friendly asynchronous API with typo detection. Register `IFuzzyScorer` → `FuzzyScorer`
in a DI container, or use `new FuzzyScorer()` directly.

#### `ScoreAsync(string text, double sensitivity, CancellationToken ct)`

```csharp
using FuzzyScorer;

IFuzzyScorer scorer = new FuzzyScorer();
string text = "apple aple apple\nbanana cherry";

var result = await scorer.ScoreAsync(text, sensitivity: 0.02, CancellationToken.None);

Console.WriteLine($"Original words:  {result.OriginalSize}");   // 5
Console.WriteLine($"Compressed:      {result.CompressedSize}"); // 4 (aple merged into apple)

foreach (var error in result.Errors)
    Console.WriteLine($"Typo '{error.ErrorText}' (x{error.RepetitionCount}) on lines: {string.Join(",", error.LineNumbers)}");
// Output:
// Typo 'aple' (x1) on lines: 1
```

`sensitivity` (0.0–1.0) maps linearly to the maximum edit distance:
`maxEditDistance = round(sensitivity × 50)`.

| sensitivity | max edit distance | behaviour |
|---|---|---|
| 0.0 | 0 | exact match only (case-insensitive) |
| 0.02 | 1 | catches typical typos (`"aple"` → `"apple"`) |
| 0.5 | 25 | aggressive merging |
| 1.0 | 50 | maximum fuzziness |

Returns `FuzzyScorerResult`:
- **OriginalSize** — total number of words after normalization
- **CompressedSize** — number of distinct groups after merging
- **Errors** — detected potential typos: every group member that is not the most frequent
  word in its group, reported as an `ErrorEntry` (`ErrorText`, `RepetitionCount`, 1-based
  `LineNumbers`)

### Normalization rules

Before analysis the text is:
- Cleaned of everything that is not a Unicode letter, digit, hyphen, or whitespace
  (`"hello!"` → `"hello"`, `"café"` is preserved, `"well-known"` is preserved,
  `"don't"` → `"dont"`)
- Split into words on whitespace
- Compared case-insensitively

## Limits & security

- Input is validated before processing: max **1,000,000** characters, **10,000** words per
  text, **256** characters per word, and the edit distance is capped at **50**.
- Grouping is bounded by a hard budget of **10,000,000** pairwise edit-distance comparisons
  (`WordScorer.MaxSimilarityComparisons`); exceeding it throws `ArgumentException` instead
  of degrading into unbounded quadratic work.
- The Levenshtein routine is banded, short-circuits on the threshold, and uses
  `O(min(n, m))` memory.
- Words longer than 256 characters are **silently dropped** (not an error).
- The limits are `public const` fields on `WordScorer` (`MaxInputLength`, `MaxWordsPerText`,
  `MaxWordLength`, `MaxEditDistanceLimit`, `MaxSimilarityComparisons`). They are
  compile-time constants and cannot be changed at runtime.
- No unsafe code, no unmanaged memory, and no runtime dependencies beyond the .NET BCL.
- Cancellation: every long-running path accepts a `CancellationToken` — via the static
  method overloads and as a required parameter in `ScoreAsync` /
  `GroupWordsBySimilarity`.
- Results are immutable (`WordScore`, `FuzzyScorerResult`, `ErrorEntry`) — validated at
  construction and read-only afterwards.

See [SECURITY.md](SECURITY.md) for the full threat model and reporting process.

## FAQ

**Does FuzzyScorer understand meaning?**
No. It is structural (Levenshtein), not semantic. `"TIGER"` and `"TlGER"` match;
`"cat"` and `"dog"` never do.

**How do I detect typos?**
Use `IFuzzyScorer.ScoreAsync` — it reports every group member that is not the most frequent
word in its group, with 1-based line numbers.

**What happens to words longer than 256 characters?**
They are silently dropped.

**Is configuration or DI required?**
No. The static methods work out of the box; the instance API is simply
`new FuzzyScorer()`.

## License

MIT — see [LICENSE](LICENSE).
