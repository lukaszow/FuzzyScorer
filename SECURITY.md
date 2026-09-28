# Security Policy

## Overview

FuzzyScorer is a .NET library designed with security-first principles. This document outlines the security measures, threat model, and best practices for using FuzzyScorer safely in production environments.

## Security Features

### Input Validation & Limits

All input text is validated to prevent denial-of-service attacks:

| Limit | Value | Rationale |
|-------|-------|-----------|
| **MaxInputLength** | 1,000,000 characters | Hard cap on the raw input size |
| **MaxWordsPerText** | 10,000 | Prevents memory exhaustion from oversized inputs |
| **MaxWordLength** | 256 characters | Limits processing overhead per word |
| **MaxEditDistanceLimit** | 50 | Upper bound for the caller-supplied similarity threshold |
| **MaxSimilarityComparisons** | 10,000,000 | Hard budget on pairwise edit-distance comparisons in grouping |

**Behavior**: Input exceeding limits raises `ArgumentException` with descriptive message.

```csharp
// ✅ Valid
WordScorer.GetWordFrequencies("small text");                    // OK
WordScorer.GroupSimilarWords(text, maxEditDistance: 10);        // OK (≤ 50)

// ❌ Invalid
WordScorer.GetWordFrequencies(hugeText);                        // ArgumentException: exceeds 10,000 words
WordScorer.GroupSimilarWords(text, maxEditDistance: 100);       // ArgumentException: must be ≤ 50
```

### Bounded Algorithmic Complexity

The similarity grouping is the only super-linear path in the library. It is bounded on
three independent axes:

1. **Distinct-word budget** — `MaxWordsPerText` (10,000) caps the number of input words.
2. **Comparison budget** — `MaxSimilarityComparisons` (10,000,000) caps the total number
   of `(word, group-leader)` comparisons. Exceeding it throws `ArgumentException` instead
   of degrading silently. This bounds the otherwise quadratic `O(N²)` grouping.
3. **Banded edit distance** — the Levenshtein routine short-circuits when the length
   difference exceeds the threshold, when the minimum row cost exceeds the threshold,
   and only evaluates a diagonal band of width `2 × maxEditDistance + 1`. It uses two
   reusable rows (`O(min(n, m))` memory) instead of a full `(n+1) × (m+1)` matrix.

Together these prevent the cubic-time / unbounded-allocation behaviour that a naive
Levenshtein implementation would allow on adversarial input.

### Input Normalization

Input is normalized to remove attack vectors:

1. **Character Filtering**: characters that are not Unicode letters (`\p{L}`), Unicode
   digits (`\p{N}`), whitespace (`\s`), or hyphens (`-`) are removed entirely.
   - Prevents injection of invisible/control characters.
   - Example: `"hello\x00world"` → `"helloworld"` (the control character is deleted).
   - Example: `"don't"` → `"dont"` (apostrophes are not part of the allowed set).

2. **Whitespace Handling**: input is split on spaces, `\t`, `\n`, and `\r`
   (whitespace is used as a delimiter, it is not rewritten in the source string).
   - Cross-platform consistency (Windows, Linux, macOS).

3. **Word Extraction**: individual words are validated for length and content.
   - Empty words discarded.
   - Words longer than 256 characters are silently dropped (not an error).

### Immutable Objects

`WordScore` objects are read-only after construction:

```csharp
var score = new WordScore("hello", 5);

// ❌ NOT POSSIBLE (compiler error)
// score.Text = "goodbye";
// score.Score = 10;

// ✅ CORRECT (read-only properties)
Console.WriteLine(score.Text);   // "hello"
Console.WriteLine(score.Score);  // 5
```

**Benefits**:
- Thread-safe immutability
- Prevents accidental data corruption
- Predictable behavior in multithreaded contexts

### Cancellation Support

All scoring methods accept `CancellationToken` for controlled resource management:

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

