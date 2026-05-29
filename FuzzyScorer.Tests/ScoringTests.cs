using Xunit;
using FuzzyScorer;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace FuzzyScorer.Tests
{
    /// <summary>
    /// Unit tests for word scoring methods in the WordScorer class.
    /// </summary>
    public class ScoringTests
    {
        #region Frequency Method Tests (GetWordFrequencies)

        /// <summary>
        /// Typical case for frequency-based word scoring.
        /// </summary>
        [Fact]
        public void Frequency_TypicalCase_ShouldCountCorrectly()
        {
            // Arrange
            string inputText = "apple banana apple cherry banana apple";

            // Act
            var results = WordScorer.GetWordFrequencies(inputText);

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal(3, results.First(r => r.Text == "apple").Score);
            Assert.Equal(2, results.First(r => r.Text == "banana").Score);
            Assert.Equal(1, results.First(r => r.Text == "cherry").Score);
        }

        /// <summary>
        /// Null input for frequency method should return empty list.
        /// </summary>
        [Fact]
        public void Frequency_NullInput_ShouldReturnEmptyList()
        {
            var results = WordScorer.GetWordFrequencies(null);

            Assert.NotNull(results);
            Assert.Empty(results);
        }

        /// <summary>
        /// Whitespace and empty strings for frequency method should return empty list.
        /// </summary>
        [Fact]
        public void Frequency_EmptyAndWhitespace_ShouldReturnEmptyList()
        {
            var results = WordScorer.GetWordFrequencies("   ");

            Assert.Empty(results);
        }

        /// <summary>
        /// Case-different words should be grouped together.
        /// </summary>
        [Fact]
        public void Frequency_CaseInsensitive_ShouldGroupSameWord()
        {
            var results = WordScorer.GetWordFrequencies("Apple apple APPLE");

            Assert.Single(results);
            Assert.Equal(3, results[0].Score);
        }

        /// <summary>
        /// Input exceeding MaxInputLength should throw ArgumentException.
        /// </summary>
        [Fact]
        public void Frequency_InputTooLong_ShouldThrowArgumentException()
        {
            var input = new string('a', 1_000_001);

            var ex = Assert.Throws<ArgumentException>(() => WordScorer.GetWordFrequencies(input));
            Assert.Contains("maximum length", ex.Message);
        }

        /// <summary>
        /// Input with too many words should throw ArgumentException.
        /// </summary>
        [Fact]
        public void Frequency_TooManyWords_ShouldThrowArgumentException()
        {
            var words = string.Join(" ", Enumerable.Repeat("word", 10_001));

            var ex = Assert.Throws<ArgumentException>(() => WordScorer.GetWordFrequencies(words));
            Assert.Contains("exceeding limit", ex.Message);
        }

        /// <summary>
        /// Pre-cancelled token should throw OperationCanceledException.
        /// </summary>
        [Fact]
        public void Frequency_CancelledToken_ShouldThrowOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                WordScorer.GetWordFrequencies("some input", cts.Token));
        }

        #endregion

        #region Similarity Method Tests (GroupSimilarWords)

        /// <summary>
        /// Typical case: Similar words should be grouped together.
        /// </summary>
        [Fact]
        public void Similarity_TypicalCase_ShouldGroupSimilarWords()
        {
            // "apple" and "aple" are 1 edit away (maxEditDistance = 1)
            string inputText = "apple aple apple";

            var results = WordScorer.GroupSimilarWords(inputText, 1);

            Assert.Single(results);
            Assert.Equal(3, results[0].Score);
            Assert.Equal("apple", results[0].Text, ignoreCase: true);
        }

        /// <summary>
        /// Edit distance of 0 should behave like exact-match frequency counting.
        /// </summary>
        [Fact]
        public void Similarity_ZeroDistance_ShouldRequireExactMatch()
        {
            string inputText = "apple aple";

            var results = WordScorer.GroupSimilarWords(inputText, 0);

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.Text == "apple");
            Assert.Contains(results, r => r.Text == "aple");
        }

        /// <summary>
        /// Large edit distance should group all words into a single group.
        /// </summary>
        [Fact]
        public void Similarity_LargeDistance_ShouldGroupAllWords()
        {
            string inputText = "apple banana cherry";

            var results = WordScorer.GroupSimilarWords(inputText, 10);

            Assert.Single(results);
            Assert.Equal(3, results[0].Score);
        }

        /// <summary>
        /// Null input for similarity method should return empty list.
        /// </summary>
        [Fact]
        public void Similarity_NullInput_ShouldReturnEmptyList()
        {
            var results = WordScorer.GroupSimilarWords(null, 1);

            Assert.NotNull(results);
            Assert.Empty(results);
        }

        /// <summary>
        /// Negative edit distance should throw ArgumentException.
        /// </summary>
        [Fact]
        public void Similarity_NegativeEditDistance_ShouldThrowArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.GroupSimilarWords("apple banana", -1));
            Assert.Contains("maxEditDistance", ex.Message);
        }

        /// <summary>
        /// Edit distance exceeding the limit should throw ArgumentException.
        /// </summary>
        [Fact]
        public void Similarity_EditDistanceExceedsLimit_ShouldThrowArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.GroupSimilarWords("apple banana", 51));
            Assert.Contains("maxEditDistance", ex.Message);
        }

        /// <summary>
        /// Pre-cancelled token should throw OperationCanceledException.
        /// </summary>
        [Fact]
        public void Similarity_CancelledToken_ShouldThrowOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                WordScorer.GroupSimilarWords("apple banana", 1, cts.Token));
        }

        /// <summary>
        /// Input exceeding MaxInputLength should throw ArgumentException.
        /// </summary>
        [Fact]
        public void Similarity_InputTooLong_ShouldThrowArgumentException()
        {
            var input = new string('a', 1_000_001);

            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.GroupSimilarWords(input, 1));
            Assert.Contains("maximum length", ex.Message);
        }

        /// <summary>
        /// Input with too many words should throw ArgumentException.
        /// </summary>
        [Fact]
        public void Similarity_TooManyWords_ShouldThrowArgumentException()
        {
            var words = string.Join(" ", Enumerable.Repeat("word", 10_001));

            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.GroupSimilarWords(words, 1));
            Assert.Contains("exceeding limit", ex.Message);
        }

        #endregion

        #region FuzzyScorer Tests (ScoreAsync)

        private readonly IFuzzyScorer _scorer = new FuzzyScorer();

        /// <summary>
        /// Typical case: ScoreAsync should return correct original and compressed sizes.
        /// </summary>
        [Fact]
        public async Task ScoreAsync_TypicalCase_ReturnsCorrectSizes()
        {
            string inputText = "apple banana apple cherry banana apple";
            var result = await _scorer.ScoreAsync(inputText, 0.0, CancellationToken.None);

            Assert.Equal(6, result.OriginalSize);
            Assert.Equal(3, result.CompressedSize);
            Assert.Empty(result.Errors);
        }

        /// <summary>
        /// ScoreAsync should detect potential typos (words that differ by edits).
        /// </summary>
        [Fact]
        public async Task ScoreAsync_DetectsTypos()
        {
            string inputText = "apple aple apple";
            var result = await _scorer.ScoreAsync(inputText, 0.02, CancellationToken.None);

            Assert.Equal(3, result.OriginalSize);
            Assert.Equal(1, result.CompressedSize);
            Assert.Single(result.Errors);
            Assert.Equal("aple", result.Errors[0].ErrorText);
            Assert.Equal(1, result.Errors[0].RepetitionCount);
        }

        /// <summary>
        /// Sensitivity of 0.0 should not perform any fuzzy grouping, so no typos are detected.
        /// </summary>
        [Fact]
        public async Task ScoreAsync_SensitivityZero_ExactMatchOnly()
        {
            string inputText = "apple aple";
            var result = await _scorer.ScoreAsync(inputText, 0.0, CancellationToken.None);

            Assert.Equal(2, result.OriginalSize);
            Assert.Equal(2, result.CompressedSize);
            Assert.Empty(result.Errors);
        }

        /// <summary>
        /// Sensitivity of 1.0 should group all words together even if they differ significantly.
        /// </summary>
        [Fact]
        public async Task ScoreAsync_SensitivityOne_MaxFuzzy()
        {
            string inputText = "apple aple";
            var result = await _scorer.ScoreAsync(inputText, 1.0, CancellationToken.None);

            Assert.Equal(2, result.OriginalSize);
            Assert.Equal(1, result.CompressedSize);
            Assert.Single(result.Errors);
        }

        /// <summary>
        /// Empty input should return zeros and no errors.
        /// </summary>
        [Fact]
        public async Task ScoreAsync_EmptyInput_ReturnsZeroSizes()
        {
            var result = await _scorer.ScoreAsync("", 0.5, CancellationToken.None);

            Assert.Equal(0, result.OriginalSize);
            Assert.Equal(0, result.CompressedSize);
            Assert.Empty(result.Errors);
        }

        /// <summary>
        /// Whitespace-only input should return zeros and no errors.
        /// </summary>
        [Fact]
        public async Task ScoreAsync_WhitespaceInput_ReturnsZeroSizes()
        {
            var result = await _scorer.ScoreAsync("   ", 0.5, CancellationToken.None);

            Assert.Equal(0, result.OriginalSize);
            Assert.Equal(0, result.CompressedSize);
            Assert.Empty(result.Errors);
        }

        /// <summary>
        /// ErrorEntry should contain correct line numbers when error spans multiple lines.
        /// </summary>
        [Fact]
        public async Task ScoreAsync_LineNumbers_AreCorrect()
        {
            string inputText = "apple banana\napple cherry\nbanana aple";
            var result = await _scorer.ScoreAsync(inputText, 0.02, CancellationToken.None);

            Assert.Single(result.Errors);
            Assert.Equal("aple", result.Errors[0].ErrorText);
            Assert.Equal(1, result.Errors[0].RepetitionCount);
            Assert.Contains(3, result.Errors[0].LineNumbers);
        }

        /// <summary>
        /// Pre-cancelled token should throw OperationCanceledException.
        /// </summary>
        [Fact]
        public async Task ScoreAsync_CancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _scorer.ScoreAsync("some input", 0.5, cts.Token));
        }

        /// <summary>
        /// Sensitivity below 0 should throw ArgumentException.
        /// </summary>
        [Fact]
        public async Task ScoreAsync_SensitivityBelowZero_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _scorer.ScoreAsync("test", -0.1, CancellationToken.None));
        }

        /// <summary>
        /// Sensitivity above 1 should throw ArgumentException.
        /// </summary>
        [Fact]
        public async Task ScoreAsync_SensitivityAboveOne_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _scorer.ScoreAsync("test", 1.1, CancellationToken.None));
        }

        #endregion
    }
}
