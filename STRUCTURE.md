# FuzzyScorer Project Structure & Architecture

This file documents the project structure and architectural patterns for the `FuzzyScorer` project. All agents MUST strictly follow this structure.

## Project Structure

```text
FuzzyScorer/
├── AI_RULES.md                # Project-specific AI guidelines and standards
├── DEVELOPMENT.md             # Development notes and AI guidelines
├── STRUCTURE.md               # This file
├── README.md                  # Project documentation
├── FuzzyScorer.sln             # Visual Studio Solution file
├── FuzzyScorer/                # Main project directory
│   ├── FuzzyScorer.csproj      # .NET 9.0 project file
│   ├── Scorer.cs              # Core scoring logic with security safeguards
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
- **Methods**: Core logic (like word scoring) is currently implemented as static methods in `Program`. In future refactoring, these should be moved to dedicated service classes.

### 3. Namespace Convention
- Use the base namespace `FuzzyScorer` for all files within the main project directory.
- Subdirectories should correspond to sub-namespaces (e.g., `FuzzyScorer.Models`).

### 4. Technical Stack
- Targeted for **.NET 9.0**.
- Uses **C# 13** features.
- Nullability is enabled; follow strict null safety.

## Security-Related Changes (v1.1+)

### Input Limits and Validation
The `Scorer` class now enforces security limits to prevent DoS attacks:
- **MaxWordsPerText**: 10,000 words max
- **MaxWordLength**: 256 characters per word
- **MaxSimilarityThreshold**: 50 for Levenshtein distance

### Normalization
Input text is automatically normalized via `NormalizeAndExtractWords()` method:
- Removes non-alphanumeric characters (preserves letters, digits, hyphens, spaces)
- Trims and validates individual words
- Throws `ArgumentException` if limits are exceeded

### Cancellation Support
All public `GetScoringWords` methods now support `CancellationToken` parameter for controlled operation cancellation in long-running scenarios.

### Immutable WordScore
`WordScore` class properties are read-only after construction with parameter validation:
- `Text` cannot be null
- `Score` cannot be negative

### Removed Files
- **App.config**: Obsolete .NET Framework configuration file (project targets .NET 9.0)
