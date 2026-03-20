# FuzzyScorer

FuzzyScorer is a .NET 9.0 class library designed to analyze text data and generate word scoring results. These results are intended to be used for creating word clouds, where the "score" typically represents the frequency of a word in a given text.

## 🎯 Project Goal

The primary objective is to provide a robust engine for:
- Splitting text into individual words.
- Calculating word frequency (scores).
- Grouping similar words based on **Levenshtein distance** to account for typos or variations.

## 🧠 Why Levenshtein? The "Spellchecker" vs. The "Philosopher"

While modern AI models (LLMs) act as **Philosophers**, understanding the *meaning* (semantic similarity) of words—knowing that "Cat" and "Dog" are both pets—**FuzzyScorer** acts as a **Spellchecker**. 

We prioritize **structural similarity**. Instead of asking what a word *means*, we ask how it is *built*. This allows the engine to recognize that "TIGER" and "TlGER" are likely the same word, even if an AI might get confused by the visual typo.

### 🌍 Real-World Use Cases

*   **Live Event Feedback**: Merging typos in live survey results (e.g., "Excelent" and "Excellent") to show a true consensus in word clouds.
*   **OCR Data Cleaning**: Automatically repairing text from scanned documents where "l" (small L) is often mistaken for "I" (capital I).
*   **Customer Record Matching**: Identifying duplicate entries in databases like "John Smith" and "Jon Smith".
*   **Spam Filtering**: Catching "obfuscated" words designed to bypass simple filters (e.g., "M0ney" or "W4tch").
*   **Bio-informatics**: Measuring mutation distances between DNA sequences represented as strings of characters.


## 🛠 Technology Stack

- **Platform**: .NET 9.0
- **Language**: C# 13
- **Project Type**: Library
- **Key Features**: LINQ for data processing, Null Safety (Nullable enable).

## 🔒 Security & DoS Protection

FuzzyScorer includes built-in safeguards to prevent denial-of-service attacks and ensure safe operation in server environments:

- **Input Limits**:
  - Maximum 10,000 words per text (`MaxWordsPerText`)
  - Maximum word length of 256 characters (`MaxWordLength`)
  - Similarity threshold capped at 50 (`MaxSimilarityThreshold`)
  
- **Input Normalization**: Removes non-alphanumeric characters (except spaces/hyphens) and invalid words before processing.

- **Cancellation Support**: All scoring methods accept optional `CancellationToken` for graceful operation cancellation in async contexts.

- **Immutable Objects**: `WordScore` objects are read-only after construction with validation.

- **No Hardcoded Secrets**: Project contains no API keys, tokens, or sensitive data.

## 🏗 Architecture & Project Structure

The project follows a strictly defined structure as documented in [STRUCTURE.md](STRUCTURE.md).

- **Scorer.cs**: Contains the core word scoring and similarity logic.
- **WordScore.cs**: A POCO (Plain Old CLR Object) representing a word and its associated score.
- **AI_RULES.md**: Contains specific coding standards and AI-specific guidelines for this project.

## 🚀 How to Use

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Running Tests
To execute the unit tests:
1. Open a terminal in the project root.
2. Run the tests:
   ```bash
   dotnet test
   ```

### Using the Scoring Logic

#### Basic Word Frequency Scoring
Analyze text and get word frequencies (case-insensitive):

```csharp
string text = "Hello world hello again";
var results = Scorer.GetScoringWords(text);

foreach (var result in results)
{
    Console.WriteLine($"{result.Text}: {result.Score}");
}
// Output:
// hello: 2
// world: 1
// again: 1
```

#### Fuzzy Matching with Levenshtein Distance
Group similar words (e.g., handle typos) using a similarity threshold:

```csharp
string text = "Hello helo hallo world wor1d";
int similarity = 1; // Allow up to 1 character difference
var results = Scorer.GetScoringWords(text, similarity);

foreach (var result in results)
{
    Console.WriteLine($"{result.Text}: {result.Score}");
}
// Output:
// Hello: 3   (groups "Hello", "helo", "hallo")
// world: 2   (groups "world", "wor1d")
```

#### With Cancellation Token (Async Operations)
For long-running operations or server contexts, provide a `CancellationToken`:

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
string largeText = /* ... large input ... */;

try
{
    var results = Scorer.GetScoringWords(largeText, cts.Token);
    // Process results
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation was cancelled after 5 seconds.");
}
```

#### Error Handling
Input exceeding limits raises `ArgumentException`:

```csharp
try
{
    string hugeSizedText = string.Join(" ", Enumerable.Range(0, 20000).Select(i => $"word{i}"));
    var results = Scorer.GetScoringWords(hugeSizedText);
}
catch (ArgumentException ex)
{
    Console.WriteLine($"Input validation failed: {ex.Message}");
    // "Input contains 20000 words, exceeding limit of 10000"
}
```

## 📜 Development Rules
All contributors (including AI agents) must follow the rules defined in `AI_RULES.md` and respect the directory structure in `STRUCTURE.md`.

- **PascalCase** for methods and properties.
- **camelCase** for local variables.
- **XML Documentation** required for all public members.

## 🔐 Security

For detailed information on security features, threat model, and best practices, see [SECURITY.md](SECURITY.md).
