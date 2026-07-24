using System;
using System.Collections.Generic;
using System.Linq;
using MackySoft.Text.Vocabularies;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Unity.Index;
using UnityEditor;

#nullable enable

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Builds schema results from one live asset instance. </summary>
    internal sealed class AssetTargetSchemaBuilder
    {
        /// <summary> Builds one schema entry from a live asset instance. </summary>
        /// <param name="unityObject"> The live asset object. </param>
        /// <returns> The schema entry. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="unityObject" /> is <see langword="null" />. </exception>
        public IndexSchemaEntryJsonContract Build (UnityEngine.Object unityObject)
        {
            if (unityObject == null)
            {
                throw new ArgumentNullException(nameof(unityObject));
            }

            var assetType = unityObject.GetType();
            var serializedObject = new SerializedObject(unityObject);
            var properties = CollectProperties(serializedObject, assetType);
            var typeId = IndexTypeIdFormatter.Format(assetType);
            return new IndexSchemaEntryJsonContract(
                SchemaKey: $"{TextVocabulary.GetText(IndexSchemaKind.Asset)}:{typeId}",
                Kind: TextVocabulary.GetText(IndexSchemaKind.Asset),
                TypeId: typeId,
                DisplayName: assetType.Name,
                Properties: properties);
        }

        private static IReadOnlyList<IndexSchemaPropertyEntryJsonContract> CollectProperties (
            SerializedObject serializedObject,
            Type rootType)
        {
            var propertyMap = new Dictionary<string, IndexSchemaPropertyEntryJsonContract>(StringComparer.Ordinal);
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;
                var propertyPath = iterator.propertyPath;
                if (string.IsNullOrWhiteSpace(propertyPath))
                {
                    continue;
                }

                var normalizedPath = IndexSerializedPropertyPathNormalizer.Normalize(propertyPath);
                if (propertyMap.ContainsKey(normalizedPath))
                {
                    continue;
                }

                string? declaredTypeId = null;
                string? elementTypeId = null;
                var isArray = false;
                if (SerializedObjectDeclaredTypeResolver.TryResolve(
                    serializedObject,
                    rootType,
                    iterator,
                    out var declaredType,
                    out _))
                {
                    declaredTypeId = IndexTypeIdFormatter.Format(declaredType);
                    var elementType = IndexDeclaredTypeResolver.TryResolveElementType(declaredType);
                    isArray = iterator.isArray
                        && iterator.propertyType != SerializedPropertyType.String
                        && elementType != null;
                    if (isArray && elementType != null)
                    {
                        elementTypeId = IndexTypeIdFormatter.Format(elementType);
                    }
                }

                propertyMap.Add(
                    normalizedPath,
                    new IndexSchemaPropertyEntryJsonContract(
                        Path: normalizedPath,
                        PropertyType: IndexSerializedPropertyTypeMapper.ToLiteral(iterator.propertyType),
                        DeclaredTypeId: declaredTypeId,
                        IsArray: isArray,
                        ElementTypeId: elementTypeId,
                        IsReadOnly: !iterator.editable));
            }

            return propertyMap.Values
                .OrderBy(static property => property.Path ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
        }
    }
}