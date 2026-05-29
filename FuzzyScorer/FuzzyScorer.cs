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

            var wordLineMap = BuildWordLineMap(inputText, cancellationToken);

            var frequencies = WordScorer.GetWordFrequencies(inputText, cancellationToken);
            int originalSize = frequencies.Sum(f => f.Score);

            var groups = WordScorer.GetWordGroups(inputText, maxEditDistance, cancellationToken);
            int compressedSize = groups.Count;

            var freqDict = frequencies.ToDictionary(f => f.Text, f => f.Score, StringComparer.OrdinalIgnoreCase);

            var errors = DetectErrors(groups, freqDict, wordLineMap);

            return new FuzzyScorerResult(originalSize, compressedSize, errors);
        }

        private static List<ErrorEntry> DetectErrors(
            List<List<string>> groups,
            Dictionary<string, int> freqDict,
            Dictionary<string, List<int>> wordLineMap)
        {
            var errors = new List<ErrorEntry>();

            foreach (var group in groups)
            {
                if (group.Count <= 1)
                    continue;

                string? mostFrequent = null;
                int maxFreq = -1;
                foreach (var word in group)
                {
                    int freq = freqDict.TryGetValue(word, out var f) ? f : 1;
                    if (freq > maxFreq)
                    {
                        maxFreq = freq;
                        mostFrequent = word;
                    }
                }

                foreach (var word in group)
                {
                    if (string.Equals(word, mostFrequent, StringComparison.OrdinalIgnoreCase))
                        continue;

                    int freq = freqDict.TryGetValue(word, out var f) ? f : 1;
                    var lineNumbers = wordLineMap.TryGetValue(word, out var lines)
                        ? lines.OrderBy(x => x).ToList()
                        : new List<int>();

                    errors.Add(new ErrorEntry(word, freq, lineNumbers));
                }
            }

            return errors;
        }

        private static Dictionary<string, List<int>> BuildWordLineMap(string inputText, CancellationToken cancellationToken)
        {
            var map = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
            var lines = inputText.Split('\n');

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalized = WordScorer.WordNormalizationRegex.Replace(lines[lineIndex], "");
                var words = normalized.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

                int lineNumber = lineIndex + 1;
                foreach (var word in words)
                {
                    if (!map.ContainsKey(word))
                        map[word] = new List<int>();
                    map[word].Add(lineNumber);
                }
            }

            return map;
        }
    }
}
