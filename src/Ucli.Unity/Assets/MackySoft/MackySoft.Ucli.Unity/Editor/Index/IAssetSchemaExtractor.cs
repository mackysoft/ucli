using System;
using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Extracts asset schema entries from one ScriptableObject-type set. </summary>
    internal interface IAssetSchemaExtractor
    {
        /// <summary> Extracts asset schema entries for one ScriptableObject-type set. </summary>
        /// <param name="assetTypes"> The asset runtime types. </param>
        /// <returns> The extraction result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assetTypes" /> is <see langword="null" />. </exception>
        IndexSchemaExtractionResult Extract (IReadOnlyList<Type> assetTypes);
    }
}
