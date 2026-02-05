using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WordsCloud
{
    /// <summary>
    /// Entry point for the WordsCloud application.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Entry point for the WordsCloud application.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                Console.WriteLine("--- WordsCloud Analysis ---");

                string combinedText;
                if (args.Length > 0)
                {
                    combinedText = string.Join(" ", args);
                }
                else
                {
                    // Default values if no arguments provided
                    var defaultTexts = new[] { "pierwszy tekst", "drugi tekst", "pierwszy tekst" };
                    combinedText = string.Join(" ", defaultTexts);
                    Console.WriteLine("No arguments provided. Using default sample text.");
                }

                var results = GetScoringWords(combinedText);

                Console.WriteLine("\nAnalysis Results:");
                foreach (var result in results)
                {
                    Console.WriteLine($"- {result.Text}: {result.Score}");
                }

                Console.WriteLine("\nTask completed successfully.");
                return 0; // Success
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"\nAn error occurred: {ex.Message}");
                return 1; // Failure
            }
        }

        /// <summary>
        /// Returns a list of words with their scores based on the number of occurrences in the provided text.
        /// </summary>
        /// <param name="inputText">The string text to analyze.</param>
        /// <returns>A list of WordScore objects.</returns>
        public static List<WordScore> GetScoringWords(string? inputText)
        {
            if (string.IsNullOrWhiteSpace(inputText))
                return new List<WordScore>();

            // Group by word and count occurrences
            return inputText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
                .Select(group => new WordScore(group.Key, group.Count()))
                .ToList();
        }

        /// <summary>
        /// Returns a list of words with their scores based on the number of occurrences in the provided text.
        /// Occurence is calculated using Levenshtein distance where words are grouped if their distance is within the targetSimilarity.
        /// </summary>
        /// <param name="inputText">The string text to analyze.</param>
        /// <param name="targetSimilarity">Maximum Levenshtein distance allowed for words to be grouped together.</param>
        /// <returns>A list of WordScore objects.</returns>
        public static List<WordScore> GetScoringWords(string? inputText, int targetSimilarity)
        {
            if (string.IsNullOrWhiteSpace(inputText))
                return new List<WordScore>();

            var allWords = inputText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();

            var groups = new List<List<string>>();

            foreach (var word in allWords)
            {
                bool addedToGroup = false;
                foreach (var group in groups)
                {
                    // If word is similar to the first word in the group, add it
                    if (ComputeLevenshteinDistance(word.ToLower(), group[0].ToLower()) <= targetSimilarity)
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
    }
}
