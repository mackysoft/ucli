using System;
using System.Collections.Generic;
using System.Linq;
using MackySoft.Ucli.Contracts.Index;
using UnityEditor;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Collects deterministic schema property entries from one SerializedObject walk. </summary>
    internal sealed class IndexSchemaPropertyCollector : IIndexSchemaPropertyCollector
    {
        /// <summary> Collects schema properties for one serialized object instance and root type. </summary>
        /// <param name="rootType"> The serialized root runtime type. </param>
        /// <param name="serializedObject"> The serialized object instance. </param>
        /// <returns> The collected property result. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when any argument is <see langword="null" />. </exception>
        public IndexSchemaPropertyCollectionResult Collect (
            Type rootType,
            SerializedObject serializedObject)
        {
            if (rootType == null)
            {
                throw new ArgumentNullException(nameof(rootType));
            }

            if (serializedObject == null)
            {
                throw new ArgumentNullException(nameof(serializedObject));
            }

            var propertyMap = new Dictionary<string, IndexSchemaPropertyEntryJsonContract>(StringComparer.Ordinal);
            var referencedTypes = new HashSet<Type>();

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

                var resolution = IndexDeclaredTypeResolver.Resolve(rootType, propertyPath);
                if (!resolution.IsResolved)
                {
                    continue;
                }

                var declaredType = resolution.DeclaredType;
                var declaredTypeId = IndexTypeIdFormatter.Format(declaredType);
                var propertyTypeLiteral = IndexSerializedPropertyTypeMapper.ToLiteral(iterator.propertyType);

                var resolvedElementType = IndexDeclaredTypeResolver.TryResolveElementType(declaredType);
                var isArray = iterator.isArray
                    && iterator.propertyType != SerializedPropertyType.String
                    && resolvedElementType != null;
                string? elementTypeId = null;
                if (isArray && resolvedElementType != null)
                {
                    elementTypeId = IndexTypeIdFormatter.Format(resolvedElementType);
                }

                propertyMap.Add(
                    normalizedPath,
                    new IndexSchemaPropertyEntryJsonContract(
                        Path: normalizedPath,
                        PropertyType: propertyTypeLiteral,
                        DeclaredTypeId: declaredTypeId,
                        IsArray: isArray,
                        ElementTypeId: elementTypeId,
                        IsReadOnly: !iterator.editable));

                referencedTypes.Add(declaredType);
                if (resolvedElementType != null)
                {
                    referencedTypes.Add(resolvedElementType);
                }
            }

            if (propertyMap.Count == 0)
            {
                return IndexSchemaPropertyCollectionResult.Empty();
            }

            var properties = propertyMap.Values
                .OrderBy(static property => property.Path ?? string.Empty, StringComparer.Ordinal)
                .ToArray();
            return new IndexSchemaPropertyCollectionResult(properties, referencedTypes);
        }
    }
}