using BenchmarkDotNet.Attributes;

namespace FuzzyScorer.Benchmarks
{
    /// <summary>Grouping throughput on unique, varied-length words.</summary>
    [MemoryDiagnoser]
    [ShortRunJob]
    public class GroupingBenchmarks
    {
        private string[] _words = null!;

        [Params(1_000, 5_000, 10_000)]
        public int WordCount { get; set; }

        [Params(1, 2)]
        public int MaxEditDistance { get; set; }

        [GlobalSetup]
        public void Setup() => _words = Corpus.CreateUniqueWords(WordCount, seed: 42);

        [Benchmark]
        public int Current() =>
            WordScorer.GroupWordsBySimilarity(_words, MaxEditDistance, default).Count;
    }

    /// <summary>2.1.0 baseline: per-occurrence scanning with allocating distance.</summary>
    [MemoryDiagnoser]
    [ShortRunJob]
    public class BaselineGroupingBenchmarks
    {
        private string[] _words = null!;

        [Params(1_000, 2_000)]
        public int WordCount { get; set; }

        [Params(1, 2)]
        public int MaxEditDistance { get; set; }

        [GlobalSetup]
        public void Setup() => _words = Corpus.CreateUniqueWords(WordCount, seed: 42);

        [Benchmark]
        public int Legacy() => LegacyGrouping.Group(_words, MaxEditDistance).Count;
    }

    /// <summary>Large thresholds (linear path, early match).</summary>
    [MemoryDiagnoser]
    [ShortRunJob]
    public class AggressiveGroupingBenchmarks
    {
        private string[] _words = null!;

        [Params(1_000)]
        public int WordCount { get; set; }

        [Params(25, 50)]
        public int MaxEditDistance { get; set; }

        [GlobalSetup]
        public void Setup() => _words = Corpus.CreateUniqueWords(WordCount, seed: 42);

        [Benchmark]
        public int Current() =>
            WordScorer.GroupWordsBySimilarity(_words, MaxEditDistance, default).Count;
    }

    /// <summary>Many repeated occurrences of a small vocabulary.</summary>
    [MemoryDiagnoser]
    [ShortRunJob]
    public class RepeatedTextBenchmarks
    {
        private string[] _words = null!;

        [Params(1, 2)]
        public int MaxEditDistance { get; set; }

        [GlobalSetup]
        public void Setup() => _words = Corpus.CreateRepeatedWords(
            uniqueCount: 100, totalCount: 10_000, seed: 42);

        [Benchmark]
        public int Current() =>
            WordScorer.GroupWordsBySimilarity(_words, MaxEditDistance, default).Count;
    }

    /// <summary>
    /// Adversarial vocabulary: 10,000 words with pairwise distance >= 2.
    /// A threshold of 1 never matches, which used to exhaust the comparison budget.
    /// </summary>
    [MemoryDiagnoser]
    [ShortRunJob]
    public class AdversarialGroupingBenchmarks
    {
        private string[] _words = null!;

        [GlobalSetup]
        public void Setup() => _words = Corpus.CreateMinDistanceTwoWords(10_000);

        [Benchmark]
        public int Current() =>
            WordScorer.GroupWordsBySimilarity(_words, 1, default).Count;
    }

    /// <summary>End-to-end frequency counting (normalization + counting).</summary>
    [MemoryDiagnoser]
    [ShortRunJob]
    public class FrequencyBenchmarks
    {
        private string _text = null!;

        [GlobalSetup]
        public void Setup() =>
            _text = string.Join(' ', Corpus.CreateUniqueWords(10_000, seed: 42));

        [Benchmark]
        public int Current() => WordScorer.GetWordFrequencies(_text).Count;
    }
}