try
{
    // Static API
    var results = WordScorer.GroupSimilarWords(largeText, 1, cts.Token);

    // Instance API
    IFuzzyScorer scorer = new FuzzyScorer();
    var result = await scorer.ScoreAsync(largeText, 0.02, cts.Token);
}
catch (OperationCanceledException)
{
    // Operation was cancelled (prevents indefinite hangs)
}
```

**Use Cases**:
- Server request timeouts
- User-initiated cancellations
- Resource management in async pipelines

### No Hardcoded Secrets

Security audit confirms:
- ✅ No API keys, passwords, tokens, or certificates in code
- ✅ No external service calls (offline analysis only)
- ✅ No executable generation or dynamic code emission
- ✅ No P/Invoke or unmanaged code
- ✅ No BinaryFormatter or unsafe serialization

## Threat Model

### Protected Against

| Threat | Mitigation |
|--------|-----------|
| **DoS via Large Input** | Word/character count limits + `MaxWordsPerText` |
| **DoS via Complexity** | Comparison budget (`MaxSimilarityComparisons`) + banded Levenshtein capped at `MaxEditDistanceLimit` |
| **Memory Exhaustion** | Input size validation before processing; edit distance uses two rows, not a full matrix |
| **Infinite Loops** | `CancellationToken` support + no dynamic recursion |
| **Malicious Characters** | Input normalization (non-alphanumeric removal) |
| **Object Mutation** | Immutable `WordScore` design |
| **Supply Chain** | No external dependencies (only .NET runtime) |

### Not Protected Against

| Threat | Reason |
|--------|--------|
| **Zero-Day Runtime Exploits** | Depends on .NET runtime security |
| **Side-Channel Attacks** | Timing analysis not mitigated |
| **Semantic Analysis Tricks** | Library performs lexical, not semantic analysis |
| **Massive Sequential Requests** | Rate limiting should be handled by calling code |

## Dependency Management

### Direct Dependencies
- **Runtime: none** — FuzzyScorer targets `net10.0` and ships zero runtime package
  references. Only the .NET runtime BCL is required.
- **Build-time only**: `Microsoft.SourceLink.GitHub` (`PrivateAssets="all"`) is used to
  embed source-control metadata. It is not shipped, does not flow to consumers, and is
  not loaded at runtime.

### Indirect Dependencies
Run vulnerability scan regularly:
```bash
dotnet list package --vulnerable
```

### Transitive Dependencies
No automatic detection in NuGet; recommend SBOM tools:
- **Dependabot** (GitHub): Automated dependency scanning
- **Snyk**: Software composition analysis
- **CycloneDX**: SBOM generation

## Security Audit Log

| Date | Check | Result |
|---|---|---|
| 2026-07-14 | Secrets/credentials scan (full codebase) | **PASS** — no secrets, keys, or credentials found |
| 2026-07-14 | Vulnerable packages (`dotnet list package --vulnerable`) | **PASS** — zero vulnerabilities in both projects |
| 2026-07-14 | External network calls in library code | **PASS** — no outbound HTTP; offline library |
| 2026-07-14 | Unsafe code / P/Invoke | **PASS** — no `unsafe`, `DllImport`, `BinaryFormatter`, or `Marshal` |
| 2026-07-14 | Direct dependency audit (csproj vs docs) | **PASS** — zero `PackageReference` entries; docs corrected |
| 2026-07-14 | .gitignore sensitive exclusions | **PASS** — no patterns for credential files |
| 2026-09-28 | Algorithmic DoS review (grouping complexity) | **FIXED** — comparison budget + banded Levenshtein with cutoff added |
| 2026-09-28 | Public API input-validation audit | **FIXED** — null/range guards added to `GroupWordsBySimilarity`, `AreWordsSimilar`; validation moved before early returns |

## Usage Guidelines

### ✅ Safe Usage

```csharp
// 1. With default limits (best for web services)
try
{
    var results = WordScorer.GetWordFrequencies(userInput);
}
catch (ArgumentException ex)
{
    _logger.LogWarning($"Invalid input: {ex.Message}");
}

