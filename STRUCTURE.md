# FuzzyScorer Project Structure & Architecture

This file documents the project structure and architectural patterns for the `FuzzyScorer` project. All agents MUST strictly follow this structure.

## Project Structure

```text
FuzzyScorer/
├── AI_RULES.md                # Project-specific AI guidelines and standards
├── AGENTS.md                  # AI agent development guide
├── STRUCTURE.md               # This file
├── README.md                  # Project documentation
├── FuzzyScorer.sln             # Visual Studio Solution file
├── FuzzyScorer/                # Main project directory
│   ├── FuzzyScorer.csproj      # .NET 10.0 project file
│   ├── WordScorer.cs          # Core word frequency and similarity logic
│   ├── WordScore.cs           # Data model for word analysis (immutable)
│   └── Properties/            # Project assembly information
└── FuzzyScorer.Tests/          # Unit test project (xUnit)
    ├── FuzzyScorer.Tests.csproj
    └── ScoringTests.cs        # Unit tests for scoring logic
```

## Architecture & Guidelines

### 1. Directory Strictness
- **DO NOT** create new top-level folders without explicit user permission.
- All core logic should reside within the `FuzzyScorer/` subdirectory.
- If the project grows, follow standard .NET conventions (e.g., `Services/`, `Models/`, `Interfaces/`) within the `FuzzyScorer/` directory.

### 2. Implementation Pattern
- **Current State**: A library providing word frequency and similarity scoring.
- **Data Models**: Use simple POCOs like `WordScore` for data representation.
- **Methods**: Core logic is implemented as static methods on `WordScorer`. Future refactoring could extract `IWordScorer` for testability.

### 3. Namespace Convention
- Use the base namespace `FuzzyScorer` for all files within the main project directory.
- Subdirectories should correspond to sub-namespaces (e.g., `FuzzyScorer.Models`).

### 4. Technical Stack
- Targeted for **.NET 10.0**.
- Uses **C# 13** features.
- Nullability is enabled; follow strict null safety.

## Security-Related Features

### Input Limits and Validation
The `WordScorer` class enforces security limits to prevent DoS attacks:
- **MaxWordsPerText**: 10,000 words max
- **MaxWordLength**: 256 characters per word
- **MaxEditDistanceLimit**: 50 for Levenshtein distance

### Normalization
Input text is automatically normalized via `NormalizeAndExtractWords()` method:
- Removes non-alphanumeric characters (preserves letters, digits, hyphens, spaces)
- Trims and validates individual words
- Throws `ArgumentException` if limits are exceeded

### Cancellation Support
All public methods accept `CancellationToken` for controlled operation cancellation in long-running scenarios.

### Immutable WordScore
`WordScore` class properties are read-only after construction with parameter validation:
- `Text` cannot be null
- `Score` cannot be negative
