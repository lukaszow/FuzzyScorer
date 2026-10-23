# Benchmarks

Measured with [BenchmarkDotNet](https://benchmarkdotnet.org/) 0.15.8, `ShortRun` job
(3 warmup + 3 measured iterations) and `MemoryDiagnoser`.

**Environment:** Intel Core i5-7400 3.00 GHz (4 cores), Windows 11 24H2,
.NET SDK 10.0.201, .NET 10.0.5, X64 RyuJIT.

Run locally:

```bash
dotnet run -c Release --project benchmarks/FuzzyScorer.Benchmarks
```

## Unique words, varied lengths (4–10 characters)

`2.1.0` is a faithful copy of the previous grouping loop (per-occurrence scan,
allocating banded distance). `2.2.0` deduplicates words, reuses buffers, and uses the
deletion-variant index for thresholds 1–2.

| Scenario | 2.1.0 | 2.2.0 | Speedup | Allocated 2.1.0 | Allocated 2.2.0 |
|---|---:|---:|---:|---:|---:|
| 1,000 words, k=1 | 11.83 ms | **1.94 ms** | 6.1× | 12.69 MB | 1.22 MB |
| 1,000 words, k=2 | 26.82 ms | **6.33 ms** | 4.2× | 21.57 MB | 3.87 MB |
| 2,000 words, k=1 | 50.25 ms | not measured | — | 53.02 MB | — |
| 2,000 words, k=2 | 102.93 ms | not measured | — | 83.09 MB | — |
| 5,000 words, k=1 | budget exceeded | **11.45 ms** | — | — | 7.70 MB |
| 5,000 words, k=2 | budget exceeded | **53.18 ms** | — | — | 17.39 MB |
| 10,000 words, k=1 | budget exceeded | **32.47 ms** | — | — | 11.39 MB |
| 10,000 words, k=2 | budget exceeded | **104.36 ms** | — | — | 33.89 MB |

For 5,000+ words the 2.1.0 baseline exceeds `MaxSimilarityComparisons` (10,000,000
pairwise distance computations) and throws `ArgumentException` instead of finishing.

## Other scenarios (2.2.0)

| Scenario | Mean | Allocated |
|---|---:|---:|
| 1,000 words, k=25 (aggressive merging) | 0.51 ms | 116 KB |
| 1,000 words, k=50 (maximum fuzziness) | 0.52 ms | 116 KB |
| 10,000 occurrences, 100 unique words, k=1 | 0.85 ms | 342 KB |
| 10,000 occurrences, 100 unique words, k=2 | 1.72 ms | 671 KB |
| 10,000 adversarial words (pairwise distance ≥ 2), k=1 | 21.50 ms | 10.07 MB |
| `GetWordFrequencies`, 10,000 words | 3.29 ms | 2.59 MB |

The adversarial corpus never matches at k=1. The linear scan performs ~50 million
comparisons and hits the budget; the deletion index answers each query from a handful
of candidates.

## Notes

- k ≤ 2 uses the deletion-variant index (variants keyed by a 64-bit hash; collisions
  only add candidates, which are verified with the exact bounded distance).
- k = 3 uses a BK-tree with subtree length pruning once the input has at least 3,000
  words; smaller inputs use the linear scan and its early exit.
- k > 3 uses the linear scan, which is fast when matches are plentiful (see k=25/50).
- The comparison budget still bounds pathological inputs whose distinct words are
  mutually far apart and cannot be pruned by the index.