// 2. With cancellation (async contexts)
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try
{
    var results = WordScorer.GroupSimilarWords(textData, 1, cts.Token);
}
catch (OperationCanceledException)
{
    _logger.LogWarning("Scoring operation timed out");
}

// 3. Read-only access (guaranteed safety)
WordScore score = new WordScore("word", 5);
// score.Text and score.Score are read-only — no mutations possible
```

### ❌ Unsafe Usage

```csharp
// DON'T: Ignore validation exceptions
var results = WordScorer.GetWordFrequencies(untrustedData); // Can throw!

// DON'T: Pass unbounded edit distance
var results = WordScorer.GroupSimilarWords(text, maxEditDistance: 10_000); // Use ≤ 50

// DON'T: No cancellation support in long operations
var results = WordScorer.GetWordFrequencies(hugeFile); // Can hang indefinitely
```

## Security Audit Checklist

Use this checklist for code reviews and security assessments:

- [ ] **Input Validation**
  - [ ] All public methods validate inputs before processing
  - [ ] Limits enforced (word count, word length, similarity)
  - [ ] Validation exceptions documented in XML comments

- [ ] **Immutability**
  - [ ] `WordScore` properties are read-only
  - [ ] Constructor validates parameters (null checks, range validation)
  - [ ] No setters on public properties

- [ ] **Resource Management**
  - [ ] `CancellationToken` accepted on long-running methods
  - [ ] No unbounded loops or recursion
  - [ ] Memory usage bounded by input limits

- [ ] **Code Quality**
  - [ ] No hardcoded secrets or credentials
  - [ ] No P/Invoke, unsafe code, or unmanaged resources
  - [ ] No reflection-based code generation
  - [ ] No BinaryFormatter or unsafe deserialization

- [ ] **Dependencies**
  - [ ] Transitive dependencies scanned for vulnerabilities
  - [ ] Pre-release packages (alpha/beta) reviewed before use
  - [ ] Version pinning documented in `FuzzyScorer.csproj`

- [ ] **Documentation**
  - [ ] Security features documented (this file)
  - [ ] Usage examples include error handling
  - [ ] Limits and constraints clearly stated
  - [ ] Cancellation patterns explained

## Reporting Security Vulnerabilities

If you discover a security vulnerability, **please do NOT open a public GitHub issue**.

Preferred channel — **GitHub Private Vulnerability Reporting**:
go to the repository's *Security* tab → *Report a vulnerability*. This keeps the report
private and lets us coordinate a fix.

Alternative channel — **email**: `lukasz.stilger@gmail.com` with:
1. Vulnerability description
2. Severity assessment (Critical/High/Medium/Low)
3. Proof-of-concept (if possible)
4. Steps to reproduce

Allow 72 hours for initial assessment, then coordinate a disclosure timeline
(typically 30–90 days).

## Future Improvements

Planned security enhancements:

- [ ] **Rate Limiting**: Built-in token bucket or sliding window rate limiter
- [ ] **Audit Logging**: Optional structured logging for security events
- [ ] **Metrics**: Usage metrics (requests/second, average processing time)
- [ ] **Fuzzing**: Continuous fuzzing test suite
- [ ] **SBOM**: Generate CycloneDX bill of materials on release

## References

- [Microsoft .NET Security Best Practices](https://docs.microsoft.com/en-us/dotnet/standard/security/)
- [OWASP: Input Validation](https://owasp.org/www-community/attacks/Injection)
- [OWASP: Denial of Service](https://owasp.org/www-community/attacks/Denial_of_Service)
- [CWE-190: Integer Overflow](https://cwe.mitre.org/data/definitions/190.html)
- [CWE-400: Uncontrolled Resource Consumption](https://cwe.mitre.org/data/definitions/400.html)

---

**Last Updated**: 2026-09-28  
**Version**: 2.0 (Algorithmic DoS hardening release)
