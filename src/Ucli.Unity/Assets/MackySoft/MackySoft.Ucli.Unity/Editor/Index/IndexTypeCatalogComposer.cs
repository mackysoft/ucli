using System;
using System.Collections.Generic;
using System.Linq;
using MackySoft.Ucli.Contracts.Index;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Composes type catalog entries from schema roots and extracted referenced types. </summary>
    internal sealed class IndexTypeCatalogComposer : IIndexTypeCatalogComposer
    {
        /// <summary> Composes sorted type entries for types.catalog.json. </summary>
        /// <param name="projectTypeCatalog"> The project root type catalog. </param>
        /// <param name="componentReferencedTypes"> Referenced types collected from component schemas. </param>
        /// <param name="assetReferencedTypes"> Referenced types collected from asset schemas. </param>
        /// <returns> The sorted type entries. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when any collection argument is <see langword="null" />. </exception>
        public IReadOnlyList<IndexTypeEntryJsonContract> Compose (
            IndexProjectTypeCatalog projectTypeCatalog,
            IReadOnlyCollection<Type> componentReferencedTypes,
            IReadOnlyCollection<Type> assetReferencedTypes)
        {
            if (projectTypeCatalog == null)
            {
                throw new ArgumentNullException(nameof(projectTypeCatalog));
            }

            if (componentReferencedTypes == null)
            {
                throw new ArgumentNullException(nameof(componentReferencedTypes));
            }

            if (assetReferencedTypes == null)
            {
                throw new ArgumentNullException(nameof(assetReferencedTypes));
            }

            var catalogTypes = BuildCatalogTypes(
                projectTypeCatalog.ComponentTypes,
                projectTypeCatalog.AssetTypes,
                projectTypeCatalog.SerializeReferenceCandidateTypes,
                componentReferencedTypes,
                assetReferencedTypes);
            return catalogTypes
                .Select(CreateTypeEntry)
                .OrderBy(static entry => entry.TypeId ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyCollection<Type> BuildCatalogTypes (
            IReadOnlyList<Type> componentTypes,
            IReadOnlyList<Type> assetTypes,
            IReadOnlyList<Type> serializeReferenceCandidateTypes,
            IReadOnlyCollection<Type> componentReferencedTypes,
            IReadOnlyCollection<Type> assetReferencedTypes)
        {
            var types = new HashSet<Type>();
            AddTypes(types, componentTypes);
            AddTypes(types, assetTypes);
            AddTypes(types, serializeReferenceCandidateTypes);
            AddTypes(types, componentReferencedTypes);
            AddTypes(types, assetReferencedTypes);

            return types
                .OrderBy(static type => IndexTypeIdFormatter.Format(type), StringComparer.Ordinal)
                .ToArray();
        }

        private static void AddTypes (
            HashSet<Type> destination,
            IEnumerable<Type> source)
        {
            foreach (var type in source)
            {
                if (IndexTypeClassification.IsCatalogType(type))
                {
                    destination.Add(type);
                }
            }
        }

        private static IndexTypeEntryJsonContract CreateTypeEntry (Type type)
        {
            var assemblyName = type.Assembly.GetName().Name ?? "unknown";
            var baseTypeId = type.BaseType == null
                ? null
                : IndexTypeIdFormatter.Format(type.BaseType);
            return new IndexTypeEntryJsonContract(
                TypeId: IndexTypeIdFormatter.Format(type),
                DisplayName: type.Name,
                Namespace: type.Namespace,
                AssemblyName: assemblyName,
                BaseTypeId: baseTypeId,
                Flags: new IndexTypeFlagsJsonContract(
                    IsAbstract: type.IsAbstract,
                    IsGenericDefinition: type.IsGenericTypeDefinition,
                    IsUnityObject: typeof(UnityEngine.Object).IsAssignableFrom(type),
                    IsComponent: typeof(Component).IsAssignableFrom(type),
                    IsScriptableObject: typeof(ScriptableObject).IsAssignableFrom(type),
                    IsSerializeReferenceCandidate: IndexTypeClassification.IsSerializeReferenceCandidate(type)));
        }
    }
}