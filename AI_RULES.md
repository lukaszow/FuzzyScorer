# AI Rules - FuzzyScorer Project

This document defines the coding standards, architectural patterns, and rules for AI contributions to the `FuzzyScorer` project.

## Tech Stack
- **Core**: .NET 9.0
- **Language**: C# 13
- **Project Type**: Console Application

## Coding Standards

### Naming Conventions
- **Classes/Interfaces/Methods/Properties**: Use `PascalCase`.
- **Local Variables/Parameters**: Use `camelCase`.
- **Private Fields**: Use `_camelCase` (e.g., `_wordScores`).

### Documentation
- All public methods and classes MUST have XML documentation comments (`///`).
- Comments should explain the *why* and any non-obvious logic.

### Style & Patterns
- **LINQ**: Prefer LINQ for collection transformations and filtering for better readability.
- **Null Safety**:
    - Project has `<Nullable>enable</Nullable>`.
    - Avoid returning `null` from methods that return collections; return an empty collection instead.
    - Address all `CS8603` (Possible null reference return) warnings.
- **Strings**: Use `StringComparison.OrdinalIgnoreCase` for case-insensitive comparisons where appropriate.

## Domain Specific Logic

### Word Scoring
- The primary goal is to analyze text and produce a set of unique words with frequency-based scores.
- Future implementations will include a similarity-based scoring system using the **Levenshtein distance** algorithm.

### Resource Management
- Ensure all `IDisposable` resources are properly handled with `using` statements or declarations.
