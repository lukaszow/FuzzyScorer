namespace FuzzyScorer
{
    /// <summary>
    /// Determines how similarity groups are formed.
    /// </summary>
    public enum GroupingMode
    {
        /// <summary>
        /// Each word is compared with the group leaders; the first matching leader wins.
        /// Results are order-dependent, but each comparison is limited to the leaders.
        /// </summary>
        Leader,

        /// <summary>
        /// Similar words are connected transitively (union-find). Slower for large
        /// vocabularies because every pair of distinct words is considered, subject to
        /// the length filter and the comparison budget.
        /// </summary>
        ConnectedComponents,
    }
}
