using System;

namespace FuzzyScorer
{
    /// <summary>
    /// Controls normalization, limits, grouping, and typo selection.
    /// All properties are optional; the defaults preserve the 2.2.0 behavior.
    /// Limit values may make processing stricter but cannot exceed the hard limits
    /// declared on <see cref="WordScorer"/>.
    /// </summary>
    public sealed class FuzzyScorerOptions
    {
        /// <summary>
        /// Gets the maximum edit distance used for fuzzy grouping (default 1).
        /// Must be between 0 and <see cref="WordScorer.MaxEditDistanceLimit"/>.
        /// </summary>
        public int MaxEditDistance { get; init; } = 1;

        /// <summary>
        /// Gets the maximum input length in characters (default 1,000,000).
        /// Cannot exceed <see cref="WordScorer.MaxInputLength"/>.
        /// </summary>
        public int MaxInputLength { get; init; } = WordScorer.MaxInputLength;

        /// <summary>
        /// Gets the maximum number of words per input (default 10,000).
        /// Cannot exceed <see cref="WordScorer.MaxWordsPerText"/>.
        /// </summary>
        public int MaxWordsPerText { get; init; } = WordScorer.MaxWordsPerText;

        /// <summary>
        /// Gets the maximum length of a single word (default 256).
        /// Longer words are dropped. Cannot exceed <see cref="WordScorer.MaxWordLength"/>.
        /// </summary>
        public int MaxWordLength { get; init; } = WordScorer.MaxWordLength;

        /// <summary>
        /// Gets the minimum word length eligible for fuzzy merging (default 0, meaning
        /// all lengths). Shorter words still group with exact case-insensitive matches
        /// but are never merged with different spellings.
        /// </summary>
        public int MinWordLengthForFuzzy { get; init; }

        /// <summary>
        /// Gets a value indicating whether diacritics are removed before analysis
        /// (default <see langword="false"/>). Useful for matching "cafe" with "café".
        /// </summary>
        public bool IgnoreDiacritics { get; init; }

        /// <summary>
        /// Gets a value indicating whether leading and trailing hyphens are trimmed
        /// from words (default <see langword="true"/>).
        /// </summary>
        public bool StripEdgeHyphens { get; init; } = true;

        /// <summary>
        /// Gets the rule used to pick a group representative when word frequencies tie
        /// (default <see cref="TieBreakMode.LongestThenOrdinal"/>).
        /// </summary>
        public TieBreakMode TieBreak { get; init; } = TieBreakMode.LongestThenOrdinal;

        /// <summary>
        /// Gets the grouping strategy (default <see cref="GroupingMode.Leader"/>).
        /// </summary>
        public GroupingMode Grouping { get; init; } = GroupingMode.Leader;

        internal void Validate()
        {
            if (MaxEditDistance < 0 || MaxEditDistance > WordScorer.MaxEditDistanceLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxEditDistance),
                    MaxEditDistance,
                    $"MaxEditDistance must be between 0 and {WordScorer.MaxEditDistanceLimit}.");
            }

            if (MaxInputLength < 1 || MaxInputLength > WordScorer.MaxInputLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxInputLength),
                    MaxInputLength,
                    $"MaxInputLength must be between 1 and {WordScorer.MaxInputLength}.");
            }

            if (MaxWordsPerText < 1 || MaxWordsPerText > WordScorer.MaxWordsPerText)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxWordsPerText),
                    MaxWordsPerText,
                    $"MaxWordsPerText must be between 1 and {WordScorer.MaxWordsPerText}.");
            }

            if (MaxWordLength < 1 || MaxWordLength > WordScorer.MaxWordLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MaxWordLength),
                    MaxWordLength,
                    $"MaxWordLength must be between 1 and {WordScorer.MaxWordLength}.");
            }

            if (MinWordLengthForFuzzy < 0 || MinWordLengthForFuzzy > MaxWordLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(MinWordLengthForFuzzy),
                    MinWordLengthForFuzzy,
                    $"MinWordLengthForFuzzy must be between 0 and {MaxWordLength}.");
            }
        }
    }
}
