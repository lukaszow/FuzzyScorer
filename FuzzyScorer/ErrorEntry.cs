using System;
using System.Collections.Generic;
using System.Linq;

namespace FuzzyScorer
{
    /// <summary>
    /// Represents a potential error (typo) detected during fuzzy scoring,
    /// including the variant text, its frequency, and the lines on which it appears.
    /// </summary>
    public class ErrorEntry
    {
        /// <summary>
        /// Gets the potentially misspelled word text.
        /// </summary>
        public string ErrorText { get; }

        /// <summary>
        /// Gets the number of times the error text appears in the input.
        /// </summary>
        public int RepetitionCount { get; }

        /// <summary>
        /// Gets the 1-based line numbers where the error text appears.
        /// </summary>
        public IReadOnlyList<int> LineNumbers { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorEntry"/> class.
        /// </summary>
        /// <param name="errorText">The potentially misspelled word text.</param>
        /// <param name="repetitionCount">The number of times the word appears.</param>
        /// <param name="lineNumbers">The 1-based line numbers where the word appears.</param>
        /// <exception cref="ArgumentNullException">Thrown if errorText or lineNumbers is null.</exception>
        /// <exception cref="ArgumentException">Thrown if repetitionCount is negative.</exception>
        public ErrorEntry(string errorText, int repetitionCount, List<int> lineNumbers)
        {
            if (errorText == null)
                throw new ArgumentNullException(nameof(errorText));
            if (lineNumbers == null)
                throw new ArgumentNullException(nameof(lineNumbers));
            if (repetitionCount < 0)
                throw new ArgumentException("RepetitionCount cannot be negative.", nameof(repetitionCount));

            ErrorText = errorText;
            RepetitionCount = repetitionCount;
            LineNumbers = lineNumbers.OrderBy(x => x).ToList().AsReadOnly();
        }
    }
}
