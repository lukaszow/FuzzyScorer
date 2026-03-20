# Security Policy

## Overview

FuzzyScorer is a .NET library designed with security-first principles. This document outlines the security measures, threat model, and best practices for using FuzzyScorer safely in production environments.

## Security Features

### Input Validation & Limits

All input text is validated to prevent denial-of-service attacks:

| Limit | Value | Rationale |
|-------|-------|-----------|
| **MaxWordsPerText** | 10,000 | Prevents memory exhaustion from oversized inputs |
| **MaxWordLength** | 256 characters | Limits processing overhead per word |
| **MaxSimilarityThreshold** | 50 | Prevents quadratic complexity in grouping algorithm |

**Behavior**: Input exceeding limits raises `ArgumentException` with descriptive message.

```csharp
// ✅ Valid
Scorer.GetScoringWords("small text");                    // OK
Scorer.GetScoringWords(text, similarity: 10);            // OK (≤ 50)

// ❌ Invalid
Scorer.GetScoringWords(hugeText);                        // ArgumentException: exceeds 10,000 words
Scorer.GetScoringWords(text, similarity: 100);           // ArgumentException: must be ≤ 50
```

### Input Normalization

Before processing, input is normalized to remove attack vectors:

1. **Character Filtering**: Non-alphanumeric characters removed (except spaces, hyphens)
   - Prevents injection of invisible/control characters
   - Example: `"hello\x00world"` → `"hello world"`

2. **Whitespace Handling**: Normalizes `\t`, `\n`, `\r` to space
   - Cross-platform consistency (Windows, Linux, macOS)

3. **Word Extraction**: Individual words validated for length and content
   - Empty words discarded
   - Words > 256 characters rejected

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
var cts = new CancellationTokenSource();

// Cancel operation after 5 seconds
cts.CancelAfter(TimeSpan.FromSeconds(5));

try
{
    var results = Scorer.GetScoringWords(largeText, 1, cts.Token);
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
| **DoS via Complexity** | Levenshtein distance capped + similarity threshold limit |
| **Memory Exhaustion** | Input size validation before processing |
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
- **ModelContextProtocol.NET.Server** (v0.3.3-alpha)
  - Used for: Protocol server communication
  - Status: Pre-release (alpha) — review for production use
  - Recommendation: Track updates; consider pinning version until stable release

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

## Usage Guidelines

### ✅ Safe Usage

```csharp
// 1. With default limits (best for web services)
try
{
    var results = Scorer.GetScoringWords(userInput);
}
catch (ArgumentException ex)
{
    // Log and return error to user
    _logger.LogWarning($"Invalid input: {ex.Message}");
    return BadRequest(ex.Message);
}

// 2. With cancellation (async contexts)
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
try
{
    var results = Scorer.GetScoringWords(textData, 1, cts.Token);
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
var results = Scorer.GetScoringWords(untrustedData); // Can throw!

// DON'T: Pass unbounded similarity threshold
var results = Scorer.GetScoringWords(text, 10_000); // Use ≤ 50

// DON'T: No cancellation support in long operations
var results = Scorer.GetScoringWords(hugeFile); // Can hang indefinitely
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

Instead:
1. Email relevant security contact with:
   - Vulnerability description
   - Severity assessment (Critical/High/Medium/Low)
   - Proof-of-concept (if possible)
   - Steps to reproduce

2. Allow 72 hours for initial assessment

3. Coordinate disclosure timeline (typically 30–90 days)

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

**Last Updated**: 2026-02-17  
**Version**: 1.1 (Security Hardening Release)
