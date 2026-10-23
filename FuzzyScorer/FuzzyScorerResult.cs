using System;
using System.Collections.Generic;
using System.Linq;

namespace FuzzyScorer
{
    /// <summary>
    /// Contains the result of a fuzzy scoring operation: word counts, detected errors,
    /// the edit distance that was used, and the resulting groups.
    /// </summary>
    public class FuzzyScorerResult
    {
        /// <summary>
        /// Gets the total number of words in the original input.
        /// </summary>
        public int TotalWords { get; }

        /// <summary>
        /// Gets the number of word groups after fuzzy grouping.
        /// </summary>
        public int UniqueGroups { get; }

        /// <summary>
        /// Gets the list of potential errors (typos) detected during scoring.
        /// </summary>
        public IReadOnlyList<ErrorEntry> Errors { get; }

        /// <summary>
        /// Gets the edit distance used for this run.
        /// </summary>
        public int MaxEditDistance { get; }

        /// <summary>
        /// Gets the similarity groups (representative, distinct members, total count).
        /// </summary>
        public IReadOnlyList<WordGroup> WordGroups { get; }

        /// <summary>
        /// Gets the total number of words in the original input.
        /// </summary>
        [Obsolete("Use TotalWords instead.")]
        public int OriginalSize => TotalWords;

        /// <summary>
        /// Gets the number of word groups after fuzzy grouping.
        /// </summary>
        [Obsolete("Use UniqueGroups instead.")]
        public int CompressedSize => UniqueGroups;

        /// <summary>
        /// Initializes a new instance of the <see cref="FuzzyScorerResult"/> class.
        /// </summary>
        /// <param name="originalSize">Total word count in the original input.</param>
        /// <param name="compressedSize">Number of groups after fuzzy grouping.</param>
        /// <param name="errors">List of detected error entries.</param>
        /// <exception cref="ArgumentNullException">Thrown if errors is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if a size is negative.</exception>
        public FuzzyScorerResult(int originalSize, int compressedSize, IEnumerable<ErrorEntry> errors)
            : this(originalSize, compressedSize, errors, maxEditDistance: 0, Array.Empty<WordGroup>())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FuzzyScorerResult"/> class.
        /// </summary>
        /// <param name="totalWords">Total word count in the original input.</param>
        /// <param name="uniqueGroups">Number of groups after fuzzy grouping.</param>
        /// <param name="errors">List of detected error entries.</param>
        /// <param name="maxEditDistance">The edit distance used for this run.</param>
        /// <param name="wordGroups">The resulting groups.</param>
        /// <exception cref="ArgumentNullException">Thrown if a sequence is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if a size is negative or the distance is out of range.</exception>
        public FuzzyScorerResult(
            int totalWords,
            int uniqueGroups,
            IEnumerable<ErrorEntry> errors,
            int maxEditDistance,
            IEnumerable<WordGroup> wordGroups)
        {
            ArgumentNullException.ThrowIfNull(errors);
            ArgumentNullException.ThrowIfNull(wordGroups);
            if (totalWords < 0)
                throw new ArgumentOutOfRangeException(nameof(totalWords), totalWords, "TotalWords cannot be negative.");
            if (uniqueGroups < 0)
                throw new ArgumentOutOfRangeException(nameof(uniqueGroups), uniqueGroups, "UniqueGroups cannot be negative.");
            if (maxEditDistance < 0 || maxEditDistance > WordScorer.MaxEditDistanceLimit)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxEditDistance),
                    maxEditDistance,
                    $"MaxEditDistance must be between 0 and {WordScorer.MaxEditDistanceLimit}.");
            }

            TotalWords = totalWords;
            UniqueGroups = uniqueGroups;
            Errors = errors.ToList().AsReadOnly();
            MaxEditDistance = maxEditDistance;
            WordGroups = wordGroups.ToList().AsReadOnly();
        }
    }
}
