namespace WordsCloud
{
    /// <summary>
    /// Represents a word and its associated score.
    /// </summary>
    public class WordScore
    {
        /// <summary>
        /// Gets or sets the word text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the word score (frequency or similarity score).
        /// </summary>
        public int Score { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WordScore"/> class.
        /// </summary>
        /// <param name="text">The word text.</param>
        /// <param name="score">The word score (frequency or similarity score).</param>
        public WordScore(string text, int score)
        {
            Text = text;
            Score = score;
        }
    }
}
