using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace FuzzyScorer
{
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

        internal static readonly Regex WordNormalizationRegex = new Regex(@"[^\p{L}\p{N}\s-]", RegexOptions.Compiled);

        /// <summary>
        /// Returns word frequency counts from the input text.
        /// Comparisons are case-insensitive (e.g., "Apple" and "apple" are treated as the same word).
        /// </summary>
        /// <param name="inputText">The raw text to analyze for word frequency.</param>
        /// <returns>A list of WordScore objects, each containing a unique word and its frequency count.</returns>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        public static List<WordScore> GetWordFrequencies(string? inputText)
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
        public static List<WordScore> GetWordFrequencies(string? inputText, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputText))
                return new List<WordScore>();

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
        public static List<WordScore> GroupSimilarWords(string? inputText, int maxEditDistance)
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
        public static List<WordScore> GroupSimilarWords(string? inputText, int maxEditDistance, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputText))
                return new List<WordScore>();

            if (maxEditDistance < 0 || maxEditDistance > MaxEditDistanceLimit)
                throw new ArgumentException($"maxEditDistance must be between 0 and {MaxEditDistanceLimit}", nameof(maxEditDistance));

            var allWords = NormalizeAndExtractWords(inputText, cancellationToken);
            return GroupBySimilarity(allWords, maxEditDistance, cancellationToken);
        }

        /// <summary>
        /// Groups a list of words by similarity. Each inner list contains all words
        /// assigned to one similarity group. The first occurrence becomes the group's leader.
        /// Results are order-dependent.
        /// </summary>
        /// <param name="words">The list of words to group.</param>
        /// <param name="maxEditDistance">Maximum edit distance for similarity.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of similarity groups.</returns>
        public static List<List<string>> GroupWordsBySimilarity(List<string> words, int maxEditDistance, CancellationToken cancellationToken)
        {
            return BuildSimilarityGroups(words, maxEditDistance, cancellationToken);
        }

        /// <summary>
        /// Returns true if the two words are similar within the given edit distance threshold.
        /// </summary>
        /// <param name="word1">First word.</param>
        /// <param name="word2">Second word.</param>
        /// <param name="maxEditDistance">Maximum allowed edit distance.</param>
        public static bool AreWordsSimilar(string word1, string word2, int maxEditDistance)
        {
            return ComputeEditDistance(
                word1.ToLowerInvariant(),
                word2.ToLowerInvariant()) <= maxEditDistance;
        }

        /// <summary>
        /// Groups words by similarity and returns the original word groups.
        /// Each inner list contains all words assigned to one similarity group.
        /// The first occurrence of a word becomes the group's leader.
        /// Results are order-dependent.
        /// </summary>
        internal static List<List<string>> GetWordGroups(string? inputText, int maxEditDistance, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputText))
                return new List<List<string>>();

            if (maxEditDistance < 0 || maxEditDistance > MaxEditDistanceLimit)
                throw new ArgumentException($"maxEditDistance must be between 0 and {MaxEditDistanceLimit}", nameof(maxEditDistance));

            var allWords = NormalizeAndExtractWords(inputText, cancellationToken);
            return BuildSimilarityGroups(allWords, maxEditDistance, cancellationToken);
        }

        /// <summary>
        /// Groups words by similarity. The first occurrence of a word becomes
        /// the group's representative. Results are order-dependent.
        /// </summary>
        private static List<WordScore> GroupBySimilarity(List<string> words, int maxEditDistance, CancellationToken cancellationToken)
        {
            var groups = BuildSimilarityGroups(words, maxEditDistance, cancellationToken);

            return groups
                .Select(group => new WordScore(group[0], group.Count))
                .ToList();
        }

        /// <summary>
        /// Builds similarity groups for a list of words. Each inner list contains
        /// all words assigned to one group. The first occurrence becomes the group leader.
        /// </summary>
        internal static List<List<string>> BuildSimilarityGroups(List<string> words, int maxEditDistance, CancellationToken cancellationToken)
        {
            var groups = new List<List<string>>();
            var lowerGroupLeaders = new List<string>();

            foreach (var word in words)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var lowerWord = word.ToLowerInvariant();

                bool addedToGroup = false;
                for (int i = 0; i < groups.Count; i++)
                {
                    if (ComputeEditDistance(lowerWord, lowerGroupLeaders[i]) <= maxEditDistance)
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

            return groups;
        }

        /// <summary>
        /// Computes the edit distance between two strings.
        /// </summary>
        private static int ComputeEditDistance(string s, string t)
        {
            if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
            if (string.IsNullOrEmpty(t)) return s.Length;

            int n = s.Length;
            int m = t.Length;
            int[,] d = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++) d[i, 0] = i;
            for (int j = 0; j <= m; j++) d[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                    d[i, j] = Math.Min(
                        Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                        d[i - 1, j - 1] + cost);
                }
            }
            return d[n, m];
        }

        /// <summary>
        /// Strips non-alphanumeric characters (except hyphens and whitespace),
        /// splits into words, and enforces security limits.
        /// </summary>
        internal static List<string> NormalizeAndExtractWords(string inputText, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (inputText.Length > MaxInputLength)
                throw new ArgumentException($"Input exceeds maximum length of {MaxInputLength} characters", nameof(inputText));

            var normalized = WordNormalizationRegex.Replace(inputText, "");

            var words = normalized
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length <= MaxWordLength)
                .ToList();

            if (words.Count > MaxWordsPerText)
                throw new ArgumentException($"Input contains {words.Count} words, exceeding limit of {MaxWordsPerText}", nameof(inputText));

            return words;
        }
    }
}
