using System;
using System.Collections.Generic;
using System.Text;

namespace FuzzyScorer.Benchmarks
{
    /// <summary>
    /// Deterministic corpus generators for the benchmarks.
    /// </summary>
    internal static class Corpus
    {
        private static readonly string[] Syllables =
        {
            "ka", "lo", "mi", "ra", "te", "su", "no", "vi", "pe", "da",
            "zu", "ri", "mo", "la", "ne", "po", "ta", "si", "gu", "be",
        };

        /// <summary>Unique words with varied lengths (4–10 characters).</summary>
        public static string[] CreateUniqueWords(int wordCount, int seed)
        {
            var random = new Random(seed);
            var words = new HashSet<string>(StringComparer.Ordinal);
            var builder = new StringBuilder();

            while (words.Count < wordCount)
            {
                builder.Clear();
                int syllableCount = 2 + random.Next(4);
                for (int i = 0; i < syllableCount; i++)
                    builder.Append(Syllables[random.Next(Syllables.Length)]);

                words.Add(builder.ToString());
            }

            var result = new string[words.Count];
            words.CopyTo(result);
            return result;
        }

        /// <summary>Few unique words repeated many times.</summary>
        public static string[] CreateRepeatedWords(int uniqueCount, int totalCount, int seed)
        {
            var random = new Random(seed);
            var unique = CreateUniqueWords(uniqueCount, seed);
            var result = new string[totalCount];

            for (int i = 0; i < totalCount; i++)
                result[i] = unique[random.Next(unique.Length)];

            return result;
        }

        /// <summary>
        /// Unique six-letter words with pairwise Levenshtein distance of at least 2.
        /// Every word differs from every other in at least two positions, so a
        /// threshold of 1 never matches — the worst case for the grouping loop.
        /// </summary>
        public static string[] CreateMinDistanceTwoWords(int wordCount)
        {
            var result = new string[wordCount];
            Span<char> characters = stackalloc char[6];

            for (int index = 0; index < wordCount; index++)
            {
                int value = index;
                int sum = 0;

                for (int position = 5; position >= 1; position--)
                {
                    int digit = value % 26;
                    value /= 26;
                    characters[position] = (char)('a' + digit);
                    sum += digit;
                }

                characters[0] = (char)('a' + ((26 - (sum % 26)) % 26));
                result[index] = new string(characters);
            }

            return result;
        }
    }
}
