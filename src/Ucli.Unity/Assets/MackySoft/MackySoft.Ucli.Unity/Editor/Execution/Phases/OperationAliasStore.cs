using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Stores operation alias values resolved during one request execution. </summary>
    internal sealed class OperationAliasStore
    {
        private readonly Dictionary<string, ResolvedReference> resolvedReferencesByAlias =
            new Dictionary<string, ResolvedReference>(StringComparer.Ordinal);

        /// <summary> Stores or replaces one resolved reference for the specified alias. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="resolvedReference"> The resolved reference value. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="alias" /> is null, empty, whitespace, or contains outer whitespace. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="resolvedReference" /> is <see langword="null" />. </exception>
        public void Set (
            string alias,
            ResolvedReference resolvedReference)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                throw new ArgumentException("Alias must not be null, empty, or whitespace.", nameof(alias));
            }

            if (StringValueValidator.HasOuterWhitespace(alias))
            {
                throw new ArgumentException("Alias must not contain leading or trailing whitespace.", nameof(alias));
            }

            if (resolvedReference == null)
            {
                throw new ArgumentNullException(nameof(resolvedReference));
            }

            resolvedReferencesByAlias[alias] = resolvedReference;
        }

        /// <summary> Tries to get one previously stored resolved reference by alias. </summary>
        /// <param name="alias"> The alias name. </param>
        /// <param name="resolvedReference"> The resolved reference when found. </param>
        /// <returns> <see langword="true" /> when alias exists; otherwise <see langword="false" />. </returns>
        public bool TryGet (
            string alias,
            out ResolvedReference? resolvedReference)
        {
            if (string.IsNullOrWhiteSpace(alias))
            {
                resolvedReference = null;
                return false;
            }

            return resolvedReferencesByAlias.TryGetValue(alias, out resolvedReference);
        }
    }
}
