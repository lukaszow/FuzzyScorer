using System;

namespace FuzzyScorer
{
    /// <summary>
    /// Represents a word and its count (immutable).
    /// </summary>
    public class WordScore
    {
        /// <summary>
        /// Gets the word text.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the number of occurrences represented by this entry.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Gets the number of occurrences represented by this entry.
        /// </summary>
        [Obsolete("Use Count instead.")]
        public int Score => Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="WordScore"/> class.
        /// </summary>
        /// <param name="text">The word text.</param>
        /// <param name="score">The number of occurrences.</param>
        /// <exception cref="ArgumentNullException">Thrown if text is null.</exception>
        /// <exception cref="ArgumentException">Thrown if score is negative.</exception>
        public WordScore(string text, int score)
        {
            ArgumentNullException.ThrowIfNull(text);
            if (score < 0)
                throw new ArgumentException("Count cannot be negative.", nameof(score));

            Text = text;
            Count = score;
        }
    }
}
