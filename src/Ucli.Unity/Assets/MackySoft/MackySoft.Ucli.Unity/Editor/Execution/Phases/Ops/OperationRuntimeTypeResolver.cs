using System;
using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Resolves runtime types from stable <c>typeId</c> values shared by operations. </summary>
    internal static class OperationRuntimeTypeResolver
    {
        /// <summary> Resolves one <c>typeId</c> to a loadable runtime type. </summary>
        /// <param name="typeId"> The stable type identifier. </param>
        /// <param name="runtimeType"> The resolved runtime type when successful. </param>
        /// <param name="errorMessage"> The validation error message when resolution fails. </param>
        /// <returns> <see langword="true" /> when a runtime type is resolved; otherwise <see langword="false" />. </returns>
        public static bool TryResolveRuntimeType (
            string typeId,
            [NotNullWhen(true)] out Type? runtimeType,
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

        /// <summary> Returns whether the runtime type can be instantiated directly by uCLI operations. </summary>
        /// <param name="runtimeType"> The runtime type to validate. </param>
        /// <returns> <see langword="true" /> when the type is concrete and loadable; otherwise <see langword="false" />. </returns>
        public static bool IsConcreteRuntimeType (Type runtimeType)
        {
            return runtimeType != null
                && !runtimeType.IsAbstract
                && !runtimeType.IsInterface
                && !runtimeType.IsGenericTypeDefinition
                && !runtimeType.ContainsGenericParameters;
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
    }
}
