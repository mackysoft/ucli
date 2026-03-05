using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Extracts component schema entries from one component-type set. </summary>
    internal interface IComponentSchemaExtractor
    {
        /// <summary> Extracts component schema entries for one component-type set. </summary>
        /// <param name="componentTypes"> The component runtime types. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The extraction result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="componentTypes" /> is <see langword="null" />. </exception>
        ValueTask<IndexSchemaExtractionResult> Extract (
            IReadOnlyList<Type> componentTypes,
            CancellationToken cancellationToken = default);
    }
}
