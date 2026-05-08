using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Resolves validated eval entry points from emitted in-memory assemblies. </summary>
    internal sealed class CsEvalEntryPointReflectionResolver
    {
        public bool TryResolve (
            Assembly assembly,
            CsEvalEntryPointName entryPoint,
            out MethodInfo method,
            out string errorMessage)
        {
            method = null!;
            var type = assembly.GetType(entryPoint.ReflectionTypeName, throwOnError: false, ignoreCase: false);
            if (type == null)
            {
                errorMessage = $"Resolved entry point type '{entryPoint.ReflectionTypeName}' was not found.";
                return false;
            }

            var matches = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(candidate => string.Equals(candidate.Name, entryPoint.MethodName, StringComparison.Ordinal))
                .Where(IsCandidate)
                .ToArray();
            if (matches.Length == 0)
            {
                errorMessage = $"Resolved entry point '{entryPoint.DisplayName}' was not found in the emitted assembly.";
                return false;
            }

            if (matches.Length > 1)
            {
                errorMessage = $"Resolved entry point '{entryPoint.DisplayName}' is ambiguous in the emitted assembly.";
                return false;
            }

            method = matches[0];
            errorMessage = string.Empty;
            return true;
        }

        private static bool IsCandidate (MethodInfo method)
        {
            var parameters = method.GetParameters();
            return !method.IsGenericMethod
                && method.GetCustomAttribute<AsyncStateMachineAttribute>() == null
                && !IsTaskLike(method.ReturnType)
                && method.ReturnType == typeof(object)
                && parameters.Length == 1
                && parameters[0].ParameterType == typeof(UcliCsEvalContext);
        }

        private static bool IsTaskLike (Type returnType)
        {
            return returnType == typeof(Task)
                || returnType == typeof(ValueTask)
                || (returnType.IsGenericType
                    && (returnType.GetGenericTypeDefinition() == typeof(Task<>)
                        || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)));
        }
    }
}
