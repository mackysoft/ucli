using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;
using UnityEditor;
using UnityEngine;

#nullable enable

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Extracts schema entries for ScriptableObject runtime types. </summary>
    internal sealed class AssetSchemaExtractor : IAssetSchemaExtractor
    {
        private const int YieldInterval = 32;

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
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The extraction result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assetTypes" /> is <see langword="null" />. </exception>
        public async ValueTask<IndexSchemaExtractionResult> ExtractAsync (
            IReadOnlyList<Type> assetTypes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
            var editorAssemblyNames = IndexAssemblyNameSetResolver.ResolveEditorAssemblyNames();
            // NOTE: Unity serialization APIs must run on the main thread, so we use cooperative yielding instead of worker-thread offload.
            var canYield = SynchronizationContext.Current != null;
            for (var i = 0; i < assetTypes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (canYield && i > 0 && (i % YieldInterval) == 0)
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var assetType = assetTypes[i];
                if (!IsValidAssetType(assetType, editorAssemblyNames))
                {
                    continue;
                }

                var entry = TryExtractEntry(assetType, out var propertyResult);
                entries.Add(entry);
                referencedTypes.Add(assetType);
                foreach (var referencedType in propertyResult.ReferencedTypes)
                {
                    referencedTypes.Add(referencedType);
                }
            }

            if (entries.Count == 0)
            {
                return IndexSchemaExtractionResult.Empty();
            }

            return new IndexSchemaExtractionResult(IndexJsonOrderingPolicy.OrderSchemaEntries(entries), referencedTypes);
        }

        private IndexSchemaEntryJsonContract TryExtractEntry (
            Type assetType,
            out IndexSchemaPropertyCollectionResult propertyResult)
        {
            ScriptableObject? instance = null;
            try
            {
                instance = ScriptableObject.CreateInstance(assetType);
                if (instance == null)
                {
                    throw new InvalidOperationException($"Failed to create ScriptableObject instance. type={assetType.FullName}");
                }

                var serializedObject = new SerializedObject(instance);
                propertyResult = schemaPropertyCollector.Collect(assetType, serializedObject);
                return new IndexSchemaEntryJsonContract(
                    SchemaKey: CreateSchemaKey(assetType),
                    Kind: ContractLiteralCodec.ToValue(IndexSchemaKind.Asset),
                    TypeId: IndexTypeIdFormatter.Format(assetType),
                    DisplayName: assetType.Name,
                    Properties: propertyResult.Properties);
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
            return $"{ContractLiteralCodec.ToValue(IndexSchemaKind.Asset)}:{typeId}";
        }

        private static bool IsValidAssetType (
            Type assetType,
            HashSet<string> editorAssemblyNames)
        {
            var assemblyName = assetType?.Assembly.GetName().Name;
            return assetType != null
                && typeof(ScriptableObject).IsAssignableFrom(assetType)
                && !assetType.IsAbstract
                && !assetType.IsGenericTypeDefinition
                && !assetType.ContainsGenericParameters
                && !string.IsNullOrWhiteSpace(assemblyName)
                && !editorAssemblyNames.Contains(assemblyName);
        }
    }
}
