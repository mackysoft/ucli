using System;

#nullable enable

namespace MackySoft.Ucli.Unity.Index
{
    /// <summary> Provides shared type-classification predicates for index catalog generation. </summary>
    internal static class IndexTypeClassification
    {
        /// <summary> Returns whether one type can be represented by type catalog entries. </summary>
        /// <param name="type"> The target type. </param>
        /// <returns> <see langword="true" /> when the type is valid for catalog representation. </returns>
        public static bool IsCatalogType (Type type)
        {
            return type != null
                && !type.IsPointer
                && !type.IsByRef
                && !type.ContainsGenericParameters;
        }

        /// <summary> Returns whether one type can be referenced by Unity serialize-reference fields. </summary>
        /// <param name="type"> The target type. </param>
        /// <returns> <see langword="true" /> when the type is a serialize-reference candidate. </returns>
        public static bool IsSerializeReferenceCandidate (Type type)
        {
            return type != null
                && type.IsClass
                && !type.IsAbstract
                && !type.IsGenericTypeDefinition
                && !type.ContainsGenericParameters
                && type.IsDefined(typeof(SerializableAttribute), inherit: false)
                && !typeof(UnityEngine.Object).IsAssignableFrom(type);
        }
    }
}
