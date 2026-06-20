using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Unity;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary> Resolves executeMethod runner entrypoints from loaded Unity editor assemblies. </summary>
    internal sealed class BuildExecuteMethodResolver
    {
        /// <summary> Resolves one runner method by uCLI runner.method identity. </summary>
        public BuildExecuteMethodResolutionResult Resolve (string methodName)
        {
            if (string.IsNullOrWhiteSpace(methodName))
            {
                return BuildExecuteMethodResolutionResult.Failure(
                    BuildErrorCodes.BuildExecuteMethodNotFound,
                    "Build executeMethod runner.method must not be empty.");
            }

            if (methodName.IndexOf(',') >= 0)
            {
                return BuildExecuteMethodResolutionResult.Failure(
                    BuildErrorCodes.BuildExecuteMethodNotFound,
                    "Build executeMethod runner.method must not contain an assembly-qualified type name.");
            }

            var separatorIndex = methodName.LastIndexOf('.');
            if (separatorIndex <= 0 || separatorIndex == methodName.Length - 1)
            {
                return BuildExecuteMethodResolutionResult.Failure(
                    BuildErrorCodes.BuildExecuteMethodNotFound,
                    "Build executeMethod runner.method must be Namespace.Type.Method or Type.Method.");
            }

            var typeName = methodName.Substring(0, separatorIndex);
            var methodShortName = methodName.Substring(separatorIndex + 1);
            var types = ResolveTypes(typeName);
            if (types.Count == 0)
            {
                return BuildExecuteMethodResolutionResult.Failure(
                    BuildErrorCodes.BuildExecuteMethodNotFound,
                    $"Build executeMethod runner type was not found: {typeName}.");
            }

            if (types.Count > 1)
            {
                return BuildExecuteMethodResolutionResult.Failure(
                    BuildErrorCodes.BuildExecuteMethodAmbiguous,
                    $"Build executeMethod runner type is ambiguous: {typeName}.");
            }

            var methods = types[0]
                .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance)
                .Where(method => string.Equals(method.Name, methodShortName, StringComparison.Ordinal))
                .ToArray();
            if (methods.Length == 0)
            {
                return BuildExecuteMethodResolutionResult.Failure(
                    BuildErrorCodes.BuildExecuteMethodNotFound,
                    $"Build executeMethod runner method was not found: {methodName}.");
            }

            if (methods.Length > 1)
            {
                return BuildExecuteMethodResolutionResult.Failure(
                    BuildErrorCodes.BuildExecuteMethodAmbiguous,
                    $"Build executeMethod runner method is ambiguous: {methodName}.");
            }

            var method = methods[0];
            if (!method.IsStatic)
            {
                return BuildExecuteMethodResolutionResult.Failure(
                    BuildErrorCodes.BuildExecuteMethodNotStatic,
                    $"Build executeMethod runner method must be static: {methodName}.");
            }

            if (!HasSupportedVisibility(method) || method.IsGenericMethodDefinition || !HasSupportedSignature(method))
            {
                return BuildExecuteMethodResolutionResult.Failure(
                    BuildErrorCodes.BuildExecuteMethodUnsupportedSignature,
                    $"Build executeMethod runner method has an unsupported signature: {methodName}.");
            }

            return BuildExecuteMethodResolutionResult.Success(method);
        }

        private static IReadOnlyList<Type> ResolveTypes (string typeName)
        {
            var types = new List<Type>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                foreach (var type in GetLoadableTypes(assemblies[i]))
                {
                    if (string.Equals(type.FullName, typeName, StringComparison.Ordinal)
                        || string.Equals(type.Name, typeName, StringComparison.Ordinal))
                    {
                        types.Add(type);
                    }
                }
            }

            return types;
        }

        private static IReadOnlyList<Type> GetLoadableTypes (Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(static type => type != null).Cast<Type>().ToArray();
            }
        }

        private static bool HasSupportedVisibility (MethodInfo method)
        {
            return method.IsPublic || method.IsAssembly;
        }

        private static bool HasSupportedSignature (MethodInfo method)
        {
            if (method.ReturnType != typeof(UcliBuildRunnerResult))
            {
                return false;
            }

            var parameters = method.GetParameters();
            return parameters.Length == 0
                || (parameters.Length == 1 && parameters[0].ParameterType == typeof(UcliBuildRunnerContext));
        }
    }
}
