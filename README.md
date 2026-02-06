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
You can use the `Scorer.GetScoringWords` static method to analyze text:

```csharp
string text = "Hello world hello again";
var results = Scorer.GetScoringWords(text);

foreach (var result in results)
{
    Console.WriteLine($"{result.Text}: {result.Score}");
}
```

## 📜 Development Rules
All contributors (including AI agents) must follow the rules defined in `AI_RULES.md` and respect the directory structure in `STRUCTURE.md`.

- **PascalCase** for methods and properties.
- **camelCase** for local variables.
- **XML Documentation** required for all public members.
