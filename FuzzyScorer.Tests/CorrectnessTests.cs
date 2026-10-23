using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FuzzyScorer;
using Xunit;

namespace FuzzyScorer.Tests
{
    /// <summary>
    /// Regression tests for the correctness fixes shipped in 1.2.0:
    /// normalization, validation, immutability, and deterministic typo selection.
    /// </summary>
    public class CorrectnessTests
    {
        private readonly IFuzzyScorer _scorer = new FuzzyScorer();

        #region Normalization

        [Fact]
        public void Normalize_NonBreakingSpace_SplitsWords()
        {
            var words = WordScorer.NormalizeAndExtractWords("alpha\u00A0beta", CancellationToken.None);

            string[] expected = { "alpha", "beta" };
            Assert.Equal(expected, words);
        }

        [Fact]
        public void Normalize_VerticalTabAndFormFeed_SplitWords()
        {
            var words = WordScorer.NormalizeAndExtractWords("alpha\vbeta\fgamma", CancellationToken.None);

            string[] expected = { "alpha", "beta", "gamma" };
            Assert.Equal(expected, words);
        }

        [Fact]
        public void Normalize_UnicodeLineSeparator_SplitsWords()
        {
            var words = WordScorer.NormalizeAndExtractWords("alpha\u2028beta", CancellationToken.None);

            string[] expected = { "alpha", "beta" };
            Assert.Equal(expected, words);
        }

        [Fact]
        public void Normalize_PunctuationBecomesSeparator()
        {
            var words = WordScorer.NormalizeAndExtractWords("hello,world foo.bar", CancellationToken.None);

            string[] expected = { "hello", "world", "foo", "bar" };
            Assert.Equal(expected, words);
        }

        [Fact]
        public void Normalize_Apostrophe_RemovedWithoutSplitting()
        {
            var words = WordScorer.NormalizeAndExtractWords("don't john\u2019s", CancellationToken.None);

            string[] expected = { "dont", "johns" };
            Assert.Equal(expected, words);
        }

        [Fact]
        public void Normalize_NfcAndNfd_ProduceSameWord()
        {
            // U+00E9 (precomposed) vs "e" + U+0301 (combining acute accent)
            var results = WordScorer.GetWordFrequencies("caf\u00E9 cafe\u0301");

            Assert.Single(results);
            Assert.Equal(2, results[0].Count);
        }

        [Fact]
        public void Normalize_TrimsEdgeHyphens()
        {
            var words = WordScorer.NormalizeAndExtractWords("--foo-- -bar-baz-", CancellationToken.None);

            string[] expected = { "foo", "bar-baz" };
            Assert.Equal(expected, words);
        }

        [Fact]
        public void Normalize_InvalidSurrogate_DoesNotThrow()
        {
            var words = WordScorer.NormalizeAndExtractWords("ok\uD800", CancellationToken.None);

            Assert.Contains("ok", words);
        }

        #endregion

        #region Immutability

        [Fact]
        public void FuzzyScorerResult_Errors_AreImmutableCopy()
        {
            var source = new List<ErrorEntry>
            {
                new ErrorEntry("aple", 1, new List<int> { 1 }),
            };

            var result = new FuzzyScorerResult(1, 1, source);
            source.Add(new ErrorEntry("appple", 1, new List<int> { 2 }));

            Assert.Single(result.Errors);
        }

        [Fact]
        public void WordScore_NullText_ThrowsArgumentNull()
        {
            Assert.Throws<ArgumentNullException>(() => new WordScore(null!, 1));
        }

        #endregion

        #region Validation

        [Fact]
        public void AreWordsSimilar_NullFirstWord_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                WordScorer.AreWordsSimilar(null!, "apple", 1));
        }

        [Fact]
        public void AreWordsSimilar_NullSecondWord_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                WordScorer.AreWordsSimilar("apple", null!, 1));
        }

        [Fact]
        public void AreWordsSimilar_InvalidDistance_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WordScorer.AreWordsSimilar("apple", "aple", -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WordScorer.AreWordsSimilar("apple", "aple", 51));
        }

        [Fact]
        public void GroupWordsBySimilarity_NullList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                WordScorer.GroupWordsBySimilarity(null!, 1, CancellationToken.None));
        }

        [Fact]
        public void GroupWordsBySimilarity_NullElement_Throws()
        {
            var words = new List<string> { "apple", null! };

            Assert.Throws<ArgumentException>(() =>
                WordScorer.GroupWordsBySimilarity(words, 1, CancellationToken.None));
        }

        [Fact]
        public void GroupWordsBySimilarity_InvalidDistance_Throws()
        {
            var words = new List<string> { "apple" };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WordScorer.GroupWordsBySimilarity(words, -1, CancellationToken.None));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WordScorer.GroupWordsBySimilarity(words, 51, CancellationToken.None));
        }

        #endregion

        #region Typo selection and line tracking

        [Fact]
        public async Task ScoreAsync_TieOnFrequency_PrefersLongest()
        {
            // All three appear once; "apple" is the longest variant and becomes the representative.
            var result = await _scorer.ScoreAsync("aple ale apple", 0.02, CancellationToken.None);

            Assert.Equal(2, result.Errors.Count);
            Assert.DoesNotContain(result.Errors, e => e.Text == "apple");
            Assert.Contains(result.Errors, e => e.Text == "aple");
            Assert.Contains(result.Errors, e => e.Text == "ale");
        }

        [Fact]
        public async Task ScoreAsync_TieOnFrequencyAndLength_PrefersOrdinal()
        {
            // Equal frequency and equal length: "cat" sorts before "cot" and wins.
            var result = await _scorer.ScoreAsync("cat cot", 0.02, CancellationToken.None);

            Assert.Single(result.Errors);
            Assert.Equal("cot", result.Errors[0].Text);
        }

        [Fact]
        public async Task ScoreAsync_RepeatedWordOnSameLine_DistinctLineNumbers()
        {
            var result = await _scorer.ScoreAsync(
                "aple aple apple apple apple", 0.02, CancellationToken.None);

            var error = Assert.Single(result.Errors);
            Assert.Equal("aple", error.Text);
            Assert.Equal(2, error.Occurrences);
            Assert.Single(error.LineNumbers);
            Assert.Equal(1, error.LineNumbers[0]);
        }

        [Fact]
        public async Task ScoreAsync_RepeatedTypo_ReportedOnce()
        {
            var result = await _scorer.ScoreAsync(
                "apple apple aple aple", 0.02, CancellationToken.None);

            var error = Assert.Single(result.Errors);
            Assert.Equal("aple", error.Text);
            Assert.Equal(2, error.Occurrences);
        }

        [Fact]
        public async Task Errors_AreSortedByLine()
        {
            var result = await _scorer.ScoreAsync(
                "banana banana bannana\nfoo\napple aple", 0.02, CancellationToken.None);

            Assert.Equal(2, result.Errors.Count);
            Assert.Equal("bannana", result.Errors[0].Text);
            Assert.Equal(1, result.Errors[0].LineNumbers[0]);
            Assert.Equal("aple", result.Errors[1].Text);
            Assert.Equal(3, result.Errors[1].LineNumbers[0]);
        }

        #endregion
    }
}
