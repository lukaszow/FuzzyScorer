using Xunit;
using WordsCloud;
using System.Collections.Generic;
using System.Linq;

namespace WordsCloud.Tests
{
    /// <summary>
    /// Unit tests for word scoring methods in the Program class.
    /// </summary>
    public class ScoringTests
    {
        #region Frequency Method Tests (GetScoringWords - 1 overload)

        /// <summary>
        /// Typical case for frequency-based word scoring.
        /// </summary>
        [Fact]
        public void Frequency_TypicalCase_ShouldCountCorrectly()
        {
            // Arrange
            string inputText = "apple banana apple cherry banana apple";

            // Act
            var results = Program.GetScoringWords(inputText);

            // Assert
            Assert.Equal(3, results.Count);
            Assert.Equal(3, results.First(r => r.Text == "apple").Score);
            Assert.Equal(2, results.First(r => r.Text == "banana").Score);
            Assert.Equal(1, results.First(r => r.Text == "cherry").Score);
        }

        /// <summary>
        /// Edge case: Null input for frequency method.
        /// </summary>
        [Fact]
        public void Frequency_NullInput_ShouldReturnEmptyList()
        {
            // Act
            var results = Program.GetScoringWords(null);

            // Assert
            Assert.NotNull(results);
            Assert.Empty(results);
        }

        /// <summary>
        /// Edge case: Whitespace and empty strings for frequency method.
        /// </summary>
        [Fact]
        public void Frequency_EmptyAndWhitespace_ShouldReturnEmptyList()
        {
            // Arrange
            string inputText = "   ";

            // Act
            var results = Program.GetScoringWords(inputText);

            // Assert
            Assert.Empty(results);
        }

        #endregion

        #region Similarity Method Tests (GetScoringWords - 2 overloads)

        /// <summary>
        /// Typical case: Similar words should be grouped together.
        /// </summary>
        [Fact]
        public void Similarity_TypicalCase_ShouldGroupSimilarWords()
        {
            // Arrange
            // "apple" and "aple" are 1 edit away (targetSimilarity = 1)
            string inputText = "apple aple apple";

            // Act
            var results = Program.GetScoringWords(inputText, 1);

            // Assert
            Assert.Single(results);
            Assert.Equal(3, results[0].Score);
            Assert.Equal("apple", results[0].Text, ignoreCase: true);
        }

        /// <summary>
        /// Edge case: Similarity score of 0 should behave like frequency count.
        /// </summary>
        [Fact]
        public void Similarity_ZeroDistance_ShouldRequireExactMatch()
        {
            // Arrange
            string inputText = "apple aple";

            // Act
            var results = Program.GetScoringWords(inputText, 0);

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.Text == "apple");
            Assert.Contains(results, r => r.Text == "aple");
        }

        /// <summary>
        /// Edge case: Large similarity distance should group very different words.
        /// </summary>
        [Fact]
        public void Similarity_LargeDistance_ShouldGroupAllWords()
        {
            // Arrange
            string inputText = "apple banana cherry";

            // Act
            var results = Program.GetScoringWords(inputText, 10);

            // Assert
            Assert.Single(results);
            Assert.Equal(3, results[0].Score);
        }

        #endregion
    }
}
