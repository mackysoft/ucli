using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Extracts asset schema entries from one ScriptableObject-type set. </summary>
    internal interface IAssetSchemaExtractor
    {
        /// <summary> Extracts asset schema entries for one ScriptableObject-type set. </summary>
        /// <param name="assetTypes"> The asset runtime types. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The extraction result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assetTypes" /> is <see langword="null" />. </exception>
        ValueTask<IndexSchemaExtractionResult> Extract (
            IReadOnlyList<Type> assetTypes,
            CancellationToken cancellationToken = default);
    }
}