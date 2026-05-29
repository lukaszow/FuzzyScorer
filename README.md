# FuzzyScorer

## 1. The Problem

Raw text from users, surveys, or OCR is full of noisy variants — `"Excellent"`, `"Excelent"`, `"excelent"`, `"Excelleent"`. A naive word counter treats each as a separate word, fragmenting your frequency counts. You either live with the noise or write fragile custom normalization.

FuzzyScorer solves this: it gives you accurate word counts by merging exact duplicates (case-insensitive) and structurally similar variants (via Levenshtein distance) in a single call.

**Structural similarity, not semantic.** Unlike AI models that understand meaning (knowing "cat" and "dog" are both pets), FuzzyScorer looks at how a word is *built* — so `"TIGER"` and `"TlGER"` (a common OCR error) are recognized as the same word, even though no semantic model would confuse them.

### Real-World Use Cases

- **Live Event Feedback**: Merge typos in survey results (e.g., `"Excelent"` and `"Excellent"`) to show true consensus in word clouds.
- **OCR Data Cleaning**: Repair text where `"l"` (lowercase L) is mistaken for `"I"` (capital I) in scanned documents.
- **Customer Record Matching**: Identify duplicates like `"John Smith"` and `"Jon Smith"`.
- **Spam Filtering**: Catch obfuscated words designed to bypass simple filters (e.g., `"M0ney"`, `"W4tch"`).

## 2. How to Use

### Installation

```bash
dotnet add package FuzzyScorer --version 1.0.0
```

The package includes XML documentation files for full IntelliSense support.

### Usage

#### Quick Analysis (Static API)

```csharp
using FuzzyScorer;

string text = "apple aple Apple";
var results = WordScorer.GroupSimilarWords(text, maxEditDistance: 1);

foreach (var word in results)
    Console.WriteLine($"{word.Text}: {word.Score}");

// Output:
// apple: 3
```

`GroupSimilarWords` is the primary entry point for static usage — it handles both exact case-insensitive grouping and fuzzy matching. For word-frequency-only scenarios, use `WordScorer.GetWordFrequencies(text)`.

#### Async Analysis with Error Detection (Instance API)

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

`ScoreAsync` returns a `FuzzyScorerResult` with:
- **OriginalSize** — total word count
- **CompressedSize** — unique groups after fuzzy merging
- **Errors** — detected potential typos (words that differ from the most frequent word in their similarity group)

### Running Tests

```bash
dotnet test
```

Requires [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

## 3. Technology

### Safety

- All input validated before processing: max 1,000,000 characters, 10,000 words, 256 characters per word, edit distance capped at 50.
- Input is normalized — non-alphanumeric characters (except hyphens and whitespace) are stripped.
- No unsafe code, no unmanaged memory, no external runtime dependencies beyond .NET BCL.
- `CancellationToken` accepted on every public method for graceful cancellation.
- `WordScore` is immutable — validated at construction, read-only thereafter.

### Stability

- .NET 10.0 with nullable reference types enabled.
- 100% of public API covered by xUnit tests (26 tests, all passing).
- Every public member has XML documentation.
- All public methods are pure and stateless — no mutable shared state, no thread-safety concerns.

### Flexibility

- Three operations: exact frequency counting (`GetWordFrequencies`), fuzzy similarity grouping (`GroupSimilarWords`), and async analysis with error detection (`ScoreAsync`).
- `WordScore` and `ErrorEntry` are simple immutable POCOs — trivially mapped to JSON, DTOs, or database rows.
- No IoC container or configuration required — drop in and call.

### Extensibility

- Instance API via `IFuzzyScorer` interface for testability and DI — static `WordScorer` facade preserved for backward compatibility.
- Levenshtein implementation is private — can be replaced or optimized without affecting callers.
- Security limits are constants, not hardcoded magic numbers — adjustable without changing behavior.
