using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Stores operation alias values resolved during one request execution. </summary>
    internal sealed class OperationAliasStore
    {
        private readonly Dictionary<string, UnityGlobalObjectId> globalObjectIdsByAlias =
            new Dictionary<string, UnityGlobalObjectId>(StringComparer.Ordinal);

        /// <summary> Stores or replaces one resolved reference for the specified alias. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="globalObjectId"> The resolved object identity. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="alias" /> is null, empty, whitespace, or contains outer whitespace. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="globalObjectId" /> is <see langword="null" />. </exception>
        public void Set (
            string alias,
            UnityGlobalObjectId globalObjectId)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException("Alias must not be null, empty, or whitespace.", nameof(alias));
            }

            if (StringValueValidator.HasOuterWhitespace(alias))
            {
                throw new ArgumentException("Alias must not contain leading or trailing whitespace.", nameof(alias));
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
            string alias,
            [NotNullWhen(true)] out UnityGlobalObjectId? globalObjectId)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                globalObjectId = null;
                return false;
            }

            return globalObjectIdsByAlias.TryGetValue(alias, out globalObjectId);
        }
    }
}
