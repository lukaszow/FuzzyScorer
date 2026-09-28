# Contributing to FuzzyScorer

Thanks for your interest in improving FuzzyScorer. This document describes the development
workflow and the standards this project follows.

## Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting started

```bash
git clone https://github.com/lukaszow/FuzzyScorer.git
cd FuzzyScorer
dotnet build
dotnet test
```

## Before opening a pull request

Run all of the following and make sure they pass:

```bash
dotnet format --verify-no-changes
dotnet build --configuration Release
dotnet test --configuration Release
```

- The build treats analyzer warnings as errors (`TreatWarningsAsErrors`).
- Every public member must have XML documentation (`GenerateDocumentationFile` is on).
- New behavior should come with tests in `FuzzyScorer.Tests/ScoringTests.cs`.
- Keep the public API surface immutable and null-safe.

## Coding standards

See [`AI_RULES.md`](AI_RULES.md) and [`STRUCTURE.md`](STRUCTURE.md). In short:

- PascalCase for public members, `_camelCase` for private fields, camelCase for locals.
- Prefer LINQ for collection transformations.
- Use `StringComparer.OrdinalIgnoreCase` / `ToLowerInvariant()` for culture-safe comparisons.
- Never return `null` from collection-returning methods.

## Commit messages

Use clear, imperative messages. [Conventional Commits](https://www.conventionalcommits.org/)
style is preferred, e.g. `fix: bound similarity grouping complexity`.

## Reporting bugs and security issues

- Functional bugs and feature requests: open a GitHub issue using the provided templates.
- **Security vulnerabilities: do not open a public issue.** Follow [`SECURITY.md`](SECURITY.md).

## License

By contributing, you agree that your contributions are licensed under the
[MIT License](LICENSE).
