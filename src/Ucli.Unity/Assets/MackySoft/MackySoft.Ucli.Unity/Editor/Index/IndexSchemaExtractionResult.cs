using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Represents one schema extraction output that contains schema entries and referenced runtime types. </summary>
    /// <param name="Entries"> The extracted schema entries. </param>
    /// <param name="ReferencedTypes"> The referenced runtime types found while extracting properties. </param>
    internal sealed record IndexSchemaExtractionResult (
        IReadOnlyList<IndexSchemaEntryJsonContract> Entries,
        IReadOnlyCollection<Type> ReferencedTypes)
    {
        /// <summary> Creates one empty schema extraction result. </summary>
        /// <returns> The empty extraction result. </returns>
        public static IndexSchemaExtractionResult Empty ()
        {
            return new IndexSchemaExtractionResult(
                Array.Empty<IndexSchemaEntryJsonContract>(),
                Array.Empty<Type>());
        }
    }
}
