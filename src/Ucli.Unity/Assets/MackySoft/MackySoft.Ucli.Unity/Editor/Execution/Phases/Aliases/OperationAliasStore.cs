using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Stores operation alias values resolved during one request execution. </summary>
    internal sealed class OperationAliasStore
    {
        private readonly Dictionary<RequestLocalAliasIdentity, UnityGlobalObjectId> globalObjectIdsByAlias =
            new Dictionary<RequestLocalAliasIdentity, UnityGlobalObjectId>();

        /// <summary> Stores or replaces one resolved reference for the specified alias. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="globalObjectId"> The resolved object identity. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="alias" /> is null, empty, whitespace, or contains outer whitespace. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="globalObjectId" /> is <see langword="null" />. </exception>
        public void Set (
            RequestLocalAliasIdentity alias,
            UnityGlobalObjectId globalObjectId)
        {
            if (alias == null)
            {
                throw new ArgumentNullException(nameof(alias));
            }

            if (globalObjectId == null)
            {
                throw new ArgumentNullException(nameof(globalObjectId));
            }

            globalObjectIdsByAlias[alias] = globalObjectId;
        }

        /// <summary> Tries to get one previously stored resolved reference by alias. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="globalObjectId"> The resolved identity when found. </param>
        /// <returns> <see langword="true" /> when alias exists; otherwise <see langword="false" />. </returns>
        public bool TryGet (
            RequestLocalAliasIdentity alias,
            [NotNullWhen(true)] out UnityGlobalObjectId? globalObjectId)
        {
            if (alias == null)
            {
                globalObjectId = null;
                return false;
            }

            return globalObjectIdsByAlias.TryGetValue(alias, out globalObjectId);
        }
    }
}
