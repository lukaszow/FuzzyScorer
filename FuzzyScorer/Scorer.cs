using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FuzzyScorer
{
    /// <summary>
    /// Core scoring logic for the FuzzyScorer library.
    /// </summary>
    public class Scorer
    {
        // Security limits to prevent DoS attacks
        private const int MaxWordsPerText = 10_000;
        private const int MaxWordLength = 256;
        private const int MaxSimilarityThreshold = 50;
        /// <summary>
        /// Analyzes the provided text by breaking it down into individual words and counting how many times each word appears.
        /// It ignores whether a word is written in UPPERCASE or lowercase (e.g., "Apple" and "apple" are treated as the same word).
        /// </summary>
        /// <param name="inputText">The raw text you want to analyze for word frequency.</param>
        /// <returns>
        /// A list of WordScore objects, where each object contains a unique word from the text 
        /// and a number representing how many times that specific word was found.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        public static List<WordScore> GetScoringWords(string? inputText)
        {
            return GetScoringWords(inputText, CancellationToken.None);
        }

        /// <summary>
        /// Analyzes the provided text by breaking it down into individual words and counting how many times each word appears.
        /// Supports cancellation token for long-running operations.
        /// </summary>
        /// <param name="inputText">The raw text you want to analyze for word frequency.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>
        /// A list of WordScore objects, where each object contains a unique word from the text 
        /// and a number representing how many times that specific word was found.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        /// <exception cref="OperationCanceledException">Thrown if operation is cancelled.</exception>
        public static List<WordScore> GetScoringWords(string? inputText, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputText))
                return new List<WordScore>();

            // Normalize and extract words
            var normalizedWords = NormalizeAndExtractWords(inputText, cancellationToken);
            
            // Group by word and count occurrences
            return normalizedWords
                .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
                .Select(group => new WordScore(group.Key, group.Count()))
                .ToList();
        }

        /// <summary>
        /// Analyzes text and groups words together even if they aren't identical, as long as they are visually similar.
        /// It uses the Levenshtein distance to catch variations like typos or different word endings (e.g., "TIGER" and "TlGER").
        /// </summary>
        /// <param name="inputText">The raw text you want to analyze.</param>
        /// <param name="targetSimilarity">
        /// The maximum number of character edits (insertions, deletions, or substitutions) allowed to group words.
        /// A higher number means more aggressive grouping of less-similar words.
        /// </param>
        /// <returns>
        /// A list of WordScore objects where similar words are merged into a single entry, 
        /// using the first word encountered as the "representative" for the group.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if parameters exceed limits.</exception>
        public static List<WordScore> GetScoringWords(string? inputText, int targetSimilarity)
        {
            return GetScoringWords(inputText, targetSimilarity, CancellationToken.None);
        }

        /// <summary>
        /// Analyzes text and groups words together even if they aren't identical, as long as they are visually similar.
        /// Supports cancellation token for long-running operations.
        /// </summary>
        /// <param name="inputText">The raw text you want to analyze.</param>
        /// <param name="targetSimilarity">
        /// The maximum number of character edits (insertions, deletions, or substitutions) allowed to group words.
        /// A higher number means more aggressive grouping of less-similar words.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>
        /// A list of WordScore objects where similar words are merged into a single entry, 
        /// using the first word encountered as the "representative" for the group.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if parameters exceed limits.</exception>
        /// <exception cref="OperationCanceledException">Thrown if operation is cancelled.</exception>
        public static List<WordScore> GetScoringWords(string? inputText, int targetSimilarity, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(inputText))
                return new List<WordScore>();

            // Validate parameters
            if (targetSimilarity < 0 || targetSimilarity > MaxSimilarityThreshold)
                throw new ArgumentException($"targetSimilarity must be between 0 and {MaxSimilarityThreshold}", nameof(targetSimilarity));

            var allWords = NormalizeAndExtractWords(inputText, cancellationToken);
            var groups = new List<List<string>>();

            foreach (var word in allWords)
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool addedToGroup = false;
                foreach (var group in groups)
                {
                    // If word is similar to the first word in the group, add it
                    if (ComputeLevenshteinDistance(word.ToLowerInvariant(), group[0].ToLowerInvariant()) <= targetSimilarity)
                    {
                        group.Add(word);
                        addedToGroup = true;
                        break;
                    }
                }

                if (!addedToGroup)
                {
                    groups.Add(new List<string> { word });
                }
            }

            return groups
                .Select(group => new WordScore(group[0], group.Count))
                .ToList();
        }

        /// <summary>
        /// Computes the Levenshtein distance between two strings.
        /// </summary>
        private static int ComputeLevenshteinDistance(string s, string t)
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
        /// Normalizes input text and extracts words with security limits applied.
        /// Removes punctuation, trims whitespace, and enforces word/length limits.
        /// </summary>
        /// <param name="inputText">Raw input text.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>List of normalized words.</returns>
        /// <exception cref="ArgumentException">Thrown if input exceeds limits.</exception>
        private static List<string> NormalizeAndExtractWords(string inputText, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Remove non-alphanumeric characters except spaces (basic punctuation normalization)
            var normalized = Regex.Replace(inputText, @"[^\p{L}\p{N}\s-]", "", RegexOptions.Compiled);

            // Split and filter
            var words = normalized
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length > 0 && w.Length <= MaxWordLength)
                .ToList();

            // Check for DoS: too many words
            if (words.Count > MaxWordsPerText)
                throw new ArgumentException($"Input contains {words.Count} words, exceeding limit of {MaxWordsPerText}", nameof(inputText));

            return words;
        }
    }
}
