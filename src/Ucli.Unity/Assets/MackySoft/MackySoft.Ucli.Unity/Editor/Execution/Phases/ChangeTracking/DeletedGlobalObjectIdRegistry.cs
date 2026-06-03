using System;
using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks stable GlobalObjectIds that were removed from request-local plan state. </summary>
    internal sealed class DeletedGlobalObjectIdRegistry
    {
        private readonly HashSet<string> deletedGlobalObjectIds = new HashSet<string>(StringComparer.Ordinal);

        /// <summary> Marks one stable GlobalObjectId as deleted from the current request-local plan state. </summary>
        /// <param name="globalObjectId"> The stable GlobalObjectId to record. Must not be <see langword="null" />, empty, or whitespace. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="globalObjectId" /> is <see langword="null" />, empty, or whitespace. </exception>
        public void MarkDeleted (string globalObjectId)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId))
            {
                throw new ArgumentException("GlobalObjectId must not be null, empty, or whitespace.", nameof(globalObjectId));
            }

            deletedGlobalObjectIds.Add(globalObjectId);
        }

        /// <summary> Determines whether one stable GlobalObjectId was deleted from the current request-local plan state. </summary>
        /// <param name="globalObjectId"> The stable GlobalObjectId to test. </param>
        /// <returns> <see langword="true" /> when the identifier is tracked as deleted; otherwise <see langword="false" />. </returns>
        public bool Contains (string globalObjectId)
        {
            if (string.IsNullOrWhiteSpace(globalObjectId))
            {
                return false;
            }

            return deletedGlobalObjectIds.Contains(globalObjectId);
        }

        /// <summary> Clears all deleted GlobalObjectId markers tracked for the current request. </summary>
        public void Clear ()
        {
            deletedGlobalObjectIds.Clear();
        }
    }
}
