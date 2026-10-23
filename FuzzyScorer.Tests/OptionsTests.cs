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
    /// Tests for the 2.3.0 options API, result enrichments, and new naming.
    /// </summary>
    public class OptionsTests
    {
        private readonly FuzzyScorer _scorer = new FuzzyScorer();

        #region Options validation

        [Fact]
        public void Options_InvalidMaxEditDistance_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WordScorer.GetWordFrequencies("text", new FuzzyScorerOptions { MaxEditDistance = -1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WordScorer.GetWordFrequencies("text", new FuzzyScorerOptions { MaxEditDistance = 51 }));
        }

        [Fact]
        public void Options_InvalidLimits_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WordScorer.GetWordFrequencies("text", new FuzzyScorerOptions { MaxInputLength = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WordScorer.GetWordFrequencies("text", new FuzzyScorerOptions { MaxInputLength = WordScorer.MaxInputLength + 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WordScorer.GetWordFrequencies("text", new FuzzyScorerOptions { MaxWordLength = 0 }));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WordScorer.GetWordFrequencies("text", new FuzzyScorerOptions { MinWordLengthForFuzzy = 1_000 }));
        }

        [Fact]
        public void Options_LowerLimit_IsEnforced()
        {
            var options = new FuzzyScorerOptions { MaxWordsPerText = 2 };

            var ex = Assert.Throws<ArgumentException>(() =>
                WordScorer.GetWordFrequencies("one two three", options));
            Assert.Contains("exceeding limit", ex.Message);
        }

        #endregion

        #region Normalization options

        [Fact]
        public void IgnoreDiacritics_MergesAccentedVariants()
        {
            var merged = WordScorer.GetWordFrequencies(
                "cafe café",
                new FuzzyScorerOptions { IgnoreDiacritics = true });

            var entry = Assert.Single(merged);
            Assert.Equal(2, entry.Count);

            var separate = WordScorer.GetWordFrequencies("cafe café");
            Assert.Equal(2, separate.Count);
        }

        [Fact]
        public void StripEdgeHyphens_CanBeDisabled()
        {
            var trimmed = WordScorer.NormalizeAndExtractWords("--foo--", CancellationToken.None);
            Assert.Equal(new List<string> { "foo" }, trimmed);

            var preserved = WordScorer.NormalizeAndExtractWords(
                "--foo--",
                new FuzzyScorerOptions { StripEdgeHyphens = false },
                CancellationToken.None);
            Assert.Equal(new List<string> { "--foo--" }, preserved);
        }

        #endregion

        #region Grouping options

        [Fact]
        public void MinWordLengthForFuzzy_SkipsShortWords()
        {
            var defaults = WordScorer.GroupSimilarWords("cat cot apple aple", maxEditDistance: 1);
            Assert.Equal(2, defaults.Count);

            var options = new FuzzyScorerOptions
            {
                MaxEditDistance = 1,
                MinWordLengthForFuzzy = 4,
            };

            var results = WordScorer.GroupSimilarWords("cat cot apple aple", options);

            Assert.Equal(3, results.Count);
            Assert.Equal(2, results.First(result => result.Text == "apple").Count);
            Assert.Equal(1, results.First(result => result.Text == "cat").Count);
        }

        [Fact]
        public void ConnectedComponents_MergesTransitively()
        {
            var words = new[] { "apple", "aple", "ale" };

            var leaderGroups = WordScorer.GroupWordsBySimilarity(
                words,
                1,
                CancellationToken.None);
            Assert.Equal(2, leaderGroups.Count);

            var componentGroups = WordScorer.GroupWordsBySimilarity(
                words,
                new FuzzyScorerOptions { MaxEditDistance = 1, Grouping = GroupingMode.ConnectedComponents },
                CancellationToken.None);

            var group = Assert.Single(componentGroups);
            Assert.Equal(words, group);
        }

        [Fact]
        public void AreWordsSimilar_Options_UsesConfiguredDistance()
        {
            Assert.True(WordScorer.AreWordsSimilar("abc", "axc", new FuzzyScorerOptions { MaxEditDistance = 1 }));
            Assert.False(WordScorer.AreWordsSimilar("abc", "axc", new FuzzyScorerOptions { MaxEditDistance = 0 }));
        }

        #endregion

        #region Tie-breaking and suggestions

        [Fact]
        public async Task TieBreak_FirstOccurrence_KeepsFirstVariant()
        {
            var options = new FuzzyScorerOptions
            {
                MaxEditDistance = 1,
                TieBreak = TieBreakMode.FirstOccurrence,
            };

            var result = await _scorer.ScoreAsync("aple apple", options);

            var error = Assert.Single(result.Errors);
            Assert.Equal("apple", error.Text);
            Assert.Equal("aple", error.SuggestedText);
            Assert.False(error.IsCertain);
        }

        [Fact]
        public async Task TieBreak_LongestThenOrdinal_IsUncertainOnTie()
        {
            var result = await _scorer.ScoreAsync(
                "aple apple",
                new FuzzyScorerOptions { MaxEditDistance = 1 });

            var error = Assert.Single(result.Errors);
            Assert.Equal("aple", error.Text);
            Assert.Equal("apple", error.SuggestedText);
            Assert.False(error.IsCertain);
        }

        [Fact]
        public async Task TieBreak_ReportAllTies_ReportsEveryVariant()
        {
            var options = new FuzzyScorerOptions
            {
                MaxEditDistance = 1,
                TieBreak = TieBreakMode.ReportAllTies,
            };

            var result = await _scorer.ScoreAsync("aple apple", options);

            Assert.Equal(2, result.Errors.Count);
            Assert.All(result.Errors, error =>
            {
                Assert.Null(error.SuggestedText);
                Assert.False(error.IsCertain);
            });
        }

        [Fact]
        public async Task Representative_WithoutTie_IsCertain()
        {
            var result = await _scorer.ScoreAsync(
                "apple apple aple",
                new FuzzyScorerOptions { MaxEditDistance = 1 });

            var error = Assert.Single(result.Errors);
            Assert.Equal("aple", error.Text);
            Assert.Equal("apple", error.SuggestedText);
            Assert.True(error.IsCertain);
        }

        #endregion

        #region Result enrichment

        [Fact]
        public async Task Result_ExposesGroupsAndEditDistance()
        {
            var result = await _scorer.ScoreAsync(
                "apple aple apple",
                new FuzzyScorerOptions { MaxEditDistance = 1 });

            Assert.Equal(3, result.TotalWords);
            Assert.Equal(1, result.UniqueGroups);
            Assert.Equal(1, result.MaxEditDistance);

            var group = Assert.Single(result.WordGroups);
            Assert.Equal("apple", group.Representative);
            Assert.Equal(new List<string> { "apple", "aple" }, group.Members);
            Assert.Equal(3, group.Count);
        }

        [Fact]
        public async Task Score_Options_MinWordLength_IsApplied()
        {
            var result = await _scorer.ScoreAsync(
                "cat cot",
                new FuzzyScorerOptions { MaxEditDistance = 1, MinWordLengthForFuzzy = 4 });

            Assert.Equal(2, result.UniqueGroups);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public void ResultEnvelope_ValidatesRanges()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new FuzzyScorerResult(1, 1, new List<ErrorEntry>(), 51, Array.Empty<WordGroup>()));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new ErrorEntry("a", -1, new List<int> { 1 }, null, true));
        }

        [Fact]
        public void WordGroup_ValidatesArguments()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new WordGroup(null!, new List<string> { "a" }, 1));
            Assert.Throws<ArgumentNullException>(() =>
                new WordGroup("a", null!, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WordGroup("a", new List<string> { "a" }, -1));
        }

        #endregion

        #region Sync API

        [Fact]
        public void Score_ReturnsResultSynchronously()
        {
            var scorer = new FuzzyScorer();

            var result = scorer.Score("apple aple apple", 0.02);

            Assert.Equal(3, result.TotalWords);
            Assert.Single(result.Errors);
        }

        [Fact]
        public async Task ScoreAsync_DefaultCancellationToken_IsAccepted()
        {
            var result = await _scorer.ScoreAsync("apple aple", 0.02);

            Assert.Equal(1, result.MaxEditDistance);
            Assert.Single(result.Errors);
        }

        #endregion

        #region Obsolete aliases

        [Fact]
        public void ObsoleteAliases_ForwardToNewProperties()
        {
#pragma warning disable CS0618
            var word = new WordScore("apple", 3);
            Assert.Equal(3, word.Score);

            var error = new ErrorEntry("aple", 2, new List<int> { 1 });
            Assert.Equal("aple", error.ErrorText);
            Assert.Equal(2, error.RepetitionCount);

            var result = new FuzzyScorerResult(3, 2, new[] { error });
            Assert.Equal(3, result.OriginalSize);
            Assert.Equal(2, result.CompressedSize);
#pragma warning restore CS0618
        }

        #endregion
    }
}
