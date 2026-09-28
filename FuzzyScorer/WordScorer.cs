using System;
using System.Collections.Generic;
using System.Linq;
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
        /// Maximum number of pairwise edit-distance comparisons performed by
        /// <see cref="BuildSimilarityGroups"/> (10,000,000). Acts as a hard bound
        /// against quadratic-time inputs; exceeding it throws <see cref="ArgumentException"/>.
        /// </summary>
        public const int MaxSimilarityComparisons = 10_000_000;

        internal static readonly Regex WordNormalizationRegex = new Regex(@"[^\p{L}\p{N}\s-]", RegexOptions.Compiled);

        private static readonly char[] WordSeparators = { ' ', '\t', '\n', '\r' };

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
            if (string.IsNullOrWhiteSpace(inputText))
                return Array.Empty<WordScore>();

            var normalizedWords = NormalizeAndExtractWords(inputText, cancellationToken);

            return normalizedWords
                .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
                .Select(group => new WordScore(group.Key, group.Count()))
                .ToList();
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
        /// <exception cref="ArgumentException">Thrown if parameters exceed limits.</exception>
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
        /// <exception cref="ArgumentException">Thrown if parameters exceed limits.</exception>
        /// <exception cref="OperationCanceledException">Thrown if operation is cancelled.</exception>
        public static IReadOnlyList<WordScore> GroupSimilarWords(string? inputText, int maxEditDistance, CancellationToken cancellationToken)
        {
            ValidateEditDistance(maxEditDistance);

            if (string.IsNullOrWhiteSpace(inputText))
                return Array.Empty<WordScore>();

            var allWords = NormalizeAndExtractWords(inputText, cancellationToken);
            return GroupBySimilarity(allWords, maxEditDistance, cancellationToken);
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
        /// <exception cref="ArgumentNullException">Thrown if words is null.</exception>
        /// <exception cref="ArgumentException">Thrown if maxEditDistance or the comparison budget is exceeded.</exception>
        public static IReadOnlyList<IReadOnlyList<string>> GroupWordsBySimilarity(IEnumerable<string> words, int maxEditDistance, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(words);

            ValidateEditDistance(maxEditDistance);

            return BuildSimilarityGroups(words, maxEditDistance, cancellationToken);
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
        /// <exception cref="ArgumentException">Thrown if maxEditDistance is out of range.</exception>
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
        private static List<WordScore> GroupBySimilarity(IReadOnlyList<string> words, int maxEditDistance, CancellationToken cancellationToken)
        {
            var groups = BuildSimilarityGroups(words, maxEditDistance, cancellationToken);

            return groups
                .Select(group => new WordScore(group[0], group.Count))
                .ToList();
        }

        /// <summary>
        /// Builds similarity groups for a collection of words. Each inner list contains
        /// all words assigned to one group. The first occurrence becomes the group leader.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown if words is null.</exception>
        /// <exception cref="ArgumentException">Thrown if maxEditDistance is out of range or the comparison budget is exceeded.</exception>
        internal static IReadOnlyList<IReadOnlyList<string>> BuildSimilarityGroups(IEnumerable<string> words, int maxEditDistance, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(words);

            ValidateEditDistance(maxEditDistance);

            var groups = new List<List<string>>();
            var lowerGroupLeaders = new List<string>();
            long comparisons = 0;

            foreach (var word in words)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lowerWord = word.ToLowerInvariant();

                bool addedToGroup = false;
                for (int i = 0; i < groups.Count; i++)
                {
                    if (++comparisons > MaxSimilarityComparisons)
                        throw new ArgumentException(
                            $"Similarity grouping exceeded the maximum of {MaxSimilarityComparisons} " +
                            "edit-distance comparisons. Reduce the number of distinct words or lower maxEditDistance.",
                            nameof(words));

                    if (ComputeEditDistance(lowerWord, lowerGroupLeaders[i], maxEditDistance) >= 0)
                    {
                        groups[i].Add(word);
                        addedToGroup = true;
                        break;
                    }
                }

                if (!addedToGroup)
                {
                    groups.Add(new List<string> { word });
                    lowerGroupLeaders.Add(lowerWord);
                }
            }

            return (IReadOnlyList<IReadOnlyList<string>>)groups;
        }

        /// <summary>
        /// Computes the edit distance between two strings without allocating a full matrix.
        /// Returns the distance when it is at most <paramref name="maxDistance"/>, or -1 otherwise.
        /// </summary>
        private static int ComputeEditDistance(string s, string t, int maxDistance)
        {
            if (maxDistance < 0)
                return -1;

            int n = s.Length;
            int m = t.Length;

            if (Math.Abs(n - m) > maxDistance)
                return -1;

            if (maxDistance == 0)
                return string.Equals(s, t, StringComparison.Ordinal) ? 0 : -1;

            var previous = new int[m + 1];
            var current = new int[m + 1];

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

                var temp = previous;
                previous = current;
                current = temp;
            }

            return previous[m] <= maxDistance ? previous[m] : -1;
        }

        private static void ValidateEditDistance(int maxEditDistance)
        {
            if (maxEditDistance < 0 || maxEditDistance > MaxEditDistanceLimit)
                throw new ArgumentException($"maxEditDistance must be between 0 and {MaxEditDistanceLimit}", nameof(maxEditDistance));
        }

        /// <summary>
        /// Strips non-alphanumeric characters (except hyphens and whitespace),
        /// splits into words, and enforces security limits.
        /// </summary>
        internal static List<string> NormalizeAndExtractWords(string inputText, CancellationToken cancellationToken)
        {
            return NormalizeAndExtractWordsWithLines(inputText, cancellationToken)
                .Select(entry => entry.Word)
                .ToList();
        }

        /// <summary>
        /// Strips non-alphanumeric characters, splits into words, records the 1-based
        /// line number of each occurrence, and enforces security limits.
        /// </summary>
        internal static List<(string Word, int LineNumber)> NormalizeAndExtractWordsWithLines(string inputText, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (inputText.Length > MaxInputLength)
                throw new ArgumentException($"Input exceeds maximum length of {MaxInputLength} characters", nameof(inputText));

            var results = new List<(string Word, int LineNumber)>();
            var lines = inputText.Split('\n');

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var normalized = WordNormalizationRegex.Replace(lines[lineIndex], "");
                var words = normalized.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);

                int lineNumber = lineIndex + 1;
                foreach (var word in words)
                {
                    if (word.Length > MaxWordLength)
                        continue;

                    results.Add((word, lineNumber));
                }
            }

            if (results.Count > MaxWordsPerText)
                throw new ArgumentException($"Input contains {results.Count} words, exceeding limit of {MaxWordsPerText}", nameof(inputText));

            return results;
        }
    }
}
