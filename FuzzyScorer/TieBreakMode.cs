namespace FuzzyScorer
{
    /// <summary>
    /// Determines which word becomes the representative when frequencies tie.
    /// </summary>
    public enum TieBreakMode
    {
        /// <summary>The first occurrence in the group wins.</summary>
        FirstOccurrence,

        /// <summary>The longest variant wins; remaining ties are resolved by ordinal order.</summary>
        LongestThenOrdinal,

        /// <summary>
        /// Same representative as <see cref="LongestThenOrdinal"/>, but when the top
        /// frequency is shared, every variant is reported as uncertain.
        /// </summary>
        ReportAllTies,
    }
}
