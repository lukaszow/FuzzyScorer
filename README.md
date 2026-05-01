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

```csharp
using FuzzyScorer;

string text = "apple aple Apple";
var results = WordScorer.GroupSimilarWords(text, maxEditDistance: 1);

foreach (var word in results)
    Console.WriteLine($"{word.Text}: {word.Score}");

// Output:
// apple: 3
```

`GroupSimilarWords` is the primary entry point — it handles both exact case-insensitive grouping and fuzzy matching. For word-frequency-only scenarios, use `WordScorer.GetWordFrequencies(text)`.

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
- 100% of public API covered by xUnit tests (16 tests, all passing).
- Every public member has XML documentation.
- All public methods are pure and stateless — no mutable shared state, no thread-safety concerns.

### Flexibility

- Two orthogonal operations exposed as separate methods: exact frequency counting and fuzzy similarity grouping.
- `WordScore` is a simple immutable POCO — trivially mapped to JSON, DTOs, or database rows.
- No IoC container or configuration required — drop in and call.

### Extensibility

- Static facade can be wrapped in an `IWordScorer` interface or extended with instance-based options without breaking the existing API surface.
- Levenshtein implementation is private — can be replaced or optimized without affecting callers.
- Security limits are constants, not hardcoded magic numbers — adjustable without changing behavior.
