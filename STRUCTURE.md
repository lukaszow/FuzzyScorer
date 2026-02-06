# FuzzyScorer Project Structure & Architecture

This file documents the project structure and architectural patterns for the `FuzzyScorer` project. All agents MUST strictly follow this structure.

## Project Structure

```text
FuzzyScorer/
├── AI_RULES.md                # Project-specific AI guidelines and standards
├── STRUCTURE.md               # This file
├── FuzzyScorer.sln             # Visual Studio Solution file
├── FuzzyScorer/                # Main project directory
│   ├── FuzzyScorer.csproj      # .NET 9.0 project file
│   ├── App.config             # Application configuration
│   ├── Scorer.cs              # Core scoring logic
│   ├── WordScore.cs           # Data model for word analysis
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
