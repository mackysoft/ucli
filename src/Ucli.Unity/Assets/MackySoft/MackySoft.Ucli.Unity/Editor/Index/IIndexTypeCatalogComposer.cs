using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Index;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Composes type catalog entries from project root types and referenced schema types. </summary>
    internal interface IIndexTypeCatalogComposer
    {
        /// <summary> Composes sorted type entries for types.catalog.json. </summary>
        /// <param name="projectTypeCatalog"> The project root type catalog. </param>
        /// <param name="componentReferencedTypes"> Referenced types collected from component schemas. </param>
        /// <param name="assetReferencedTypes"> Referenced types collected from asset schemas. </param>
        /// <returns> The sorted type entries. </returns>
        IReadOnlyList<IndexTypeEntryJsonContract> Compose (
            IndexProjectTypeCatalog projectTypeCatalog,
            IReadOnlyCollection<Type> componentReferencedTypes,
            IReadOnlyCollection<Type> assetReferencedTypes);
    }
}