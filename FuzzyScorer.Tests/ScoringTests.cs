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
    }
}
