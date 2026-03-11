using System;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves runtime types from component-domain <c>typeId</c> values. </summary>
    internal static class ComponentTypeResolver
    {
        /// <summary> Resolves one <c>typeId</c> to a concrete component runtime type. </summary>
        /// <param name="typeId"> The stable type identifier. </param>
        /// <param name="componentType"> The resolved component type when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when a concrete <see cref="Component" /> type is resolved; otherwise <see langword="false" />. </returns>
        public static bool TryResolveComponentType (
            string typeId,
            out Type? componentType,
            out string errorMessage)
        {
            componentType = null;
            if (!OperationRuntimeTypeResolver.TryResolveRuntimeType(typeId, out var runtimeType, out errorMessage))
            {
                return false;
            }

            if (!typeof(Component).IsAssignableFrom(runtimeType))
            {
                errorMessage = $"TypeId must resolve to a Unity Component type: {typeId}.";
                return false;
            }

            if (!OperationRuntimeTypeResolver.IsConcreteRuntimeType(runtimeType))
            {
                errorMessage = $"TypeId must resolve to a concrete component type: {typeId}.";
                return false;
            }

            componentType = runtimeType;
            errorMessage = string.Empty;
            return true;
        }

    }
}