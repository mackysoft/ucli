using System;
using MackySoft.Ucli.Contracts.Text;
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
            if (!TryResolveRuntimeType(typeId, out var runtimeType, out errorMessage))
            {
                return false;
            }

            if (!typeof(Component).IsAssignableFrom(runtimeType))
            {
                errorMessage = $"TypeId must resolve to a Unity Component type: {typeId}.";
                return false;
            }

            if (!IsConcreteRuntimeType(runtimeType))
            {
                errorMessage = $"TypeId must resolve to a concrete component type: {typeId}.";
                return false;
            }

            componentType = runtimeType;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary> Resolves one <c>typeId</c> to a loadable runtime type. </summary>
        /// <param name="typeId"> The stable type identifier. </param>
        /// <param name="runtimeType"> The resolved runtime type when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when a runtime type is resolved; otherwise <see langword="false" />. </returns>
        public static bool TryResolveRuntimeType (
            string typeId,
            out Type? runtimeType,
            out string errorMessage)
        {
            runtimeType = null;
            if (string.IsNullOrWhiteSpace(typeId))
            {
                errorMessage = "TypeId must not be empty or whitespace.";
                return false;
            }

            if (StringValueValidator.HasOuterWhitespace(typeId))
            {
                errorMessage = "TypeId must not contain leading or trailing whitespace.";
                return false;
            }

            runtimeType = Type.GetType(typeId, throwOnError: false);
            if (runtimeType == null && TrySplitTypeId(typeId, out var typeName, out var assemblyName))
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (var i = 0; i < assemblies.Length; i++)
                {
                    var assembly = assemblies[i];
                    var loadedAssemblyName = assembly.GetName().Name;
                    if (!string.Equals(loadedAssemblyName, assemblyName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    runtimeType = assembly.GetType(typeName, throwOnError: false, ignoreCase: false);
                    if (runtimeType != null)
                    {
                        break;
                    }
                }
            }

            if (runtimeType == null)
            {
                errorMessage = $"TypeId could not be resolved: {typeId}.";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool TrySplitTypeId (
            string typeId,
            out string typeName,
            out string assemblyName)
        {
            typeName = string.Empty;
            assemblyName = string.Empty;
            var separatorIndex = typeId.LastIndexOf(',');
            if (separatorIndex <= 0 || separatorIndex >= (typeId.Length - 1))
            {
                return false;
            }

            typeName = typeId.Substring(0, separatorIndex).Trim();
            assemblyName = typeId.Substring(separatorIndex + 1).Trim();
            return typeName.Length > 0 && assemblyName.Length > 0;
        }

        private static bool IsConcreteRuntimeType (Type runtimeType)
        {
            return !runtimeType.IsAbstract
                && !runtimeType.IsInterface
                && !runtimeType.IsGenericTypeDefinition
                && !runtimeType.ContainsGenericParameters;
        }
    }
}