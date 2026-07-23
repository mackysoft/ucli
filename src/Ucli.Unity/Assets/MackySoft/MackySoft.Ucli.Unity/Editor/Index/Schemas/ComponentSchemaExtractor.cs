using System;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Index;
using UnityEditor;
using UnityEngine;

#nullable enable

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Extracts schema entries for component runtime types. </summary>
    internal sealed class ComponentSchemaExtractor
    {
        private readonly IndexSchemaPropertyCollector schemaPropertyCollector = new IndexSchemaPropertyCollector();

        /// <summary> Extracts the schema entry for one validated component runtime type. </summary>
        /// <param name="componentType"> The validated component runtime type. </param>
        /// <returns> The extracted schema entry. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="componentType" /> is <see langword="null" />. </exception>
        public IndexSchemaEntryJsonContract Extract (Type componentType)
        {
            if (componentType == null)
            {
                throw new ArgumentNullException(nameof(componentType));
            }

            GameObject? host = null;
            try
            {
                host = new GameObject("__ucli-index-component__")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                var component = CreateComponentInstance(host, componentType);

                var serializedObject = new SerializedObject(component);
                var properties = schemaPropertyCollector.Collect(componentType, serializedObject);
                var kind = TextVocabulary.GetText(IndexSchemaKind.Comp);
                var typeId = IndexTypeIdFormatter.Format(componentType);
                return new IndexSchemaEntryJsonContract(
                    SchemaKey: $"{kind}:{typeId}",
                    Kind: kind,
                    TypeId: typeId,
                    DisplayName: componentType.Name,
                    Properties: properties);
            }
            finally
            {
                if (host != null)
                {
                    UnityEngine.Object.DestroyImmediate(host);
                }
            }
        }

        private static Component CreateComponentInstance (
            GameObject host,
            Type componentType)
        {
            if (componentType == typeof(Transform))
            {
                return host.transform;
            }

            var component = host.AddComponent(componentType);
            if (component == null)
            {
                throw new InvalidOperationException($"Failed to create component instance. type={componentType.FullName}");
            }

            return component;
        }
    }
}
