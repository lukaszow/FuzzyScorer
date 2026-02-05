using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WordsCloud
{
    class Program
    {
        /// <summary>
        /// Entry point for the WordsCloud application.
        /// </summary>
        /// <param name="args">Command-line arguments.</param>
        static void Main(string[] args)
        {
            string[] texts = { "pierwszy tekst", "drugi tekst", "pierwszy tekst" };

            var results = GetScoringWords(texts);

            foreach (var result in results)
            {
                Console.WriteLine($"Word = {result.Text}, Score = {result.Score}");
            }

            Console.ReadLine();
        }

        /// <summary>
        /// Returns a list of words with their scores based on the number of occurrences in the provided texts.
        /// </summary>
        /// <param name="listOfText">Array of strings to analyze.</param>
        /// <returns>A list of WordScore objects.</returns>
        public static List<WordScore> GetScoringWords(string[] listOfText)
        {
            if (listOfText == null || listOfText.Length == 0)
                return new List<WordScore>();

            // Group by word and count occurrences
            return listOfText
                .SelectMany(text => text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
                .Select(group => new WordScore(group.Key, group.Count()))
                .ToList();
        }

        /// <summary>
        /// Returns a list of words with their scores based on the number of occurrences in the provided texts. Occurence is calculated using Levenshtein distance (parameter target Similarity).
        /// </summary>
        /// <param name="listOfText">Array of strings to analyze.</param>
        /// <param name="targetSimilarity">Target similarity score.</param>
        /// <returns>A list of WordScore objects.</returns>
        public static List<WordScore> GetScoringWords(string[] listOfText, int targetSimilarity)
        {
            return new List<WordScore>();
        }
    }

    /// <summary>
    /// Represents a word and its associated score.
    /// </summary>
    public class WordScore
    {
        public string Text { get; set; }
        public int Score { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WordScore"/> class.
        /// </summary>
        /// <param name="text">The word text.</param>
        /// <param name="score">The word score (frequency or similarity score).</param>
        public WordScore(string text, int score)
        {
            Text = text;
            Score = score;
        }
    }
}
