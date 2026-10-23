using System;
using System.Collections.Generic;
using System.Linq;

namespace FuzzyScorer
{
    /// <summary>
    /// Represents one similarity group: its representative, the distinct spellings,
    /// and the total number of occurrences.
    /// </summary>
    public sealed class WordGroup
    {
        /// <summary>
        /// Gets the representative spelling of the group.
        /// </summary>
        public string Representative { get; }

        /// <summary>
        /// Gets the distinct spellings in first-occurrence order.
        /// </summary>
        public IReadOnlyList<string> Members { get; }

        /// <summary>
        /// Gets the total number of occurrences across all members.
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WordGroup"/> class.
        /// </summary>
        /// <param name="representative">The representative spelling.</param>
        /// <param name="members">The distinct spellings in first-occurrence order.</param>
        /// <param name="count">The total number of occurrences.</param>
        /// <exception cref="ArgumentNullException">Thrown if a parameter is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if count is negative.</exception>
        public WordGroup(string representative, IEnumerable<string> members, int count)
        {
            ArgumentNullException.ThrowIfNull(representative);
            ArgumentNullException.ThrowIfNull(members);
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Count cannot be negative.");

            Representative = representative;
            Members = members.ToList().AsReadOnly();
            Count = count;
        }
    }
}
