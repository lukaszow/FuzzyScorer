namespace FuzzyScorer
{
    /// <summary>
    /// Represents a word and its associated score (immutable).
    /// </summary>
    public class WordScore
    {
        /// <summary>
        /// Gets the word text.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the word score (frequency or similarity score).
        /// </summary>
        public int Score { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WordScore"/> class.
        /// </summary>
        /// <param name="text">The word text.</param>
        /// <param name="score">The word score (frequency or similarity score).</param>
        /// <exception cref="ArgumentNullException">Thrown if text is null.</exception>
        /// <exception cref="ArgumentException">Thrown if score is negative.</exception>
        public WordScore(string text, int score)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text));
            if (score < 0)
                throw new ArgumentException("Score cannot be negative.", nameof(score));

            Text = text;
            Score = score;
        }
    }
}
