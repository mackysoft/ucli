using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Tracks stable GlobalObjectIds that were removed from request-local plan state. </summary>
    internal sealed class DeletedGlobalObjectIdRegistry
    {
        private readonly HashSet<UnityGlobalObjectId> deletedGlobalObjectIds =
            new HashSet<UnityGlobalObjectId>();

        /// <summary> Marks one stable GlobalObjectId as deleted from the current request-local plan state. </summary>
        /// <param name="globalObjectId"> The stable identity to record. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="globalObjectId" /> is <see langword="null" />. </exception>
        public void MarkDeleted (UnityGlobalObjectId globalObjectId)
        {
            if (globalObjectId == null)
            {
                throw new ArgumentNullException(nameof(globalObjectId));
            }

            deletedGlobalObjectIds.Add(globalObjectId);
        }

        /// <summary> Determines whether one stable GlobalObjectId was deleted from the current request-local plan state. </summary>
        /// <param name="globalObjectId"> The stable identity to test. </param>
        /// <returns> <see langword="true" /> when the identifier is tracked as deleted; otherwise <see langword="false" />. </returns>
        public bool Contains (UnityGlobalObjectId globalObjectId)
        {
            if (globalObjectId == null)
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
