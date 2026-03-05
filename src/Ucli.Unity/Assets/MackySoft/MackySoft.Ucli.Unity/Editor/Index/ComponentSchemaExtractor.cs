using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Index;
using UnityEditor;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Extracts schema entries for component runtime types. </summary>
    internal sealed class ComponentSchemaExtractor : IComponentSchemaExtractor
    {
        private const int YieldInterval = 32;

        private readonly IIndexSchemaPropertyCollector schemaPropertyCollector;

        /// <summary> Initializes a new instance of the <see cref="ComponentSchemaExtractor" /> class. </summary>
        /// <param name="schemaPropertyCollector"> The shared schema-property collector dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="schemaPropertyCollector" /> is <see langword="null" />. </exception>
        public ComponentSchemaExtractor (IIndexSchemaPropertyCollector schemaPropertyCollector)
        {
            this.schemaPropertyCollector = schemaPropertyCollector ?? throw new ArgumentNullException(nameof(schemaPropertyCollector));
        }

        /// <summary> Extracts component schema entries for one component-type set. </summary>
        /// <param name="componentTypes"> The component runtime types. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> The extraction result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="componentTypes" /> is <see langword="null" />. </exception>
        public async ValueTask<IndexSchemaExtractionResult> Extract (
            IReadOnlyList<Type> componentTypes,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (componentTypes == null)
            {
                throw new ArgumentNullException(nameof(componentTypes));
            }

            if (componentTypes.Count == 0)
            {
                return IndexSchemaExtractionResult.Empty();
            }

            var entries = new List<IndexSchemaEntryJsonContract>(componentTypes.Count);
            var referencedTypes = new HashSet<Type>();
            // NOTE: Unity serialization APIs must run on the main thread, so we use cooperative yielding instead of worker-thread offload.
            var canYield = SynchronizationContext.Current != null;
            for (var i = 0; i < componentTypes.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (canYield && i > 0 && (i % YieldInterval) == 0)
                {
                    await Task.Yield();
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var componentType = componentTypes[i];
                if (!IsValidComponentType(componentType))
                {
                    continue;
                }

                var entry = TryExtractEntry(componentType, out var propertyResult);
                if (entry == null)
                {
                    continue;
                }

                entries.Add(entry);
                referencedTypes.Add(componentType);
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
            Type componentType,
            out IndexSchemaPropertyCollectionResult? propertyResult)
        {
            propertyResult = null;
            GameObject? host = null;
            try
            {
                host = new GameObject("__ucli-index-component__")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var component = host.AddComponent(componentType);
                if (component == null)
                {
                    return null;
                }

                var serializedObject = new SerializedObject(component);
                propertyResult = schemaPropertyCollector.Collect(componentType, serializedObject);
                return new IndexSchemaEntryJsonContract(
                    SchemaKey: CreateSchemaKey(componentType),
                    Kind: IndexSchemaKindValues.Comp,
                    TypeId: IndexTypeIdFormatter.Format(componentType),
                    DisplayName: componentType.Name,
                    Properties: propertyResult.Properties);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (host != null)
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }
        }

        private static string CreateSchemaKey (Type componentType)
        {
            var typeId = IndexTypeIdFormatter.Format(componentType);
            return $"{IndexSchemaKindValues.Comp}:{typeId}";
        }

        private static bool IsValidComponentType (Type componentType)
        {
            return componentType != null
                && typeof(Component).IsAssignableFrom(componentType)
                && !componentType.IsAbstract
                && !componentType.IsGenericTypeDefinition
                && !componentType.ContainsGenericParameters;
        }
    }
}