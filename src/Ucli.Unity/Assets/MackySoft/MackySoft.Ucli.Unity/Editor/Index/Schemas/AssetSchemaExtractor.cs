using System;
using MackySoft.Ucli.Contracts.Index;
using UnityEditor;
using UnityEngine;

#nullable enable

using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Extracts schema entries for ScriptableObject runtime types. </summary>
    internal sealed class AssetSchemaExtractor
    {
        private readonly IndexSchemaPropertyCollector schemaPropertyCollector = new IndexSchemaPropertyCollector();

        /// <summary> Extracts the schema entry for one validated ScriptableObject runtime type. </summary>
        /// <param name="assetType"> The validated asset runtime type. </param>
        /// <returns> The extracted schema entry. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="assetType" /> is <see langword="null" />. </exception>
        public IndexSchemaEntryJsonContract Extract (Type assetType)
        {
            if (assetType == null)
            {
                throw new ArgumentNullException(nameof(assetType));
            }

            ScriptableObject? instance = null;
            try
            {
                instance = ScriptableObject.CreateInstance(assetType);
                if (instance == null)
                {
                    throw new InvalidOperationException($"Failed to create ScriptableObject instance. type={assetType.FullName}");
                }

                var serializedObject = new SerializedObject(instance);
                var properties = schemaPropertyCollector.Collect(assetType, serializedObject);
                var kind = ContractLiteralCodec.ToValue(IndexSchemaKind.Asset);
                var typeId = IndexTypeIdFormatter.Format(assetType);
                return new IndexSchemaEntryJsonContract(
                    SchemaKey: $"{kind}:{typeId}",
                    Kind: kind,
                    TypeId: typeId,
                    DisplayName: assetType.Name,
                    Properties: properties);
            }
            finally
            {
                if (instance != null)
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }
        }
    }
}
