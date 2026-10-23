using System;
using System.Collections.Generic;
using System.Linq;

namespace FuzzyScorer
{
    /// <summary>
    /// Represents a potential error (typo) detected during fuzzy scoring,
    /// including the variant text, its frequency, the lines on which it appears,
    /// and the suggested correction.
    /// </summary>
    public class ErrorEntry
    {
        /// <summary>
        /// Gets the potentially misspelled word text.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the number of times this spelling appears in the input.
        /// </summary>
        public int Occurrences { get; }

        /// <summary>
        /// Gets the 1-based line numbers where this spelling appears.
        /// </summary>
        public IReadOnlyList<int> LineNumbers { get; }

        /// <summary>
        /// Gets the suggested correction (the group representative), or
        /// <see langword="null"/> when the group is ambiguous.
        /// </summary>
        public string? SuggestedText { get; }

        /// <summary>
        /// Gets a value indicating whether the suggestion is unambiguous. This is
        /// <see langword="false"/> when several variants share the top frequency.
        /// </summary>
        public bool IsCertain { get; }

        /// <summary>
        /// Gets the potentially misspelled word text.
        /// </summary>
        [Obsolete("Use Text instead.")]
        public string ErrorText => Text;

        /// <summary>
        /// Gets the number of times this spelling appears in the input.
        /// </summary>
        [Obsolete("Use Occurrences instead.")]
        public int RepetitionCount => Occurrences;

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorEntry"/> class.
        /// </summary>
        /// <param name="errorText">The potentially misspelled word text.</param>
        /// <param name="repetitionCount">The number of times the word appears.</param>
        /// <param name="lineNumbers">The 1-based line numbers where the word appears.</param>
        /// <exception cref="ArgumentNullException">Thrown if errorText or lineNumbers is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if repetitionCount is negative.</exception>
        public ErrorEntry(string errorText, int repetitionCount, IEnumerable<int> lineNumbers)
            : this(errorText, repetitionCount, lineNumbers, suggestedText: null, isCertain: true)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorEntry"/> class.
        /// </summary>
        /// <param name="text">The potentially misspelled word text.</param>
        /// <param name="occurrences">The number of times the word appears.</param>
        /// <param name="lineNumbers">The 1-based line numbers where the word appears.</param>
        /// <param name="suggestedText">The suggested correction, or null when ambiguous.</param>
        /// <param name="isCertain">Whether the suggestion is unambiguous.</param>
        /// <exception cref="ArgumentNullException">Thrown if text or lineNumbers is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if occurrences is negative.</exception>
        public ErrorEntry(string text, int occurrences, IEnumerable<int> lineNumbers, string? suggestedText, bool isCertain)
        {
            ArgumentNullException.ThrowIfNull(text);
            ArgumentNullException.ThrowIfNull(lineNumbers);
            if (occurrences < 0)
                throw new ArgumentOutOfRangeException(nameof(occurrences), occurrences, "Occurrences cannot be negative.");

            Text = text;
            Occurrences = occurrences;
            LineNumbers = lineNumbers.OrderBy(x => x).ToList().AsReadOnly();
            SuggestedText = suggestedText;
            IsCertain = isCertain;
        }
    }
}
