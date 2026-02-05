# WordsCloud

WordsCloud is a .NET 9.0 console application designed to analyze text data and generate word scoring results. These results are intended to be used for creating word clouds, where the "score" typically represents the frequency of a word in a given text.

## 🎯 Project Goal

The primary objective is to provide a robust engine for:
- Splitting text into individual words.
- Calculating word frequency (scores).
- (Future) Grouping similar words based on **Levenshtein distance** to account for typos or variations.

## 🛠 Technology Stack

- **Platform**: .NET 9.0
- **Language**: C# 13
- **Project Type**: Console Application
- **Key Features**: LINQ for data processing, Null Safety (Nullable enable).

## 🏗 Architecture & Project Structure

The project follows a strictly defined structure as documented in [STRUCTURE.md](STRUCTURE.md).

- **Program.cs**: Contains the entry point and core word scoring logic.
- **WordScore.cs**: A POCO (Plain Old CLR Object) representing a word and its associated score.
- **AI_RULES.md**: Contains specific coding standards and AI-specific guidelines for this project.

## 🚀 How to Use

### Prerequisites
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Running the Application
To run the application and see the sample word analysis:

1. Open a terminal in the project root.
2. Navigate to the project directory:
   ```bash
   cd WordsCloud
   ```
3. Run the application:
   ```bash
   dotnet run
   ```

### Running Tests
To execute the unit tests:
1. Open a terminal in the project root.
2. Run the tests:
   ```bash
   dotnet test
   ```

### Using the Scoring Logic
You can use the `GetScoringWords` static method to analyze arrays of strings:

```csharp
string[] texts = { "Hello world", "hello again" };
var results = Program.GetScoringWords(texts);

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
