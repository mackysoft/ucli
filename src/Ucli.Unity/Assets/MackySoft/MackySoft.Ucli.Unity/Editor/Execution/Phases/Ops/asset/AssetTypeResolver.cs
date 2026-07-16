using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves runtime types used by asset-domain operations. </summary>
    internal static class AssetTypeResolver
    {
        /// <summary> Resolves one <c>typeId</c> to a concrete <see cref="ScriptableObject" /> type. </summary>
        /// <param name="typeId"> The stable type identifier. </param>
        /// <param name="assetType"> The resolved asset type when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when a concrete scriptable-object type is resolved; otherwise <see langword="false" />. </returns>
        public static bool TryResolveCreateAssetType (
            string? typeId,
            [NotNullWhen(true)] out Type? assetType,
            out string errorMessage)
        {
            assetType = null;
            if (!OperationRuntimeTypeResolver.TryResolveRuntimeType(typeId, out var runtimeType, out errorMessage))
            {
                return false;
            }

            if (!typeof(ScriptableObject).IsAssignableFrom(runtimeType))
            {
                errorMessage = $"TypeId must resolve to a ScriptableObject type: {typeId}.";
                return false;
            }

            if (!OperationRuntimeTypeResolver.IsConcreteRuntimeType(runtimeType))
            {
                errorMessage = $"TypeId must resolve to a concrete ScriptableObject type: {typeId}.";
                return false;
            }

            assetType = runtimeType;
            errorMessage = string.Empty;
            return true;
        }
    }
}
