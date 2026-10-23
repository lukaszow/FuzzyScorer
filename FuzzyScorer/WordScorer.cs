using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace FuzzyScorer
{
    /// <summary>
    /// Provides static helpers for word frequency counting and fuzzy word grouping
    /// using the Levenshtein edit distance.
    /// </summary>
    public class WordScorer
    {
        /// <summary>Maximum allowed edit distance (50).</summary>
        public const int MaxEditDistanceLimit = 50;

        /// <summary>Maximum allowed input length in characters (1,000,000).</summary>
        public const int MaxInputLength = 1_000_000;

        /// <summary>Maximum allowed words per text input (10,000).</summary>
        public const int MaxWordsPerText = 10_000;

        /// <summary>Maximum allowed length of a single word in characters (256).</summary>
        public const int MaxWordLength = 256;

        /// <summary>
        /// Maximum number of edit-distance computations performed by
        /// <c>BuildSimilarityGroups</c> (10,000,000). Acts as a hard bound
        /// against quadratic-time inputs; exceeding it throws <see cref="ArgumentException"/>.
        /// Distinct words are compared once, so repeated occurrences do not consume budget.
        /// </summary>
        public const int MaxSimilarityComparisons = 10_000_000;

        /// <summary>
        /// Thresholds up to this value use the metric index; larger thresholds use the
        /// linear scan, where an early match is usually found after a few comparisons.
        /// </summary>
        private const int IndexedSearchMaxEditDistance = 3;

        /// <summary>Thresholds up to this value can use the deletion-variant index.</summary>
        private const int DeletionIndexMaxEditDistance = 2;

        /// <summary>Upper bound on deletion variants held in memory.</summary>
        private const long MaxIndexedVariants = 2_000_000;

        /// <summary>
        /// Minimum word count before the metric index pays for its overhead.
        /// Small inputs are faster with the linear scan and its early exit.
        /// </summary>
        private const int IndexedSearchMinWords = 3_000;

        /// <summary>Buffers up to this size are stack-allocated; larger ones are pooled.</summary>
        private const int StackBufferThreshold = 512;

        internal static readonly Regex WordNormalizationRegex = new Regex(@"[^\p{L}\p{N}'\u2019\-]+", RegexOptions.Compiled);

        private static readonly FuzzyScorerOptions DefaultOptions = new FuzzyScorerOptions();

        /// <summary>
        /// Returns word frequency counts from the input text.
        /// Comparisons are case-insensitive (e.g., "Apple" and "apple" are treated as the same word).
        /// </summary>
        /// <param name="inputText">The raw text to analyze for word frequency.</param>
        /// <returns>A list of WordScore objects, each containing a unique word and its frequency count.</returns>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        public static IReadOnlyList<WordScore> GetWordFrequencies(string? inputText)
        {
            return GetWordFrequencies(inputText, CancellationToken.None);
        }

        /// <summary>
        /// Returns word frequency counts from the input text.
        /// Supports cancellation token for long-running operations.
        /// </summary>
        /// <param name="inputText">The raw text to analyze for word frequency.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>A list of WordScore objects, each containing a unique word and its frequency count.</returns>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        /// <exception cref="OperationCanceledException">Thrown if operation is cancelled.</exception>
        public static IReadOnlyList<WordScore> GetWordFrequencies(string? inputText, CancellationToken cancellationToken)
        {
            return GetWordFrequencies(inputText, DefaultOptions, cancellationToken);
        }

        /// <summary>
        /// Returns word frequency counts using explicit normalization and limit options.
        /// </summary>
        /// <param name="inputText">The raw text to analyze for word frequency.</param>
        /// <param name="options">Normalization and limit options.</param>
        /// <returns>A list of WordScore objects, each containing a unique word and its frequency count.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if an option is out of range.</exception>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        public static IReadOnlyList<WordScore> GetWordFrequencies(string? inputText, FuzzyScorerOptions options)
        {
            return GetWordFrequencies(inputText, options, CancellationToken.None);
        }

        /// <summary>
        /// Returns word frequency counts using explicit normalization and limit options.
        /// </summary>
        /// <param name="inputText">The raw text to analyze for word frequency.</param>
        /// <param name="options">Normalization and limit options.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>A list of WordScore objects, each containing a unique word and its frequency count.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if an option is out of range.</exception>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        /// <exception cref="OperationCanceledException">Thrown if operation is cancelled.</exception>
        public static IReadOnlyList<WordScore> GetWordFrequencies(string? inputText, FuzzyScorerOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            if (string.IsNullOrWhiteSpace(inputText))
                return Array.Empty<WordScore>();

            var counts = CountWords(inputText, options, cancellationToken);
            var results = new List<WordScore>(counts.Count);

            foreach (var entry in counts)
                results.Add(new WordScore(entry.Key, entry.Value));

            return results;
        }

        /// <summary>
        /// Groups words that differ by at most <paramref name="maxEditDistance"/> edits,
        /// merging typos and small variations into the same group.
        /// The first occurrence of a word becomes the group representative.
        /// Results are order-dependent: different input ordering may produce different representatives.
        /// </summary>
        /// <param name="inputText">The raw text to analyze.</param>
        /// <param name="maxEditDistance">
        /// Maximum allowed difference for similarity. Must be between 0 and <see cref="MaxEditDistanceLimit"/>.
        /// </param>
        /// <returns>A list of WordScore objects where similar words are merged into a single entry.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxEditDistance"/> is outside the allowed range.</exception>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        public static IReadOnlyList<WordScore> GroupSimilarWords(string? inputText, int maxEditDistance)
        {
            return GroupSimilarWords(inputText, maxEditDistance, CancellationToken.None);
        }

        /// <summary>
        /// Groups words that differ by at most <paramref name="maxEditDistance"/> edits,
        /// merging typos and small variations into the same group.
        /// Supports cancellation token for long-running operations.
        /// </summary>
        /// <param name="inputText">The raw text to analyze.</param>
        /// <param name="maxEditDistance">
        /// Maximum allowed difference for similarity. Must be between 0 and <see cref="MaxEditDistanceLimit"/>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>A list of WordScore objects where similar words are merged into a single entry.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxEditDistance"/> is outside the allowed range.</exception>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        /// <exception cref="OperationCanceledException">Thrown if operation is cancelled.</exception>
        public static IReadOnlyList<WordScore> GroupSimilarWords(string? inputText, int maxEditDistance, CancellationToken cancellationToken)
        {
            ValidateEditDistance(maxEditDistance);

            return GroupSimilarWords(
                inputText,
                new FuzzyScorerOptions { MaxEditDistance = maxEditDistance },
                cancellationToken);
        }

        /// <summary>
        /// Groups similar words using explicit options (edit distance, limits, minimum
        /// length for fuzzy matching, and grouping mode).
        /// </summary>
        /// <param name="inputText">The raw text to analyze.</param>
        /// <param name="options">Grouping and normalization options.</param>
        /// <returns>A list of WordScore objects where similar words are merged into a single entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if an option is out of range.</exception>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        public static IReadOnlyList<WordScore> GroupSimilarWords(string? inputText, FuzzyScorerOptions options)
        {
            return GroupSimilarWords(inputText, options, CancellationToken.None);
        }

        /// <summary>
        /// Groups similar words using explicit options (edit distance, limits, minimum
        /// length for fuzzy matching, and grouping mode).
        /// </summary>
        /// <param name="inputText">The raw text to analyze.</param>
        /// <param name="options">Grouping and normalization options.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>A list of WordScore objects where similar words are merged into a single entry.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if an option is out of range.</exception>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        /// <exception cref="OperationCanceledException">Thrown if operation is cancelled.</exception>
        public static IReadOnlyList<WordScore> GroupSimilarWords(string? inputText, FuzzyScorerOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            if (string.IsNullOrWhiteSpace(inputText))
                return Array.Empty<WordScore>();

            var allWords = NormalizeAndExtractWords(inputText, options, cancellationToken);
            return GroupBySimilarity(allWords, options, cancellationToken);
        }

        /// <summary>
        /// Groups a list of words by similarity. Each inner list contains all words
        /// assigned to one similarity group. The first occurrence becomes the group's leader.
        /// Results are order-dependent.
        /// </summary>
        /// <param name="words">The list of words to group.</param>
        /// <param name="maxEditDistance">
        /// Maximum edit distance for similarity. Must be between 0 and <see cref="MaxEditDistanceLimit"/>.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of similarity groups.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="words"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="words"/> contains a null element.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxEditDistance"/> is outside the allowed range.</exception>
        public static IReadOnlyList<IReadOnlyList<string>> GroupWordsBySimilarity(IEnumerable<string> words, int maxEditDistance, CancellationToken cancellationToken)
        {
            ValidateEditDistance(maxEditDistance);

            return GroupWordsBySimilarity(
                words,
                new FuzzyScorerOptions { MaxEditDistance = maxEditDistance },
                cancellationToken);
        }

        /// <summary>
        /// Groups a pre-normalized word sequence using explicit options.
        /// </summary>
        /// <param name="words">The words to group.</param>
        /// <param name="options">Grouping options.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of similarity groups.</returns>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if an option is out of range.</exception>
        /// <exception cref="ArgumentException">Thrown if the collection contains a null element or the budget is exceeded.</exception>
        public static IReadOnlyList<IReadOnlyList<string>> GroupWordsBySimilarity(IEnumerable<string> words, FuzzyScorerOptions options, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(words);
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            return BuildSimilarityGroups(words, options, cancellationToken);
        }

        /// <summary>
        /// Returns true if the two words are similar within the given edit distance threshold.
        /// Comparisons are case-insensitive.
        /// </summary>
        /// <param name="word1">First word.</param>
        /// <param name="word2">Second word.</param>
        /// <param name="maxEditDistance">
        /// Maximum allowed edit distance. Must be between 0 and <see cref="MaxEditDistanceLimit"/>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if word1 or word2 is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if maxEditDistance is out of range.</exception>
        public static bool AreWordsSimilar(string word1, string word2, int maxEditDistance)
        {
            ArgumentNullException.ThrowIfNull(word1);
            ArgumentNullException.ThrowIfNull(word2);

            ValidateEditDistance(maxEditDistance);

            return ComputeEditDistance(
                word1.ToLowerInvariant(),
                word2.ToLowerInvariant(),
                maxEditDistance) >= 0;
        }

        /// <summary>
        /// Returns true if the two words are similar within the configured edit distance.
        /// Comparisons are case-insensitive.
        /// </summary>
        /// <param name="word1">First word.</param>
        /// <param name="word2">Second word.</param>
        /// <param name="options">Options providing <see cref="FuzzyScorerOptions.MaxEditDistance"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if an option is out of range.</exception>
        public static bool AreWordsSimilar(string word1, string word2, FuzzyScorerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            return AreWordsSimilar(word1, word2, options.MaxEditDistance);
        }

        /// <summary>
        /// Groups words by similarity and returns the original word groups.
        /// Each inner list contains all words assigned to one similarity group.
        /// The first occurrence of a word becomes the group's leader.
        /// Results are order-dependent.
        /// </summary>
        internal static IReadOnlyList<IReadOnlyList<string>> GetWordGroups(string? inputText, int maxEditDistance, CancellationToken cancellationToken)
        {
            ValidateEditDistance(maxEditDistance);

            if (string.IsNullOrWhiteSpace(inputText))
                return Array.Empty<IReadOnlyList<string>>();

            var allWords = NormalizeAndExtractWords(inputText, cancellationToken);
            return BuildSimilarityGroups(allWords, maxEditDistance, cancellationToken);
        }

        /// <summary>
        /// Groups words by similarity. The first occurrence of a word becomes
        /// the group's representative. Results are order-dependent.
        /// </summary>
        private static List<WordScore> GroupBySimilarity(
            IReadOnlyList<string> words,
            FuzzyScorerOptions options,
            CancellationToken cancellationToken)
        {
            var groups = BuildSimilarityGroups(words, options, cancellationToken);

            return groups
                .Select(group => new WordScore(group[0], group.Count))
                .ToList();
        }

        /// <summary>
        /// Builds similarity groups using the supplied options.
        /// </summary>
        internal static IReadOnlyList<IReadOnlyList<string>> BuildSimilarityGroups(
            IEnumerable<string> words,
            FuzzyScorerOptions options,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            return BuildSimilarityGroups(
                words,
                options.MaxEditDistance,
                MaxSimilarityComparisons,
                forceIndex: false,
                options.MinWordLengthForFuzzy,
                options.Grouping,
                cancellationToken);
        }

        /// <summary>
        /// Builds similarity groups for a collection of words. Each inner list contains
        /// all words assigned to one group (including repeated occurrences). The first
        /// occurrence becomes the group leader. Each distinct word is compared against
        /// the group leaders only once; repeated occurrences reuse the assigned group.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if words is null.</exception>
        /// <exception cref="ArgumentException">Thrown if the collection contains a null element or the comparison budget is exceeded.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if maxEditDistance is out of range.</exception>
        internal static IReadOnlyList<IReadOnlyList<string>> BuildSimilarityGroups(IEnumerable<string> words, int maxEditDistance, CancellationToken cancellationToken)
        {
            return BuildSimilarityGroups(
                words,
                maxEditDistance,
                MaxSimilarityComparisons,
                forceIndex: false,
                minWordLengthForFuzzy: 0,
                GroupingMode.Leader,
                cancellationToken);
        }

        /// <summary>
        /// Builds similarity groups with an explicit comparison budget. The overload with
        /// the public limit is used by callers; this one exists so tests can verify the
        /// budget guard without performing millions of comparisons.
        /// </summary>
        internal static IReadOnlyList<IReadOnlyList<string>> BuildSimilarityGroups(IEnumerable<string> words, int maxEditDistance, long comparisonLimit, CancellationToken cancellationToken)
        {
            return BuildSimilarityGroups(
                words,
                maxEditDistance,
                comparisonLimit,
                forceIndex: false,
                minWordLengthForFuzzy: 0,
                GroupingMode.Leader,
                cancellationToken);
        }

        /// <summary>
        /// Builds similarity groups. <paramref name="forceIndex"/> lets tests exercise the
        /// indexed paths on small corpora; production callers rely on the size thresholds.
        /// </summary>
        internal static IReadOnlyList<IReadOnlyList<string>> BuildSimilarityGroups(IEnumerable<string> words, int maxEditDistance, long comparisonLimit, bool forceIndex, CancellationToken cancellationToken)
        {
            return BuildSimilarityGroups(
                words,
                maxEditDistance,
                comparisonLimit,
                forceIndex,
                minWordLengthForFuzzy: 0,
                GroupingMode.Leader,
                cancellationToken);
        }

        /// <summary>
        /// Builds similarity groups with explicit budget, minimum fuzzy length, and
        /// grouping mode.
        /// </summary>
        internal static IReadOnlyList<IReadOnlyList<string>> BuildSimilarityGroups(
            IEnumerable<string> words,
            int maxEditDistance,
            long comparisonLimit,
            bool forceIndex,
            int minWordLengthForFuzzy,
            GroupingMode groupingMode,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(words);

            ValidateEditDistance(maxEditDistance);

            if (minWordLengthForFuzzy < 0 || minWordLengthForFuzzy > MaxWordLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minWordLengthForFuzzy),
                    minWordLengthForFuzzy,
                    $"minWordLengthForFuzzy must be between 0 and {MaxWordLength}.");
            }

            if (groupingMode is not GroupingMode.Leader and not GroupingMode.ConnectedComponents)
                throw new ArgumentOutOfRangeException(nameof(groupingMode), groupingMode, "Unknown grouping mode.");

            var wordList = words as List<string> ?? words.ToList();

            if (groupingMode == GroupingMode.ConnectedComponents)
            {
                return BuildConnectedComponents(
                    wordList,
                    maxEditDistance,
                    comparisonLimit,
                    minWordLengthForFuzzy,
                    cancellationToken);
            }

            var groups = new List<List<string>>();
            var leaderLowers = new List<string>();
            var leaderLengths = new List<int>();
            var groupByLowerWord = new Dictionary<string, int>(StringComparer.Ordinal);
            var budget = new ComparisonBudget(comparisonLimit);
            bool fuzzyEnabled = maxEditDistance > 0;

            IWordIndex? index = null;
            if (fuzzyEnabled && maxEditDistance <= DeletionIndexMaxEditDistance
                && EstimateVariantCount(wordList, maxEditDistance) <= MaxIndexedVariants)
            {
                index = new DeletionIndex(maxEditDistance, budget);
            }
            else if (fuzzyEnabled && maxEditDistance <= IndexedSearchMaxEditDistance
                && (forceIndex || wordList.Count >= IndexedSearchMinWords))
            {
                index = new SimilarityIndex(budget);
            }

            foreach (var word in wordList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (word is null)
                    throw new ArgumentException("Word collection cannot contain null elements.", nameof(words));

                var lowerWord = word.ToLowerInvariant();

                if (groupByLowerWord.TryGetValue(lowerWord, out int existingGroup))
                {
                    groups[existingGroup].Add(word);
                    continue;
                }

                int matchIndex = -1;

                if (fuzzyEnabled && lowerWord.Length >= minWordLengthForFuzzy)
                {
                    if (index is not null)
                    {
                        matchIndex = index.FindFirstMatch(lowerWord, maxEditDistance);
                    }
                    else
                    {
                        for (int i = 0; i < groups.Count; i++)
                        {
                            if (leaderLengths[i] < minWordLengthForFuzzy)
                                continue;

                            if (Math.Abs(lowerWord.Length - leaderLengths[i]) > maxEditDistance)
                                continue;

                            budget.Count();
                            if (ComputeEditDistance(lowerWord, leaderLowers[i], maxEditDistance) >= 0)
                            {
                                matchIndex = i;
                                break;
                            }
                        }
                    }
                }

                if (matchIndex >= 0)
                {
                    groups[matchIndex].Add(word);
                    groupByLowerWord[lowerWord] = matchIndex;
                }
                else
                {
                    groupByLowerWord[lowerWord] = groups.Count;
                    groups.Add(new List<string> { word });
                    leaderLowers.Add(lowerWord);
                    leaderLengths.Add(lowerWord.Length);

                    if (lowerWord.Length >= minWordLengthForFuzzy)
                        index?.Add(lowerWord, groups.Count - 1);
                }
            }

            return (IReadOnlyList<IReadOnlyList<string>>)groups;
        }

        /// <summary>
        /// Groups words by transitive similarity (connected components). Every pair of
        /// distinct words is considered, subject to the length filter and the budget.
        /// </summary>
        private static List<IReadOnlyList<string>> BuildConnectedComponents(
            List<string> words,
            int maxEditDistance,
            long comparisonLimit,
            int minWordLengthForFuzzy,
            CancellationToken cancellationToken)
        {
            var uniqueWords = new List<string>();
            var lowerWords = new List<string>();
            var indexByLowerWord = new Dictionary<string, int>(StringComparer.Ordinal);

            foreach (var word in words)
            {
                if (word is null)
                    throw new ArgumentException("Word collection cannot contain null elements.", nameof(words));

                var lowerWord = word.ToLowerInvariant();
                if (indexByLowerWord.ContainsKey(lowerWord))
                    continue;

                indexByLowerWord[lowerWord] = uniqueWords.Count;
                uniqueWords.Add(word);
                lowerWords.Add(lowerWord);
            }

            var parents = new int[uniqueWords.Count];
            for (int i = 0; i < parents.Length; i++)
                parents[i] = i;

            var budget = new ComparisonBudget(comparisonLimit);

            for (int i = 0; i < uniqueWords.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (lowerWords[i].Length < minWordLengthForFuzzy)
                    continue;

                for (int j = i + 1; j < uniqueWords.Count; j++)
                {
                    if (lowerWords[j].Length < minWordLengthForFuzzy)
                        continue;

                    if (Math.Abs(lowerWords[i].Length - lowerWords[j].Length) > maxEditDistance)
                        continue;

                    budget.Count();
                    if (ComputeEditDistance(lowerWords[i], lowerWords[j], maxEditDistance) >= 0)
                        Union(parents, i, j);
                }
            }

            var groupsByRoot = new Dictionary<int, List<string>>();
            var groupOrder = new List<int>();

            foreach (var word in words)
            {
                int root = Find(parents, indexByLowerWord[word.ToLowerInvariant()]);

                if (!groupsByRoot.TryGetValue(root, out var group))
                {
                    group = new List<string>();
                    groupsByRoot[root] = group;
                    groupOrder.Add(root);
                }

                group.Add(word);
            }

            var result = new List<IReadOnlyList<string>>(groupOrder.Count);
            foreach (int root in groupOrder)
                result.Add(groupsByRoot[root]);

            return result;
        }

        private static int Find(int[] parents, int index)
        {
            while (parents[index] != index)
            {
                parents[index] = parents[parents[index]];
                index = parents[index];
            }

            return index;
        }

        private static void Union(int[] parents, int first, int second)
        {
            int firstRoot = Find(parents, first);
            int secondRoot = Find(parents, second);

            if (firstRoot == secondRoot)
                return;

            if (firstRoot < secondRoot)
                parents[secondRoot] = firstRoot;
            else
                parents[firstRoot] = secondRoot;
        }

        /// <summary>
        /// Computes the edit distance between two strings without allocating a full matrix.
        /// Returns the distance when it is at most <paramref name="maxDistance"/>, or -1 otherwise.
        /// </summary>
        private static int ComputeEditDistance(string s, string t, int maxDistance)
        {
            if (maxDistance < 0)
                return -1;

            // The distance is symmetric; keep the column dimension as the shorter string
            // so the reused buffers stay small.
            if (t.Length > s.Length)
                (s, t) = (t, s);

            int n = s.Length;
            int m = t.Length;

            if (n - m > maxDistance)
                return -1;

            if (maxDistance == 0)
                return string.Equals(s, t, StringComparison.Ordinal) ? 0 : -1;

            if (m + 1 <= StackBufferThreshold)
            {
                Span<int> previous = stackalloc int[m + 1];
                Span<int> current = stackalloc int[m + 1];
                return ComputeEditDistanceCore(s, t, maxDistance, previous, current);
            }

            int[] rented = ArrayPool<int>.Shared.Rent(2 * (m + 1));
            try
            {
                return ComputeEditDistanceCore(
                    s,
                    t,
                    maxDistance,
                    rented.AsSpan(0, m + 1),
                    rented.AsSpan(m + 1, m + 1));
            }
            finally
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }

        private static int ComputeEditDistanceCore(string s, string t, int maxDistance, Span<int> previous, Span<int> current)
        {
            int n = s.Length;
            int m = t.Length;

            for (int j = 0; j <= m; j++)
                previous[j] = j <= maxDistance ? j : maxDistance + 1;

            for (int i = 1; i <= n; i++)
            {
                int start = Math.Max(1, i - maxDistance);
                int end = Math.Min(m, i + maxDistance);

                current[0] = i <= maxDistance ? i : maxDistance + 1;
                int rowMin = current[0];

                for (int j = 1; j <= m; j++)
                {
                    int value;
                    if (j < start || j > end)
                    {
                        value = maxDistance + 1;
                    }
                    else
                    {
                        int cost = t[j - 1] == s[i - 1] ? 0 : 1;
                        int deletion = previous[j] + 1;
                        int insertion = current[j - 1] + 1;
                        int substitution = previous[j - 1] + cost;
                        value = Math.Min(Math.Min(deletion, insertion), substitution);
                        if (value > maxDistance)
                            value = maxDistance + 1;
                    }

                    current[j] = value;
                    if (value < rowMin)
                        rowMin = value;
                }

                if (rowMin > maxDistance)
                    return -1;

                var swap = previous;
                previous = current;
                current = swap;
            }

            return previous[m] <= maxDistance ? previous[m] : -1;
        }

        /// <summary>
        /// Computes the exact edit distance between two strings. Required by the
        /// metric index, which relies on the triangle inequality.
        /// </summary>
        private static int ComputeExactDistance(string s, string t)
        {
            if (s.Length == 0)
                return t.Length;
            if (t.Length == 0)
                return s.Length;

            if (t.Length > s.Length)
                (s, t) = (t, s);

            int m = t.Length;

            if (m + 1 <= StackBufferThreshold)
            {
                Span<int> previous = stackalloc int[m + 1];
                Span<int> current = stackalloc int[m + 1];
                return ComputeExactDistanceCore(s, t, previous, current);
            }

            int[] rented = ArrayPool<int>.Shared.Rent(2 * (m + 1));
            try
            {
                return ComputeExactDistanceCore(
                    s,
                    t,
                    rented.AsSpan(0, m + 1),
                    rented.AsSpan(m + 1, m + 1));
            }
            finally
            {
                ArrayPool<int>.Shared.Return(rented);
            }
        }

        private static int ComputeExactDistanceCore(string s, string t, Span<int> previous, Span<int> current)
        {
            int n = s.Length;
            int m = t.Length;

            for (int j = 0; j <= m; j++)
                previous[j] = j;

            for (int i = 1; i <= n; i++)
            {
                current[0] = i;

                for (int j = 1; j <= m; j++)
                {
                    int cost = t[j - 1] == s[i - 1] ? 0 : 1;
                    int deletion = previous[j] + 1;
                    int insertion = current[j - 1] + 1;
                    int substitution = previous[j - 1] + cost;
                    current[j] = Math.Min(Math.Min(deletion, insertion), substitution);
                }

                var swap = previous;
                previous = current;
                current = swap;
            }

            return previous[m];
        }

        private static void ValidateEditDistance(int maxEditDistance)
        {
            if (maxEditDistance < 0 || maxEditDistance > MaxEditDistanceLimit)
                throw new ArgumentOutOfRangeException(
                    nameof(maxEditDistance),
                    maxEditDistance,
                    $"maxEditDistance must be between 0 and {MaxEditDistanceLimit}.");
        }

        /// <summary>
        /// Counts words in a single pass, preserving first-seen order and spelling.
        /// </summary>
        internal static List<KeyValuePair<string, int>> CountWords(string inputText, CancellationToken cancellationToken)
        {
            return CountWords(inputText, DefaultOptions, cancellationToken);
        }

        /// <summary>
        /// Counts words in a single pass, preserving first-seen order and spelling.
        /// </summary>
        internal static List<KeyValuePair<string, int>> CountWords(
            string inputText,
            FuzzyScorerOptions options,
            CancellationToken cancellationToken)
        {
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var order = new List<string>();

            foreach (var (word, _) in EnumerateWords(inputText, options, cancellationToken))
            {
                if (counts.TryGetValue(word, out int count))
                {
                    counts[word] = count + 1;
                }
                else
                {
                    counts.Add(word, 1);
                    order.Add(word);
                }
            }

            var results = new List<KeyValuePair<string, int>>(order.Count);
            foreach (var word in order)
                results.Add(new KeyValuePair<string, int>(word, counts[word]));

            return results;
        }

        /// <summary>
        /// Strips non-alphanumeric characters, splits into words, and enforces security limits.
        /// </summary>
        internal static List<string> NormalizeAndExtractWords(string inputText, CancellationToken cancellationToken)
        {
            return NormalizeAndExtractWords(inputText, DefaultOptions, cancellationToken);
        }

        /// <summary>
        /// Strips non-alphanumeric characters, splits into words, and enforces the supplied limits.
        /// </summary>
        internal static List<string> NormalizeAndExtractWords(
            string inputText,
            FuzzyScorerOptions options,
            CancellationToken cancellationToken)
        {
            return EnumerateWords(inputText, options, cancellationToken)
                .Select(entry => entry.Word)
                .ToList();
        }

        /// <summary>
        /// Normalizes text to Unicode canonical composition (form C).
        /// Returns the original text when it contains invalid UTF-16 sequences.
        /// </summary>
        internal static string NormalizeUnicode(string text)
        {
            return NormalizeUnicode(text, ignoreDiacritics: false);
        }

        /// <summary>
        /// Normalizes text to Unicode form C, optionally removing diacritics.
        /// Returns the original text when it contains invalid UTF-16 sequences.
        /// </summary>
        internal static string NormalizeUnicode(string text, bool ignoreDiacritics)
        {
            try
            {
                if (!ignoreDiacritics)
                    return text.Normalize(NormalizationForm.FormC);

                var decomposed = text.Normalize(NormalizationForm.FormD);
                var builder = new StringBuilder(decomposed.Length);

                foreach (char character in decomposed)
                {
                    if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                        builder.Append(character);
                }

                return builder.ToString().Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                // Invalid surrogate pairs cannot be normalized; process the raw text instead.
                return text;
            }
        }

        /// <summary>
        /// Replaces punctuation and control characters with spaces, splits the line
        /// into words, removes apostrophes and edge hyphens, and enforces the word length limit.
        /// </summary>
        internal static List<string> TokenizeLine(string line)
        {
            return TokenizeLine(line, MaxWordLength, stripEdgeHyphens: true);
        }

        /// <summary>
        /// Replaces punctuation and control characters with spaces, splits the line
        /// into words, and applies the supplied word rules.
        /// </summary>
        internal static List<string> TokenizeLine(string line, int maxWordLength, bool stripEdgeHyphens)
        {
            var normalized = WordNormalizationRegex.Replace(line, " ");
            var words = new List<string>();

            foreach (var rawWord in normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                var word = NormalizeWord(rawWord, stripEdgeHyphens);
                if (word.Length == 0 || word.Length > maxWordLength)
                    continue;

                words.Add(word);
            }

            return words;
        }

        private static string NormalizeWord(string word, bool stripEdgeHyphens)
        {
            // Apostrophes are removed instead of treated as separators so that
            // contractions remain a single token ("don't" -> "dont").
            var normalized = word
                .Replace("'", string.Empty)
                .Replace("\u2019", string.Empty);

            return stripEdgeHyphens ? normalized.Trim('-') : normalized;
        }

        /// <summary>
        /// Normalizes the input and yields each word with its 1-based line number,
        /// enforcing the security limits.
        /// </summary>
        private static IEnumerable<(string Word, int LineNumber)> EnumerateWords(
            string inputText,
            FuzzyScorerOptions options,
            CancellationToken cancellationToken)
        {
            if (inputText.Length > options.MaxInputLength)
                throw new ArgumentException($"Input exceeds maximum length of {options.MaxInputLength} characters", nameof(inputText));

            var lines = NormalizeUnicode(inputText, options.IgnoreDiacritics).Split('\n');
            int wordCount = 0;

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int lineNumber = lineIndex + 1;
                foreach (var word in TokenizeLine(lines[lineIndex], options.MaxWordLength, options.StripEdgeHyphens))
                {
                    wordCount++;
                    if (wordCount > options.MaxWordsPerText)
                        throw new ArgumentException($"Input contains more than {options.MaxWordsPerText} words, exceeding limit of {options.MaxWordsPerText}", nameof(inputText));

                    yield return (word, lineNumber);
                }
            }
        }

        /// <summary>
        /// Normalizes the input, splits it into words, records the 1-based
        /// line number of each occurrence, and enforces security limits.
        /// </summary>
        internal static List<(string Word, int LineNumber)> NormalizeAndExtractWordsWithLines(string inputText, CancellationToken cancellationToken)
        {
            return NormalizeAndExtractWordsWithLines(inputText, DefaultOptions, cancellationToken);
        }

        /// <summary>
        /// Normalizes the input, splits it into words, records the 1-based
        /// line number of each occurrence, and enforces the supplied limits.
        /// </summary>
        internal static List<(string Word, int LineNumber)> NormalizeAndExtractWordsWithLines(
            string inputText,
            FuzzyScorerOptions options,
            CancellationToken cancellationToken)
        {
            return EnumerateWords(inputText, options, cancellationToken).ToList();
        }

        /// <summary>
        /// Estimates the number of distinct deletion variants needed for the index.
        /// The estimate is an upper bound; it only decides whether the index is affordable.
        /// </summary>
        private static long EstimateVariantCount(List<string> words, int maxDistance)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;

            foreach (var word in words)
            {
                if (word is null)
                    continue;

                var lowerWord = word.ToLowerInvariant();
                if (!seen.Add(lowerWord))
                    continue;

                total += VariantUpperBound(lowerWord.Length, maxDistance);
                if (total > MaxIndexedVariants)
                    return total;
            }

            return total;
        }

        private static long VariantUpperBound(int length, int maxDistance)
        {
            long total = 1;
            long combination = 1;

            for (int i = 1; i <= maxDistance && i <= length; i++)
            {
                combination = combination * (length - i + 1) / i;
                total += combination;
            }

            return total;
        }

        private sealed class ComparisonBudget
        {
            private readonly long _limit;
            private long _used;

            public ComparisonBudget(long limit)
            {
                _limit = limit;
            }

            public void Count()
            {
                if (++_used > _limit)
                {
                    throw new ArgumentException(
                        $"Similarity grouping exceeded the maximum of {_limit} " +
                        "edit-distance comparisons. Reduce the number of distinct words or lower maxEditDistance.",
                        "words");
                }
            }
        }

        /// <summary>Index over group leaders used to narrow candidate comparisons.</summary>
        private interface IWordIndex
        {
            void Add(string word, int groupIndex);

            int FindFirstMatch(string word, int maxDistance);
        }

        /// <summary>
        /// Deletion-variant index. Two strings within edit distance k share a common
        /// variant obtained by deleting at most k characters from each, so looking up the
        /// query's variants yields a superset of the matches. Variants are keyed by a
        /// 64-bit hash to avoid allocating intermediate strings; hash collisions only add
        /// candidates, which are verified with the exact bounded distance.
        /// </summary>
        private sealed class DeletionIndex : IWordIndex
        {
            private const ulong FnvOffsetBasis = 14695981039346656037UL;
            private const ulong FnvPrime = 1099511628211UL;

            private readonly Dictionary<ulong, List<int>> _variants = new();
            private readonly Dictionary<int, string> _leaders = new();
            private readonly HashSet<ulong> _generated = new();
            private readonly HashSet<int> _candidates = new();
            private readonly List<int> _orderedCandidates = new();
            private readonly ComparisonBudget _budget;
            private readonly int _maxDistance;

            public DeletionIndex(int maxDistance, ComparisonBudget budget)
            {
                _maxDistance = maxDistance;
                _budget = budget;
            }

            public void Add(string word, int groupIndex)
            {
                _leaders[groupIndex] = word;
                _generated.Clear();
                GenerateVariantHashes(word, _maxDistance);

                foreach (ulong hash in _generated)
                {
                    if (!_variants.TryGetValue(hash, out var indices))
                    {
                        indices = new List<int>();
                        _variants[hash] = indices;
                    }

                    indices.Add(groupIndex);
                }
            }

            public int FindFirstMatch(string word, int maxDistance)
            {
                _generated.Clear();
                GenerateVariantHashes(word, maxDistance);

                _candidates.Clear();
                foreach (ulong hash in _generated)
                {
                    if (!_variants.TryGetValue(hash, out var indices))
                        continue;

                    foreach (int index in indices)
                        _candidates.Add(index);
                }

                if (_candidates.Count == 0)
                    return -1;

                _orderedCandidates.Clear();
                _orderedCandidates.AddRange(_candidates);
                _orderedCandidates.Sort();

                foreach (int index in _orderedCandidates)
                {
                    if (!_leaders.TryGetValue(index, out var leader))
                        continue;

                    if (leader.Length > word.Length + maxDistance || leader.Length < word.Length - maxDistance)
                        continue;

                    _budget.Count();
                    if (ComputeEditDistance(word, leader, maxDistance) >= 0)
                        return index;
                }

                return -1;
            }

            private void GenerateVariantHashes(string word, int maxDistance)
            {
                _generated.Add(HashWithDeletions(word, -1, -1));

                if (maxDistance == 0)
                    return;

                for (int i = 0; i < word.Length; i++)
                    _generated.Add(HashWithDeletions(word, i, -1));

                if (maxDistance == 1)
                    return;

                for (int i = 0; i < word.Length; i++)
                {
                    for (int j = i + 1; j < word.Length; j++)
                        _generated.Add(HashWithDeletions(word, i, j));
                }
            }

            private static ulong HashWithDeletions(string word, int skipFirst, int skipSecond)
            {
                ulong hash = FnvOffsetBasis;

                for (int i = 0; i < word.Length; i++)
                {
                    if (i == skipFirst || i == skipSecond)
                        continue;

                    hash ^= word[i];
                    hash *= FnvPrime;
                }

                return hash;
            }
        }

        /// <summary>
        /// BK-tree over group leaders. Exact Levenshtein distance is a metric, so the
        /// triangle inequality lets the search skip subtrees. Each node also tracks the
        /// length range of its subtree, which prunes branches that cannot contain a
        /// word within the edit-distance window. Group indices increase along every
        /// root-to-leaf path, so the earliest match also bounds the search.
        /// </summary>
        private sealed class SimilarityIndex : IWordIndex
        {
            private readonly ComparisonBudget _budget;
            private readonly Stack<Node> _searchStack = new();

            public SimilarityIndex(ComparisonBudget budget)
            {
                _budget = budget;
            }

            private Node? Root { get; set; }

            public void Add(string word, int groupIndex)
            {
                if (Root is null)
                {
                    Root = new Node(word, groupIndex);
                    return;
                }

                var node = Root;
                while (true)
                {
                    node.Include(word.Length);

                    _budget.Count();
                    int distance = ComputeExactDistance(word, node.Word);

                    if (distance == 0)
                        return;

                    Node? child = node.FindChild(distance);
                    if (child is not null)
                    {
                        node = child;
                        continue;
                    }

                    node.Children.Add(new Child(distance, new Node(word, groupIndex)));
                    return;
                }
            }

            public int FindFirstMatch(string word, int maxDistance)
            {
                if (Root is null)
                    return -1;

                int best = int.MaxValue;
                int minimumLength = Math.Max(0, word.Length - maxDistance);
                int maximumLength = word.Length + maxDistance;

                _searchStack.Clear();
                _searchStack.Push(Root);

                while (_searchStack.Count > 0)
                {
                    var node = _searchStack.Pop();

                    // Descendants are always inserted later, so they carry larger
                    // group indices and cannot improve the current best match.
                    if (node.GroupIndex >= best)
                        continue;

                    // No word in this subtree can fall inside the length window.
                    if (node.MaxLength < minimumLength || node.MinLength > maximumLength)
                        continue;

                    _budget.Count();
                    int distance = ComputeExactDistance(word, node.Word);

                    if (distance <= maxDistance && node.GroupIndex < best)
                        best = node.GroupIndex;

                    int minimumEdge = distance - maxDistance;
                    int maximumEdge = distance + maxDistance;

                    // Children are stored in insertion (group index) order. Pushing them
                    // in reverse makes the lowest index pop first, which finds the earliest
                    // match quickly and lets the index prune the rest of the tree.
                    for (int i = node.Children.Count - 1; i >= 0; i--)
                    {
                        var child = node.Children[i];
                        if (child.Edge >= minimumEdge && child.Edge <= maximumEdge)
                            _searchStack.Push(child.Node);
                    }
                }

                return best == int.MaxValue ? -1 : best;
            }

            private sealed class Node
            {
                public Node(string word, int groupIndex)
                {
                    Word = word;
                    GroupIndex = groupIndex;
                    MinLength = word.Length;
                    MaxLength = word.Length;
                }

                public string Word { get; }

                public int GroupIndex { get; }

                public int MinLength { get; private set; }

                public int MaxLength { get; private set; }

                public List<Child> Children { get; } = new List<Child>();

                public void Include(int length)
                {
                    if (length < MinLength)
                        MinLength = length;
                    if (length > MaxLength)
                        MaxLength = length;
                }

                public Node? FindChild(int edge)
                {
                    for (int i = 0; i < Children.Count; i++)
                    {
                        if (Children[i].Edge == edge)
                            return Children[i].Node;
                    }

                    return null;
                }
            }

            private sealed class Child
            {
                public Child(int edge, Node node)
                {
                    Edge = edge;
                    Node = node;
                }

                public int Edge { get; }

                public Node Node { get; }
            }
        }
    }
}
