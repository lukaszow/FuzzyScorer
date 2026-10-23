using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FuzzyScorer;
using Xunit;

namespace FuzzyScorer.Tests
{
    /// <summary>
    /// Differential tests: the optimized grouping (per-word deduplication and metric
    /// index) must produce exactly the same groups as a straightforward reference
    /// implementation that scans every occurrence.
    /// </summary>
    public class GroupingEquivalenceTests
    {
        private const string Alphabet = "abcde";

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(10)]
        public void BuildSimilarityGroups_MatchesReferenceImplementation(int maxEditDistance)
        {
            for (int seed = 0; seed < 25; seed++)
            {
                var random = new Random(seed);
                string[] words = CreateCorpus(random);

                List<List<string>> expected = ReferenceGroups(words, maxEditDistance);
                var actual = WordScorer.BuildSimilarityGroups(words, maxEditDistance, CancellationToken.None);

                AssertSameGroups(expected, actual);
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void GroupSimilarWords_MatchesReferenceScores(int maxEditDistance)
        {
            for (int seed = 100; seed < 120; seed++)
            {
                var random = new Random(seed);
                string[] words = CreateCorpus(random);

                List<List<string>> expected = ReferenceGroups(words, maxEditDistance);
                var actual = WordScorer.GroupSimilarWords(string.Join(' ', words), maxEditDistance);

                Assert.Equal(expected.Count, actual.Count);
                for (int i = 0; i < expected.Count; i++)
                {
                    Assert.Equal(expected[i][0], actual[i].Text);
                    Assert.Equal(expected[i].Count, actual[i].Score);
                }
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void BuildSimilarityGroups_ForcedIndex_MatchesReferenceImplementation(int maxEditDistance)
        {
            for (int seed = 200; seed < 225; seed++)
            {
                var random = new Random(seed);
                string[] words = CreateCorpus(random);

                List<List<string>> expected = ReferenceGroups(words, maxEditDistance);
                var actual = WordScorer.BuildSimilarityGroups(
                    words,
                    maxEditDistance,
                    WordScorer.MaxSimilarityComparisons,
                    forceIndex: true,
                    CancellationToken.None);

                AssertSameGroups(expected, actual);
            }
        }

        [Fact]
        public void BuildSimilarityGroups_RepeatedOccurrences_ArePreserved()
        {
            var words = new[] { "apple", "Apple", "aple", "apple" };

            var groups = WordScorer.BuildSimilarityGroups(words, 1, CancellationToken.None);

            var group = Assert.Single(groups);
            Assert.Equal(words, group.ToList());
        }

        [Fact]
        public void BuildSimilarityGroups_TenThousandUniqueWords_Completes()
        {
            string[] words = CreateUniqueWords(10_000);

            var groups = WordScorer.BuildSimilarityGroups(words, 1, CancellationToken.None);

            Assert.Equal(words.Length, groups.Sum(group => group.Count));
        }

        private static string[] CreateCorpus(Random random)
        {
            int uniqueCount = random.Next(5, 40);
            int totalCount = random.Next(10, 200);
            string[] pool = CreateUniqueWords(uniqueCount, random);

            var words = new string[totalCount];
            for (int i = 0; i < totalCount; i++)
            {
                string word = pool[random.Next(pool.Length)];
                words[i] = VaryCase(random, word);
            }

            return words;
        }

        private static string[] CreateUniqueWords(int count)
        {
            return CreateUniqueWords(count, new Random(42));
        }

        private static string[] CreateUniqueWords(int count, Random random)
        {
            var unique = new HashSet<string>(StringComparer.Ordinal);

            while (unique.Count < count)
            {
                int length = random.Next(1, 8);
                var characters = new char[length];
                for (int i = 0; i < length; i++)
                    characters[i] = Alphabet[random.Next(Alphabet.Length)];

                unique.Add(new string(characters));
            }

            return unique.ToArray();
        }

        private static string VaryCase(Random random, string word)
        {
            if (word.Length == 0)
                return word;

            int index = random.Next(word.Length);
            var characters = word.ToCharArray();
            characters[index] = char.ToUpperInvariant(characters[index]);
            return new string(characters);
        }

        private static List<List<string>> ReferenceGroups(IReadOnlyList<string> words, int maxEditDistance)
        {
            var groups = new List<List<string>>();
            var leaders = new List<string>();

            foreach (string word in words)
            {
                string lowerWord = word.ToLowerInvariant();
                bool added = false;

                for (int i = 0; i < groups.Count; i++)
                {
                    if (ReferenceDistance(lowerWord, leaders[i]) <= maxEditDistance)
                    {
                        groups[i].Add(word);
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    groups.Add(new List<string> { word });
                    leaders.Add(lowerWord);
                }
            }

            return groups;
        }

        /// <summary>Full-matrix Levenshtein distance used as the reference.</summary>
        private static int ReferenceDistance(string a, string b)
        {
            int n = a.Length;
            int m = b.Length;
            var matrix = new int[n + 1, m + 1];

            for (int i = 0; i <= n; i++)
                matrix[i, 0] = i;
            for (int j = 0; j <= m; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= m; j++)
                {
                    int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[n, m];
        }

        private static void AssertSameGroups(
            IReadOnlyList<List<string>> expected,
            IReadOnlyList<IReadOnlyList<string>> actual)
        {
            Assert.Equal(expected.Count, actual.Count);

            for (int i = 0; i < expected.Count; i++)
                Assert.Equal(expected[i], actual[i].ToList());
        }
    }
}
