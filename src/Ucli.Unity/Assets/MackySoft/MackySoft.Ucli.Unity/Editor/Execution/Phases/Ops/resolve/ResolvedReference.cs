using System;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one resolved selector target normalized as a GlobalObjectId string. </summary>
    internal sealed record ResolvedReference
    {
        /// <summary> Gets the normalized GlobalObjectId string. </summary>
        public string GlobalObjectId { get; }

        /// <summary> Initializes a new instance of the <see cref="ResolvedReference" /> class. </summary>
        /// <param name="globalObjectId"> The normalized GlobalObjectId string. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="globalObjectId" /> is null, empty, whitespace, has outer whitespace, or is malformed. </exception>
        public ResolvedReference (string globalObjectId)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId))
            {
                throw new ArgumentException("GlobalObjectId must not be null, empty, or whitespace.", nameof(globalObjectId));
            }

            if (StringValueValidator.HasOuterWhitespace(globalObjectId))
            {
                throw new ArgumentException("GlobalObjectId must not contain leading or trailing whitespace.", nameof(globalObjectId));
            }

            if (!UnityEditor.GlobalObjectId.TryParse(globalObjectId, out var parsedGlobalObjectId))
            {
                throw new ArgumentException("GlobalObjectId must be a valid GlobalObjectId string.", nameof(globalObjectId));
            }

            GlobalObjectId = parsedGlobalObjectId.ToString();
        }
    }
}