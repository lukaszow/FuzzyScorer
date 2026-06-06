using Xunit;
using FuzzyScorer;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FuzzyScorer.Tests
{
    public class ScoringTests
    {
        #region Frequency Method Tests (GetWordFrequencies)

        [Fact]
        public void Frequency_TypicalCase_ShouldCountCorrectly()
        {
            string inputText = "apple banana apple cherry banana apple";

            var results = WordScorer.GetWordFrequencies(inputText);

            Assert.Equal(3, results.Count);
            Assert.Equal(3, results.First(r => r.Text == "apple").Score);
            Assert.Equal(2, results.First(r => r.Text == "banana").Score);
            Assert.Equal(1, results.First(r => r.Text == "cherry").Score);
        }

        [Fact]
        public void Frequency_NullInput_ShouldReturnEmptyList()
        {
            var results = WordScorer.GetWordFrequencies(null);

            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public void Frequency_EmptyAndWhitespace_ShouldReturnEmptyList()
        {
            var results = WordScorer.GetWordFrequencies("   ");

            Assert.Empty(results);
        }

        [Fact]
        public void Frequency_CaseInsensitive_ShouldGroupSameWord()
        {
            var results = WordScorer.GetWordFrequencies("Apple apple APPLE");

            Assert.Single(results);
            Assert.Equal(3, results[0].Score);
        }

        [Fact]
        public void Frequency_InputTooLong_ShouldThrowArgumentException()
        {
            var input = new string('a', 1_000_001);

            var ex = Assert.Throws<ArgumentException>(() => WordScorer.GetWordFrequencies(input));
            Assert.Contains("maximum length", ex.Message);
        }

        [Fact]
        public void Frequency_TooManyWords_ShouldThrowArgumentException()
        {
            var words = string.Join(" ", Enumerable.Repeat("word", 10_001));

            var ex = Assert.Throws<ArgumentException>(() => WordScorer.GetWordFrequencies(words));
            Assert.Contains("exceeding limit", ex.Message);
        }

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

        [Fact]
        public void Similarity_TypicalCase_ShouldGroupSimilarWords()
        {
            string inputText = "apple aple apple";

            var results = WordScorer.GroupSimilarWords(inputText, 1);

            Assert.Single(results);
            Assert.Equal(3, results[0].Score);
            Assert.Equal("apple", results[0].Text, ignoreCase: true);
        }

        [Fact]
        public void Similarity_ZeroDistance_ShouldRequireExactMatch()
        {
            string inputText = "apple aple";

            var results = WordScorer.GroupSimilarWords(inputText, 0);

            Assert.Equal(2, results.Count);
            Assert.Contains(results, r => r.Text == "apple");
            Assert.Contains(results, r => r.Text == "aple");
        }

        [Fact]
        public void Similarity_LargeDistance_ShouldGroupAllWords()
        {
            string inputText = "apple banana cherry";

            var results = WordScorer.GroupSimilarWords(inputText, 10);

            Assert.Single(results);
            Assert.Equal(3, results[0].Score);
        }

        [Fact]
        public void Similarity_NullInput_ShouldReturnEmptyList()
        {
            var results = WordScorer.GroupSimilarWords(null, 1);

            Assert.NotNull(results);
            Assert.Empty(results);
        }

        [Fact]
        public void Similarity_NegativeEditDistance_ShouldThrowArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.GroupSimilarWords("apple banana", -1));
            Assert.Contains("maxEditDistance", ex.Message);
        }

        [Fact]
        public void Similarity_EditDistanceExceedsLimit_ShouldThrowArgumentException()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.GroupSimilarWords("apple banana", 51));
            Assert.Contains("maxEditDistance", ex.Message);
        }

        [Fact]
        public void Similarity_CancelledToken_ShouldThrowOperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                WordScorer.GroupSimilarWords("apple banana", 1, cts.Token));
        }

        [Fact]
        public void Similarity_InputTooLong_ShouldThrowArgumentException()
        {
            var input = new string('a', 1_000_001);

            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.GroupSimilarWords(input, 1));
            Assert.Contains("maximum length", ex.Message);
        }

        [Fact]
        public void Similarity_TooManyWords_ShouldThrowArgumentException()
        {
            var words = string.Join(" ", Enumerable.Repeat("word", 10_001));

            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.GroupSimilarWords(words, 1));
            Assert.Contains("exceeding limit", ex.Message);
        }

        #endregion

        #region AreWordsSimilar Tests

        [Fact]
        public void AreWordsSimilar_ExactMatch_ReturnsTrue()
        {
            Assert.True(WordScorer.AreWordsSimilar("apple", "apple", 0));
        }

        [Fact]
        public void AreWordsSimilar_OneEditAway_ReturnsTrue()
        {
            Assert.True(WordScorer.AreWordsSimilar("aple", "apple", 1));
            Assert.False(WordScorer.AreWordsSimilar("aple", "apple", 0));
        }

        [Fact]
        public void AreWordsSimilar_BeyondThreshold_ReturnsFalse()
        {
            Assert.False(WordScorer.AreWordsSimilar("cat", "dog", 1));
        }

        #endregion

        #region GroupWordsBySimilarity Tests

        [Fact]
        public void GroupWordsBySimilarity_TypicalCase_GroupsCorrectly()
        {
            var words = new List<string> { "apple", "aple", "banana", "cherry" };

            var groups = WordScorer.GroupWordsBySimilarity(words, 1, CancellationToken.None);

            Assert.Equal(3, groups.Count);

            var appleGroup = groups.First(g => g.Contains("apple"));
            Assert.Contains("aple", appleGroup);
            Assert.Equal(2, appleGroup.Count);
        }

        #endregion

        #region FuzzyScorer Tests (ScoreAsync)

        private readonly IFuzzyScorer _scorer = new FuzzyScorer();

        [Fact]
        public async Task ScoreAsync_TypicalCase_ReturnsCorrectSizes()
        {
            string inputText = "apple banana apple cherry banana apple";
            var result = await _scorer.ScoreAsync(inputText, 0.0, CancellationToken.None);

            Assert.Equal(6, result.OriginalSize);
            Assert.Equal(3, result.CompressedSize);
            Assert.Empty(result.Errors);
        }

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

        [Fact]
        public async Task ScoreAsync_SensitivityZero_ExactMatchOnly()
        {
            string inputText = "apple aple";
            var result = await _scorer.ScoreAsync(inputText, 0.0, CancellationToken.None);

            Assert.Equal(2, result.OriginalSize);
            Assert.Equal(2, result.CompressedSize);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ScoreAsync_SensitivityOne_MaxFuzzy()
        {
            string inputText = "apple aple";
            var result = await _scorer.ScoreAsync(inputText, 1.0, CancellationToken.None);

            Assert.Equal(2, result.OriginalSize);
            Assert.Equal(1, result.CompressedSize);
            Assert.Single(result.Errors);
        }

        [Fact]
        public async Task ScoreAsync_EmptyInput_ReturnsZeroSizes()
        {
            var result = await _scorer.ScoreAsync("", 0.5, CancellationToken.None);

            Assert.Equal(0, result.OriginalSize);
            Assert.Equal(0, result.CompressedSize);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task ScoreAsync_WhitespaceInput_ReturnsZeroSizes()
        {
            var result = await _scorer.ScoreAsync("   ", 0.5, CancellationToken.None);

            Assert.Equal(0, result.OriginalSize);
            Assert.Equal(0, result.CompressedSize);
            Assert.Empty(result.Errors);
        }

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

        [Fact]
        public async Task ScoreAsync_CancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() =>
                _scorer.ScoreAsync("some input", 0.5, cts.Token));
        }

        [Fact]
        public async Task ScoreAsync_SensitivityBelowZero_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _scorer.ScoreAsync("test", -0.1, CancellationToken.None));
        }

        [Fact]
        public async Task ScoreAsync_SensitivityAboveOne_Throws()
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _scorer.ScoreAsync("test", 1.1, CancellationToken.None));
        }

        [Fact]
        public async Task ScoreAsync_MultipleTyposInGroup_AllReported()
        {
            string inputText = "apple aple appple banana";

            var result = await _scorer.ScoreAsync(inputText, 0.02, CancellationToken.None);

            Assert.Equal(2, result.CompressedSize);
            Assert.Equal(2, result.Errors.Count);
            Assert.Contains(result.Errors, e => e.ErrorText == "aple");
            Assert.Contains(result.Errors, e => e.ErrorText == "appple");
        }

        [Fact]
        public async Task ScoreAsync_LeaderNotMostFrequent_MostFrequentWins()
        {
            string inputText = "aple apple apple apple";

            var result = await _scorer.ScoreAsync(inputText, 0.02, CancellationToken.None);

            Assert.Single(result.Errors);
            Assert.Equal("aple", result.Errors[0].ErrorText);
        }

        #endregion

        #region NormalizeAndExtractWords Direct Tests

        [Fact]
        public void Normalize_RemovesSpecialChars_AlphanumericOnly()
        {
            var words = WordScorer.NormalizeAndExtractWords("hello! world?", CancellationToken.None);

            Assert.Equal(2, words.Count);
            Assert.Equal("hello", words[0]);
            Assert.Equal("world", words[1]);
        }

        [Fact]
        public void Normalize_PreservesHyphens()
        {
            var words = WordScorer.NormalizeAndExtractWords("well-known term high-level", CancellationToken.None);

            Assert.Equal(3, words.Count);
            Assert.Contains("well-known", words);
            Assert.Contains("high-level", words);
        }

        [Fact]
        public void Normalize_FiltersWordsOverMaxLength()
        {
            var longWord = new string('a', 257);
            var input = $"short {longWord} another";

            var words = WordScorer.NormalizeAndExtractWords(input, CancellationToken.None);

            Assert.Equal(2, words.Count);
            Assert.DoesNotContain(longWord, words);
        }

        [Fact]
        public void Normalize_InputTooLong_ThrowsArgumentException()
        {
            var input = new string('a', 1_000_001);

            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.NormalizeAndExtractWords(input, CancellationToken.None));
            Assert.Contains("maximum length", ex.Message);
        }

        [Fact]
        public void Normalize_TooManyWords_ThrowsArgumentException()
        {
            var words = string.Join(" ", Enumerable.Repeat("word", 10_001));

            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.NormalizeAndExtractWords(words, CancellationToken.None));
            Assert.Contains("exceeding limit", ex.Message);
        }

        #endregion

        #region BuildSimilarityGroups Direct Tests

        [Fact]
        public void BuildGroups_EmptyList_ReturnsEmpty()
        {
            var groups = WordScorer.BuildSimilarityGroups(new List<string>(), 1, CancellationToken.None);

            Assert.Empty(groups);
        }

        [Fact]
        public void BuildGroups_SingleWord_ReturnsSingleGroup()
        {
            var groups = WordScorer.BuildSimilarityGroups(new List<string> { "apple" }, 1, CancellationToken.None);

            Assert.Single(groups);
            Assert.Single(groups[0]);
            Assert.Equal("apple", groups[0][0]);
        }

        [Fact]
        public void BuildGroups_FirstOccurrenceIsLeader()
        {
            var words = new List<string> { "aple", "apple" };

            var groups = WordScorer.BuildSimilarityGroups(words, 1, CancellationToken.None);

            Assert.Single(groups);
            Assert.Equal(2, groups[0].Count);
            Assert.Equal("aple", groups[0][0]);
        }

        [Fact]
        public void BuildGroups_ZeroEditDistance_AllSeparate()
        {
            var words = new List<string> { "cat", "dog", "fish" };

            var groups = WordScorer.BuildSimilarityGroups(words, 0, CancellationToken.None);

            Assert.Equal(3, groups.Count);
        }

        [Fact]
        public void BuildGroups_MaxEditDistance_GroupsCorrectly()
        {
            var words = new List<string> { "apple", "aple", "banana" };

            var groups = WordScorer.BuildSimilarityGroups(words, 1, CancellationToken.None);

            Assert.Equal(2, groups.Count);
            var appleGroup = groups.First(g => g.Contains("apple"));
            Assert.Contains("aple", appleGroup);
        }

        #endregion

        #region Analyze Refactoring Regression Tests

        [Fact]
        public async Task ScoreAsync_FreqDict_CorrectCountsForCaseVariants()
        {
            string inputText = "Apple aple APPLE apple";

            var result = await _scorer.ScoreAsync(inputText, 0.02, CancellationToken.None);

            Assert.Equal(4, result.OriginalSize);
            Assert.Equal(1, result.CompressedSize);

            var apleError = result.Errors.FirstOrDefault(e => e.ErrorText == "aple");
            Assert.NotNull(apleError);
            Assert.Equal(1, apleError.RepetitionCount);
        }

        [Fact]
        public async Task ScoreAsync_OriginalSize_MatchesNormalizedWordCount()
        {
            string inputText = "one two three four five";

            var normalizedWords = WordScorer.NormalizeAndExtractWords(inputText, CancellationToken.None);
            var result = await _scorer.ScoreAsync(inputText, 0.0, CancellationToken.None);

            Assert.Equal(normalizedWords.Count, result.OriginalSize);
        }

        [Fact]
        public async Task ScoreAsync_WordsExceedingMaxLength_ExcludedFromCount()
        {
            var longWord = new string('x', 257);
            string inputText = $"short {longWord} another";

            var result = await _scorer.ScoreAsync(inputText, 0.0, CancellationToken.None);

            Assert.Equal(2, result.OriginalSize);
            Assert.Equal(2, result.CompressedSize);
        }

        #endregion

        #region Model Constructor Validation Tests

        [Fact]
        public void WordScore_NegativeScore_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() => new WordScore("text", -1));
            Assert.Contains("Score", ex.Message);
        }

        [Fact]
        public void ErrorEntry_NullText_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ErrorEntry(null!, 1, new List<int> { 1 }));
        }

        [Fact]
        public void ErrorEntry_NullLines_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new ErrorEntry("text", 1, null!));
        }

        [Fact]
        public void FuzzyScorerResult_NegativeSize_Throws()
        {
            var ex1 = Assert.Throws<ArgumentException>(() =>
                new FuzzyScorerResult(-1, 0, new List<ErrorEntry>()));
            Assert.Contains("OriginalSize", ex1.Message);

            var ex2 = Assert.Throws<ArgumentException>(() =>
                new FuzzyScorerResult(0, -1, new List<ErrorEntry>()));
            Assert.Contains("CompressedSize", ex2.Message);
        }

        #endregion
    }
}
