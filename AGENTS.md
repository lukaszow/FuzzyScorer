# AGENTS.md - FuzzyScorer Development Guide

This file provides guidelines for AI agents operating in the FuzzyScorer repository.

## Project Overview

- **Platform**: .NET 10.0
- **Language**: C# 13
- **Project Type**: Library
- **Test Framework**: xUnit

## Build Commands

```bash
# Build the entire solution
dotnet build

# Build in Release mode
dotnet build --configuration Release

# Build a specific project
dotnet build FuzzyScorer/FuzzyScorer.csproj
```

## Test Commands

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --verbosity normal

# Run a single test by name
dotnet test --filter "FullyQualifiedName~ScoringTests.Frequency_TypicalCase_ShouldCountCorrectly"

# Run tests with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Code Style Guidelines

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Classes/Interfaces | PascalCase | `Scorer`, `WordScore` |
| Methods/Properties | PascalCase | `GetScoringWords`, `Score` |
| Local Variables | camelCase | `inputText`, `targetSimilarity` |
| Private Fields | _camelCase | `_wordScores`, `_maxInputLength` |
| Constants | PascalCase | `MaxWordsPerText`, `MaxInputLength` |

### Documentation Requirements

- **All public members MUST have XML documentation comments** (`///`)
- Document the purpose, parameters, return values, and exceptions
- Use `<summary>` for overview, `<param>` for parameters, `<returns>` for return values, `<exception>` for thrown exceptions

```csharp
/// <summary>
/// Analyzes the provided text by breaking it down into individual words.
/// </summary>
/// <param name="inputText">The raw text you want to analyze.</param>
/// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
/// <returns>A list of WordScore objects.</returns>
/// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
```

### Null Safety

- Project has `<Nullable>enable</Nullable>` in .csproj
- **Never return `null` from methods that return collections** - return an empty collection instead
- Address all `CS8603` (Possible null reference return) warnings
- Use nullable reference types (`string?`) for potentially null parameters

### LINQ Usage

- Prefer LINQ for collection transformations and filtering
- Chain methods for better readability

```csharp
// Good
return normalizedWords
    .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
    .Select(group => new WordScore(group.Key, group.Count()))
    .ToList();

// Avoid
var result = new List<WordScore>();
foreach (var word in normalizedWords)
{
    // ...
}
```

### String Comparisons

- Use `StringComparison.OrdinalIgnoreCase` for case-insensitive comparisons
- Use `ToLowerInvariant()` instead of `ToLower()` for culture-safe operations

### Error Handling

- Throw `ArgumentException` for invalid input with descriptive messages
- Include parameter name in exception messages using `nameof()`
- Support `CancellationToken` for all long-running operations
- Catch `OperationCanceledException` to allow graceful cancellation

```csharp
if (targetSimilarity < 0 || targetSimilarity > MaxSimilarityThreshold)
    throw new ArgumentException($"targetSimilarity must be between 0 and {MaxSimilarityThreshold}", nameof(targetSimilarity));
```

### Security Limits (DoS Prevention)

The `Scorer` class enforces these limits:

| Limit | Value | Purpose |
|-------|-------|---------|
| MaxInputLength | 1,000,000 | Max characters per input |
| MaxWordsPerText | 10,000 | Max words per text |
| MaxWordLength | 256 | Max characters per word |
| MaxSimilarityThreshold | 50 | Max Levenshtein distance |

## Project Structure

```
FuzzyScorer/
├── FuzzyScorer.sln              # Solution file
├── FuzzyScorer/                 # Main library project
│   ├── FuzzyScorer.csproj
│   ├── Scorer.cs              # Core scoring logic
│   └── WordScore.cs            # Data model (POCO)
└── FuzzyScorer.Tests/          # Unit test project
    ├── FuzzyScorer.Tests.csproj
    └── ScoringTests.cs        # xUnit tests
```

### Namespace Convention

- Use base namespace `FuzzyScorer` for all files in the main project
- Use `FuzzyScorer.Tests` for test files

### Adding New Files

- Place core logic in `FuzzyScorer/` directory
- Place tests in `FuzzyScorer.Tests/` directory
- Follow existing file naming conventions (PascalCase.cs)
- Do NOT create new top-level folders without explicit permission

## Immutability

- `WordScore` is immutable - properties have no setters
- Validate constructor parameters (null check for text, non-negative for score)
- Consider making new data classes immutable as well

## Resource Management

- Use `using` statements or declarations for `IDisposable` resources
- Static readonly fields for compiled regex patterns

```csharp
private static readonly Regex WordNormalizationRegex = new Regex(@"[^\p{L}\p{N}\s-]", RegexOptions.Compiled);
```

## Code Review Checklist

Before completing any code change:

- [ ] All public methods have XML documentation
- [ ] No `CS8603` warnings (possible null reference return)
- [ ] Null/empty inputs return empty collections, not null
- [ ] LINQ preferred for collection operations
- [ ] Security limits are enforced on new methods
- [ ] `CancellationToken` supported for long operations
- [ ] Tests pass: `dotnet test`
- [ ] Build succeeds: `dotnet build`
