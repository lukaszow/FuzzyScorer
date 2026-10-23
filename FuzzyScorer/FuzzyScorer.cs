using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        public FuzzyScorerResult Score(string inputText, double sensitivity, CancellationToken cancellationToken = default)
        {
            ValidateSensitivity(sensitivity);
            return Score(inputText, CreateOptions(sensitivity), cancellationToken);
        }

        /// <summary>
        /// Analyzes the input text with explicit options (edit distance, limits,
        /// minimum fuzzy length, normalization, grouping, and tie-breaking).
        /// </summary>
        /// <param name="inputText">The raw text to analyze.</param>
        /// <param name="options">Analysis options.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>A <see cref="FuzzyScorerResult"/> containing the analysis results.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if an option is out of range.</exception>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds size limits.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
        [SuppressMessage(
            "Performance",
            "CA1822:Mark members as static",
            Justification = "Instance API mirroring IFuzzyScorer; callers use it through the class.")]
        public FuzzyScorerResult Score(string inputText, FuzzyScorerOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            return Analyze(inputText, options, cancellationToken);
        }

        /// <summary>
        /// Asynchronously analyzes the input text and returns a result containing
        /// original and compressed word counts, along with potential errors (typos).
        /// </summary>
        /// <param name="inputText">The raw text to analyze.</param>
        /// <param name="sensitivity">
        /// A value between 0.0 and 1.0 that controls how aggressively similar words
        /// are grouped. 0.0 means exact match only; 1.0 means maximum fuzziness.
        /// </param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>A task producing a <see cref="FuzzyScorerResult"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if inputText exceeds limits or sensitivity is out of range.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
        public Task<FuzzyScorerResult> ScoreAsync(string inputText, double sensitivity, CancellationToken cancellationToken = default)
        {
            ValidateSensitivity(sensitivity);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() => Score(inputText, sensitivity, cancellationToken), cancellationToken);
        }

        /// <summary>
        /// Asynchronously analyzes the input text with explicit options.
        /// </summary>
        /// <param name="inputText">The raw text to analyze.</param>
        /// <param name="options">Analysis options.</param>
        /// <param name="cancellationToken">Cancellation token for aborting the operation.</param>
        /// <returns>A task producing a <see cref="FuzzyScorerResult"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="options"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if an option is out of range.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation is cancelled.</exception>
        public Task<FuzzyScorerResult> ScoreAsync(string inputText, FuzzyScorerOptions options, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(options);
            options.Validate();

            cancellationToken.ThrowIfCancellationRequested();
            return Task.Run(() => Score(inputText, options, cancellationToken), cancellationToken);
        }

        private static void ValidateSensitivity(double sensitivity)
        {
            if (sensitivity < 0 || sensitivity > 1)
                throw new ArgumentException("Sensitivity must be between 0.0 and 1.0.", nameof(sensitivity));
        }

        private static FuzzyScorerOptions CreateOptions(double sensitivity)
        {
            return new FuzzyScorerOptions
            {
                MaxEditDistance = (int)Math.Round(
                    sensitivity * WordScorer.MaxEditDistanceLimit,
                    MidpointRounding.AwayFromZero),
            };
        }

        private static FuzzyScorerResult Analyze(
            string inputText,
            FuzzyScorerOptions options,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(inputText))
            {
                return new FuzzyScorerResult(
                    totalWords: 0,
                    uniqueGroups: 0,
                    errors: Array.Empty<ErrorEntry>(),
                    maxEditDistance: options.MaxEditDistance,
                    wordGroups: Array.Empty<WordGroup>());
            }

            var wordsWithLines = WordScorer.NormalizeAndExtractWordsWithLines(
                inputText, options, cancellationToken);
            int totalWords = wordsWithLines.Count;

            var freqDict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var wordLineMap = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            foreach (var (word, lineNumber) in wordsWithLines)
            {
                freqDict[word] = freqDict.TryGetValue(word, out int count) ? count + 1 : 1;

                if (!wordLineMap.TryGetValue(word, out var lines))
                {
                    lines = new List<int>();
                    wordLineMap[word] = lines;
                }

                lines.Add(lineNumber);
            }

            var groups = WordScorer.BuildSimilarityGroups(
                wordsWithLines.Select(entry => entry.Word), options, cancellationToken);

            var errors = new List<ErrorEntry>();
            var wordGroups = new List<WordGroup>(groups.Count);

            foreach (var group in groups)
            {
                var distinctWords = group
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (distinctWords.Count == 0)
                    continue;

                var selection = SelectRepresentative(distinctWords, freqDict, options.TieBreak);
                wordGroups.Add(new WordGroup(selection.Text, distinctWords, group.Count));

                if (distinctWords.Count <= 1)
                    continue;

                CollectErrors(
                    distinctWords, selection, options.TieBreak, freqDict, wordLineMap, errors);
            }

            errors.Sort(CompareErrors);

            return new FuzzyScorerResult(
                totalWords,
                groups.Count,
                errors,
                options.MaxEditDistance,
                wordGroups);
        }

        private static void CollectErrors(
            List<string> distinctWords,
            RepresentativeSelection selection,
            TieBreakMode tieBreak,
            Dictionary<string, int> freqDict,
            Dictionary<string, List<int>> wordLineMap,
            List<ErrorEntry> errors)
        {
            bool reportAllTies = tieBreak == TieBreakMode.ReportAllTies && !selection.IsCertain;

            foreach (var word in distinctWords)
            {
                if (!reportAllTies
                    && string.Equals(word, selection.Text, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var lineNumbers = wordLineMap.TryGetValue(word, out var lines)
                    ? lines.Distinct().OrderBy(x => x).ToList()
                    : new List<int>();

                errors.Add(new ErrorEntry(
                    word,
                    GetFrequency(word, freqDict),
                    lineNumbers,
                    suggestedText: reportAllTies ? null : selection.Text,
                    isCertain: !reportAllTies && selection.IsCertain));
            }
        }

        private static int CompareErrors(ErrorEntry first, ErrorEntry second)
        {
            int firstLine = first.LineNumbers.Count > 0 ? first.LineNumbers[0] : int.MaxValue;
            int secondLine = second.LineNumbers.Count > 0 ? second.LineNumbers[0] : int.MaxValue;

            int lineComparison = firstLine.CompareTo(secondLine);
            if (lineComparison != 0)
                return lineComparison;

            return string.Compare(first.Text, second.Text, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Selects the group representative according to the configured tie-break rule.
        /// </summary>
        private static RepresentativeSelection SelectRepresentative(
            List<string> distinctWords,
            Dictionary<string, int> freqDict,
            TieBreakMode tieBreak)
        {
            int maxFrequency = -1;
            int tiedCount = 0;

            foreach (var word in distinctWords)
            {
                int frequency = GetFrequency(word, freqDict);

                if (frequency > maxFrequency)
                {
                    maxFrequency = frequency;
                    tiedCount = 1;
                }
                else if (frequency == maxFrequency)
                {
                    tiedCount++;
                }
            }

            string representative = distinctWords[0];
            if (tieBreak == TieBreakMode.FirstOccurrence)
            {
                foreach (var word in distinctWords)
                {
                    if (GetFrequency(word, freqDict) == maxFrequency)
                    {
                        representative = word;
                        break;
                    }
                }
            }
            else
            {
                bool found = false;

                foreach (var word in distinctWords)
                {
                    if (GetFrequency(word, freqDict) != maxFrequency)
                        continue;

                    if (!found || IsBetterRepresentative(word, representative))
                    {
                        representative = word;
                        found = true;
                    }
                }
            }

            return new RepresentativeSelection(representative, isCertain: tiedCount == 1);
        }

        private static bool IsBetterRepresentative(string candidate, string current)
        {
            if (candidate.Length != current.Length)
                return candidate.Length > current.Length;

            return string.Compare(candidate, current, StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static int GetFrequency(string word, Dictionary<string, int> freqDict)
        {
            return freqDict.TryGetValue(word, out var frequency) ? frequency : 1;
        }

        private readonly struct RepresentativeSelection
        {
            public RepresentativeSelection(string text, bool isCertain)
            {
                Text = text;
                IsCertain = isCertain;
            }

            public string Text { get; }

            public bool IsCertain { get; }
        }
    }
}
