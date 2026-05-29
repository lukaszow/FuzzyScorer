using System;
using System.Collections.Generic;

namespace FuzzyScorer
{
    /// <summary>
    /// Contains the result of a fuzzy scoring operation,
    /// including original/compressed word counts and a list of detected errors (typos).
    /// </summary>
    public class FuzzyScorerResult
    {
        /// <summary>
        /// Gets the total number of words in the original input.
        /// </summary>
        public int OriginalSize { get; }

        /// <summary>
        /// Gets the number of unique word groups after fuzzy grouping.
        /// </summary>
        public int CompressedSize { get; }

        /// <summary>
        /// Gets the list of potential errors (typos) detected during scoring.
        /// </summary>
        public IReadOnlyList<ErrorEntry> Errors { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="FuzzyScorerResult"/> class.
        /// </summary>
        /// <param name="originalSize">Total word count in the original input.</param>
        /// <param name="compressedSize">Number of unique groups after fuzzy grouping.</param>
        /// <param name="errors">List of detected error entries.</param>
        /// <exception cref="ArgumentNullException">Thrown if errors is null.</exception>
        /// <exception cref="ArgumentException">Thrown if originalSize or compressedSize is negative.</exception>
        public FuzzyScorerResult(int originalSize, int compressedSize, List<ErrorEntry> errors)
        {
            if (errors == null)
                throw new ArgumentNullException(nameof(errors));
            if (originalSize < 0)
                throw new ArgumentException("OriginalSize cannot be negative.", nameof(originalSize));
            if (compressedSize < 0)
                throw new ArgumentException("CompressedSize cannot be negative.", nameof(compressedSize));

            OriginalSize = originalSize;
            CompressedSize = compressedSize;
            Errors = errors.AsReadOnly();
        }
    }
}
