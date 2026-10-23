using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FuzzyScorer
{
    /// <summary>
    /// Analyzes input text for potential typographical errors by grouping similar
    /// words via Levenshtein distance and reporting variant spellings as errors.
    /// </summary>
    public class FuzzyScorer : IFuzzyScorer
    {
        /// <summary>
        /// Analyzes the input text and returns a result containing original and
        /// compressed word counts, along with a list of potential errors (typos).
        /// </summary>
        /// <param name="inputText">The raw text to analyze.</param>
        /// <param name="sensitivity">
        /// A value between 0.0 and 1.0 that controls how aggressively similar words
        /// are grouped. 0.0 means exact match only; 1.0 means maximum fuzziness.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>A <see cref="FuzzyScorerResult"/> containing the analysis results.</returns>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds limits or sensitivity is out of range.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
        public async Task<FuzzyScorerResult> ScoreAsync(string inputText, double sensitivity, CancellationToken cancellationToken)
        {
            if (sensitivity < 0 || sensitivity > 1)
                throw new ArgumentException("Sensitivity must be between 0.0 and 1.0.", nameof(sensitivity));

            cancellationToken.ThrowIfCancellationRequested();

            return await Task.Run(() => Analyze(inputText, sensitivity, cancellationToken), cancellationToken);
        }

        private static FuzzyScorerResult Analyze(string inputText, double sensitivity, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(inputText))
                return new FuzzyScorerResult(0, 0, new List<ErrorEntry>());

            int maxEditDistance = (int)Math.Round(sensitivity * WordScorer.MaxEditDistanceLimit);

            var wordsWithLines = WordScorer.NormalizeAndExtractWordsWithLines(inputText, cancellationToken);
            var allWords = wordsWithLines.Select(entry => entry.Word).ToList();
            int originalSize = allWords.Count;

            var freqDict = allWords
                .GroupBy(w => w, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            var groups = WordScorer.BuildSimilarityGroups(allWords, maxEditDistance, cancellationToken);
            int compressedSize = groups.Count;

            var wordLineMap = BuildWordLineMap(wordsWithLines);
            var errors = DetectErrors(groups, freqDict, wordLineMap);

            return new FuzzyScorerResult(originalSize, compressedSize, errors);
        }

        private static List<ErrorEntry> DetectErrors(
            IReadOnlyList<IReadOnlyList<string>> groups,
            Dictionary<string, int> freqDict,
            Dictionary<string, List<int>> wordLineMap)
        {
            var errors = new List<ErrorEntry>();

            foreach (var group in groups)
            {
                // One entry per distinct spelling, regardless of how many times it occurs.
                var distinctWords = group
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (distinctWords.Count <= 1)
                    continue;

                string mostFrequent = distinctWords[0];
                int maxFreq = GetFrequency(mostFrequent, freqDict);

                foreach (var word in distinctWords)
                {
                    int freq = GetFrequency(word, freqDict);
                    if (IsBetterRepresentative(word, freq, mostFrequent, maxFreq))
                    {
                        mostFrequent = word;
                        maxFreq = freq;
                    }
                }

                foreach (var word in distinctWords)
                {
                    if (string.Equals(word, mostFrequent, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var lineNumbers = wordLineMap.TryGetValue(word, out var lines)
                        ? lines.Distinct().OrderBy(x => x).ToList()
                        : new List<int>();

                    errors.Add(new ErrorEntry(word, GetFrequency(word, freqDict), lineNumbers));
                }
            }

            return errors
                .OrderBy(e => e.LineNumbers.Count > 0 ? e.LineNumbers[0] : int.MaxValue)
                .ThenBy(e => e.ErrorText, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Selects the group representative: most frequent, then longest,
        /// then the first in ordinal order. The ranking is deterministic and
        /// treats deletions (shorter typos) as the most likely variants.
        /// </summary>
        private static bool IsBetterRepresentative(
            string candidate,
            int candidateFrequency,
            string current,
            int currentFrequency)
        {
            if (candidateFrequency != currentFrequency)
                return candidateFrequency > currentFrequency;

            if (candidate.Length != current.Length)
                return candidate.Length > current.Length;

            return string.Compare(candidate, current, StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static int GetFrequency(string word, Dictionary<string, int> freqDict)
        {
            return freqDict.TryGetValue(word, out var frequency) ? frequency : 1;
        }

        private static Dictionary<string, List<int>> BuildWordLineMap(List<(string Word, int LineNumber)> wordsWithLines)
        {
            var map = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (word, lineNumber) in wordsWithLines)
            {
                if (!map.TryGetValue(word, out var lines))
                {
                    lines = new List<int>();
                    map[word] = lines;
                }

                lines.Add(lineNumber);
            }

            return map;
        }
    }
}
