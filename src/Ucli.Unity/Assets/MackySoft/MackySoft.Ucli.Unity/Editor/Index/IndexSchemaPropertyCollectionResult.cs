using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Represents one collected property set and referenced-type set from one schema root type. </summary>
    /// <param name="Properties"> The collected schema properties. </param>
    /// <param name="ReferencedTypes"> The runtime types referenced by collected properties. </param>
    internal sealed record IndexSchemaPropertyCollectionResult (
        IReadOnlyList<IndexSchemaPropertyEntryJsonContract> Properties,
        IReadOnlyCollection<Type> ReferencedTypes)
    {
        /// <summary> Creates one empty property collection result. </summary>
        /// <returns> The empty property collection result. </returns>
        public static IndexSchemaPropertyCollectionResult Empty ()
        {
            return new IndexSchemaPropertyCollectionResult(
                Array.Empty<IndexSchemaPropertyEntryJsonContract>(),
                Array.Empty<Type>());
        }
    }
}
