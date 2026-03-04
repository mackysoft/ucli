using System;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Index;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Extracts schema entries for ScriptableObject runtime types. </summary>
    internal sealed class AssetSchemaExtractor : IAssetSchemaExtractor
    {
        private readonly IIndexSchemaPropertyCollector schemaPropertyCollector;

        /// <summary> Initializes a new instance of the <see cref="AssetSchemaExtractor" /> class. </summary>
        /// <param name="schemaPropertyCollector"> The shared schema-property collector dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="schemaPropertyCollector" /> is <see langword="null" />. </exception>
        public AssetSchemaExtractor (IIndexSchemaPropertyCollector schemaPropertyCollector)
        {
            this.schemaPropertyCollector = schemaPropertyCollector ?? throw new ArgumentNullException(nameof(schemaPropertyCollector));
        }

        /// <summary> Extracts asset schema entries for one ScriptableObject-type set. </summary>
        /// <param name="assetTypes"> The asset runtime types. </param>
        /// <returns> The extraction result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assetTypes" /> is <see langword="null" />. </exception>
        public IndexSchemaExtractionResult Extract (IReadOnlyList<Type> assetTypes)
        {
            if (assetTypes == null)
            {
                throw new ArgumentNullException(nameof(assetTypes));
            }

            if (assetTypes.Count == 0)
            {
                return IndexSchemaExtractionResult.Empty();
            }

            var entries = new List<IndexSchemaEntryJsonContract>(assetTypes.Count);
            var referencedTypes = new HashSet<Type>();
            for (var i = 0; i < assetTypes.Count; i++)
            {
                var assetType = assetTypes[i];
                if (!IsValidAssetType(assetType))
                {
                    continue;
                }

                var entry = TryExtractEntry(assetType, out var propertyResult);
                if (entry == null)
                {
                    continue;
                }

                entries.Add(entry);
                referencedTypes.Add(assetType);
                foreach (var referencedType in propertyResult!.ReferencedTypes)
                {
                    referencedTypes.Add(referencedType);
                }
            }

            if (entries.Count == 0)
            {
                return IndexSchemaExtractionResult.Empty();
            }

            entries.Sort(static (x, y) =>
                StringComparer.Ordinal.Compare(x.SchemaKey ?? string.Empty, y.SchemaKey ?? string.Empty));
            return new IndexSchemaExtractionResult(entries, referencedTypes);
        }

        private IndexSchemaEntryJsonContract? TryExtractEntry (
            Type assetType,
            out IndexSchemaPropertyCollectionResult? propertyResult)
        {
            propertyResult = null;
            ScriptableObject? instance = null;
            try
            {
                instance = ScriptableObject.CreateInstance(assetType);
                if (instance == null)
                {
                    return null;
                }

                var serializedObject = new SerializedObject(instance);
                propertyResult = schemaPropertyCollector.Collect(assetType, serializedObject);
                return new IndexSchemaEntryJsonContract(
                    SchemaKey: CreateSchemaKey(assetType),
                    Kind: IndexSchemaKindValues.Asset,
                    TypeId: IndexTypeIdFormatter.Format(assetType),
                    DisplayName: assetType.Name,
                    Properties: propertyResult.Properties);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }

        private static string CreateSchemaKey (Type assetType)
        {
            var typeId = IndexTypeIdFormatter.Format(assetType);
            return $"{IndexSchemaKindValues.Asset}:{typeId}";
        }

        private static bool IsValidAssetType (Type assetType)
        {
            return assetType != null
                && typeof(ScriptableObject).IsAssignableFrom(assetType)
                && !assetType.IsAbstract
                && !assetType.IsGenericTypeDefinition
                && !assetType.ContainsGenericParameters;
        }
    }
}
