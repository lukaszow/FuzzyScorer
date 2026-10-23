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
- [Options](#options-fuzzyscoreroptions)
- [Limits & security](#limits--security)
- [Performance](#performance)
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
    Console.WriteLine($"{word.Text}: {word.Count}");

// Output:
// apple: 3
```

That is the whole library in one call — no configuration or extra setup.

## How it works

1. **Normalization** — the text is normalized to Unicode form C, punctuation is
   replaced with spaces, and words are split on any Unicode whitespace; apostrophes
   are removed and edge hyphens trimmed.
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

All static methods return immutable `WordScore` instances (word text + count) and never
return `null`; collection results are exposed as `IReadOnlyList<T>`. Every method also
has an overload accepting [`FuzzyScorerOptions`](#options-fuzzyscoreroptions).

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
- Pass `FuzzyScorerOptions` to control the threshold, minimum fuzzy length, diacritics,
  tie-breaking, and grouping mode.

```csharp
var results = WordScorer.GroupSimilarWords("apple aple apple", maxEditDistance: 1);
// results: [ apple: 3 ]
```

#### `AreWordsSimilar(string a, string b, int maxEditDistance)`

Quickly checks whether `b` is within distance ≤ `maxEditDistance` of `a`
(case-insensitive). Throws `ArgumentNullException` for null input and
`ArgumentOutOfRangeException` when `maxEditDistance` is outside 0–50.

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

### Options (`FuzzyScorerOptions`)

Every static method (and the `FuzzyScorer` class) accepts an optional options object:

| Property | Default | Meaning |
|---|---|---|
| `MaxEditDistance` | `1` | Levenshtein threshold (0–50) |
| `MaxInputLength` | `1,000,000` | Stricter values only; cannot exceed the hard cap |
| `MaxWordsPerText` | `10,000` | Stricter values only |
| `MaxWordLength` | `256` | Stricter values only; longer words are dropped |
| `MinWordLengthForFuzzy` | `0` | Shorter words merge only with exact matches (planned default 4 in 3.0) |
| `IgnoreDiacritics` | `false` | Remove diacritics (`"café"` matches `"cafe"`) |
| `StripEdgeHyphens` | `true` | Trim leading/trailing hyphens |
| `TieBreak` | `LongestThenOrdinal` | `FirstOccurrence`, `LongestThenOrdinal`, or `ReportAllTies` |
| `Grouping` | `Leader` | `Leader` or `ConnectedComponents` (transitive) |

```csharp
var options = new FuzzyScorerOptions
{
    MaxEditDistance = 1,
    MinWordLengthForFuzzy = 4,
    IgnoreDiacritics = true,
    Grouping = GroupingMode.ConnectedComponents,
};

var results = WordScorer.GroupSimilarWords(text, options);
```

Options limits can only make processing stricter; values above the hard limits declared
on `WordScorer` throw `ArgumentOutOfRangeException`.

### Instance methods (`FuzzyScorer` / `IFuzzyScorer`)

DI-friendly API with typo detection. `IFuzzyScorer` exposes the asynchronous
`ScoreAsync(text, sensitivity, ct)`; `FuzzyScorer` additionally exposes a synchronous
`Score(...)` and options-based overloads.

#### `Score` / `ScoreAsync`

```csharp
FuzzyScorer scorer = new FuzzyScorer();
string text = "apple aple apple\nbanana cherry";

// sensitivity-based
var result = await scorer.ScoreAsync(text, sensitivity: 0.02);
var same = scorer.Score(text, sensitivity: 0.02);

// options-based
var custom = await scorer.ScoreAsync(text, new FuzzyScorerOptions
{
    MaxEditDistance = 1,
    MinWordLengthForFuzzy = 4,
});

Console.WriteLine($"Total words:   {result.TotalWords}");     // 5
Console.WriteLine($"Unique groups: {result.UniqueGroups}");   // 4 (aple merged into apple)

foreach (var error in result.Errors)
    Console.WriteLine($"Typo '{error.Text}' (x{error.Occurrences}) -> '{error.SuggestedText}' on lines: {string.Join(",", error.LineNumbers)}");
// Output:
// Typo 'aple' (x1) -> 'apple' on lines: 1
```

`sensitivity` (0.0–1.0) maps to the maximum edit distance using
`Math.Round(sensitivity × 50, MidpointRounding.AwayFromZero)`.

| sensitivity | max edit distance | behaviour |
|---|---|---|
| 0.0 | 0 | exact match only (case-insensitive) |
| 0.02 | 1 | catches typical typos (`"aple"` → `"apple"`) |
| 0.5 | 25 | aggressive merging |
| 1.0 | 50 | maximum fuzziness |

Returns `FuzzyScorerResult`:
- **TotalWords** — total number of words after normalization
- **UniqueGroups** — number of distinct groups after merging
- **MaxEditDistance** — the threshold used for the run
- **WordGroups** — one `WordGroup` per group (`Representative`, distinct `Members`, total `Count`)
- **Errors** — detected potential typos: every distinct group member that is not the
  group's representative, reported as an `ErrorEntry` (`Text`, `Occurrences`,
  `LineNumbers`, `SuggestedText`, `IsCertain`), sorted by line. `SuggestedText` is the
  representative, and `IsCertain` is `false` when several variants tie for the top
  frequency. With `TieBreakMode.ReportAllTies`, every tied variant is reported with
  `SuggestedText = null`.
- The 2.2.0 names (`OriginalSize`, `CompressedSize`, `ErrorText`, `RepetitionCount`,
  `WordScore.Score`) remain as `[Obsolete]` aliases through 2.3.x.

### Normalization rules

Before analysis the text is:
- Normalized to Unicode **form C** (canonical composition) — `"café"` written with a
  combining accent matches the precomposed form
- Optionally stripped of diacritics when `IgnoreDiacritics = true` (`"café"` → `"cafe"`)
- Stripped of control and punctuation characters, which are replaced with spaces
  (`"hello,world"` → `"hello world"`, `"foo.bar"` → `"foo bar"`); apostrophes are
  removed without splitting the word (`"don't"` → `"dont"`, `"John's"` → `"johns"`)
- Split into words on **any** Unicode whitespace (spaces, tabs, non-breaking spaces,
  line/paragraph separators, …)
- Trimmed of leading/trailing hyphens (unless `StripEdgeHyphens = false`); interior
  hyphens are preserved (`"well-known"` is preserved)
- Compared case-insensitively

## Limits & security

- Input is validated before processing: max **1,000,000** characters, **10,000** words per
  text, **256** characters per word, and the edit distance is capped at **50**.
- `FuzzyScorerOptions` can enforce stricter limits; values above the hard caps throw
  `ArgumentOutOfRangeException`.
- Grouping is bounded by a hard budget of **10,000,000** pairwise edit-distance comparisons
  (`WordScorer.MaxSimilarityComparisons`); exceeding it throws `ArgumentException` instead
  of degrading into unbounded quadratic work.
- Each **distinct** word is compared with the group leaders once; repeated occurrences
  reuse the assigned group and do not consume the comparison budget.
- Thresholds **1–2** use a deletion-variant index, threshold **3** uses a BK-tree for
  large inputs, and larger thresholds use the linear scan with its early exit.
- The Levenshtein routine is banded, short-circuits on the threshold, and uses
  `O(min(n, m))` memory with pooled buffers.
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

## Performance

Measured with BenchmarkDotNet on unique words of 4–10 characters (see
[`benchmarks/RESULTS.md`](benchmarks/RESULTS.md) for methodology and before/after):

| Scenario | Time | Allocated |
|---|---:|---:|
| `GroupSimilarWords`, 10,000 words, `maxEditDistance: 1` | 32 ms | 11 MB |
| `GroupSimilarWords`, 10,000 words, `maxEditDistance: 2` | 104 ms | 34 MB |
| `GroupSimilarWords`, 1,000 words, `maxEditDistance: 1` | 1.9 ms | 1.2 MB |
| `GetWordFrequencies`, 10,000 words | 3.3 ms | 2.6 MB |

Before 2.2.0 the 10,000-word grouping cases exceeded the comparison budget and threw
`ArgumentException`. Run the suite yourself:

```bash
dotnet run -c Release --project benchmarks/FuzzyScorer.Benchmarks
```

## FAQ

**Does FuzzyScorer understand meaning?**
No. It is structural (Levenshtein), not semantic. `"TIGER"` and `"TlGER"` match;
`"cat"` and `"dog"` never do.

**How do I detect typos?**
Use `IFuzzyScorer.ScoreAsync` — it reports every distinct spelling that is not the
group's representative (most frequent, then longest, then ordinal), with 1-based line
numbers.

**What happens to words longer than 256 characters?**
They are silently dropped.

**Is configuration or DI required?**
No. The static methods work out of the box; the instance API is simply
`new FuzzyScorer()`.

## License

MIT — see [LICENSE](LICENSE).
