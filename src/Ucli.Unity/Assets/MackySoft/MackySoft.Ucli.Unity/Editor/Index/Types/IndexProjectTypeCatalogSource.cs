using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Resolves project type sets from Unity type cache and assembly metadata. </summary>
    internal sealed class IndexProjectTypeCatalogSource : IIndexProjectTypeCatalogSource
    {
        /// <summary> Resolves component/asset/serialize-reference type sets from current project state. </summary>
        /// <returns> The resolved project type catalog. </returns>
        public IndexProjectTypeCatalog Resolve ()
        {
            var runtimeAssemblyNames = IndexAssemblyNameSetResolver.ResolveRuntimeAssemblyNames();
            var componentTypes = ResolveComponentTypes(runtimeAssemblyNames);
            var assetTypes = ResolveAssetTypes(runtimeAssemblyNames);
            var serializeReferenceCandidateTypes = ResolveSerializeReferenceCandidateTypes(runtimeAssemblyNames);

            return new IndexProjectTypeCatalog(componentTypes, assetTypes, serializeReferenceCandidateTypes);
        }

        private static IReadOnlyList<Type> ResolveComponentTypes (
            HashSet<string> runtimeAssemblyNames)
        {
            var componentTypes = new List<Type>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<Component>())
            {
                if (IsProjectSchemaRootType(type, runtimeAssemblyNames))
                {
                    componentTypes.Add(type);
                }
            }

            componentTypes.Sort(static (x, y) =>
                StringComparer.Ordinal.Compare(IndexTypeIdFormatter.Format(x), IndexTypeIdFormatter.Format(y)));
            return componentTypes;
        }

        private static IReadOnlyList<Type> ResolveAssetTypes (
            HashSet<string> runtimeAssemblyNames)
        {
            var assetTypes = new List<Type>();
            foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            {
                if (IsProjectSchemaRootType(type, runtimeAssemblyNames))
                {
                    assetTypes.Add(type);
                }
            }

            assetTypes.Sort(static (x, y) =>
                StringComparer.Ordinal.Compare(IndexTypeIdFormatter.Format(x), IndexTypeIdFormatter.Format(y)));
            return assetTypes;
        }

        private static IReadOnlyList<Type> ResolveSerializeReferenceCandidateTypes (
            HashSet<string> runtimeAssemblyNames)
        {
            var candidateTypes = new List<Type>();
            foreach (var type in TypeCache.GetTypesWithAttribute<SerializableAttribute>())
            {
                if (IsProjectAssemblyType(type, runtimeAssemblyNames) && IndexTypeClassification.IsSerializeReferenceCandidate(type))
                {
                    candidateTypes.Add(type);
                }
            }

            candidateTypes.Sort(static (x, y) =>
                StringComparer.Ordinal.Compare(IndexTypeIdFormatter.Format(x), IndexTypeIdFormatter.Format(y)));
            return candidateTypes;
        }

        private static bool IsProjectSchemaRootType (
            Type type,
            HashSet<string> runtimeAssemblyNames)
        {
            return IsProjectAssemblyType(type, runtimeAssemblyNames)
                && !type.IsAbstract
                && !type.IsGenericTypeDefinition
                && !type.ContainsGenericParameters;
        }

        private static bool IsProjectAssemblyType (
            Type type,
            HashSet<string> runtimeAssemblyNames)
        {
            if (type == null)
            {
                return false;
            }

            var assemblyName = type.Assembly.GetName().Name;
            return !string.IsNullOrWhiteSpace(assemblyName) && runtimeAssemblyNames.Contains(assemblyName);
        }
    }
}