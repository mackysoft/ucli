using System;
using System.Collections.Generic;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Holds project type sets used across schema extraction and type catalog composition. </summary>
    internal sealed class IndexProjectTypeCatalog
    {
        /// <summary> Initializes a new instance of the <see cref="IndexProjectTypeCatalog" /> class. </summary>
        /// <param name="componentTypes"> The component root types. </param>
        /// <param name="assetTypes"> The ScriptableObject root types. </param>
        /// <param name="serializeReferenceCandidateTypes"> The serializable polymorphic candidate types. </param>
        /// <exception cref="ArgumentNullException"> Thrown when any argument is <see langword="null" />. </exception>
        public IndexProjectTypeCatalog (
            IReadOnlyList<Type> componentTypes,
            IReadOnlyList<Type> assetTypes,
            IReadOnlyList<Type> serializeReferenceCandidateTypes)
        {
            ComponentTypes = componentTypes ?? throw new ArgumentNullException(nameof(componentTypes));
            AssetTypes = assetTypes ?? throw new ArgumentNullException(nameof(assetTypes));
            SerializeReferenceCandidateTypes = serializeReferenceCandidateTypes ?? throw new ArgumentNullException(nameof(serializeReferenceCandidateTypes));
        }

        /// <summary> Gets component root types. </summary>
        public IReadOnlyList<Type> ComponentTypes { get; }

        /// <summary> Gets ScriptableObject root types. </summary>
        public IReadOnlyList<Type> AssetTypes { get; }

        /// <summary> Gets serializable polymorphic candidate types. </summary>
        public IReadOnlyList<Type> SerializeReferenceCandidateTypes { get; }
    }
}