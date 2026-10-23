using System;
using System.Collections.Generic;

namespace FuzzyScorer.Benchmarks
{
    /// <summary>
    /// Faithful copy of the pre-2.2.0 grouping algorithm (per-occurrence scanning,
    /// allocating banded distance) used as the benchmark baseline.
    /// </summary>
    internal static class LegacyGrouping
    {
        public static List<List<string>> Group(IReadOnlyList<string> words, int maxEditDistance)
        {
            var groups = new List<List<string>>();
            var leaders = new List<string>();
            long comparisons = 0;

            foreach (var word in words)
            {
                var lowerWord = word.ToLowerInvariant();
                bool added = false;

                for (int i = 0; i < groups.Count; i++)
                {
                    if (++comparisons > 10_000_000)
                        throw new InvalidOperationException("Comparison budget exceeded.");

                    if (Distance(lowerWord, leaders[i], maxEditDistance) >= 0)
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

        private static int Distance(string s, string t, int maxDistance)
        {
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
    }
}
