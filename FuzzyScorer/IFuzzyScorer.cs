using System.Threading;
using System.Threading.Tasks;

namespace FuzzyScorer
{
    /// <summary>
    /// Provides fuzzy scoring capabilities, analyzing input text for potential
    /// typographical errors by grouping similar words and reporting variants.
    /// </summary>
    public interface IFuzzyScorer
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
        /// <exception cref="System.ArgumentException">Thrown if inputText exceeds limits or sensitivity is out of range.</exception>
        /// <exception cref="System.OperationCanceledException">Thrown if the operation is cancelled.</exception>
        Task<FuzzyScorerResult> ScoreAsync(string inputText, double sensitivity, CancellationToken cancellationToken);
    }
}
