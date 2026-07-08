using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Defines supported <c>ucli.cs.eval</c> entry point return types. </summary>
    internal static class CsEvalEntryPointReturnTypePolicy
    {
        public const string SupportedReturnTypesDisplay = "object, Task, Task<T>, ValueTask, or ValueTask<T>";

        public static bool IsSupportedSymbolReturnType (
            ITypeSymbol returnType,
            Compilation compilation)
        {
            if (returnType.SpecialType == SpecialType.System_Object)
            {
                return true;
            }

            return IsSameOriginalDefinition(returnType, compilation.GetTypeByMetadataName(typeof(Task).FullName!), compilation)
                || IsSameOriginalDefinition(returnType, compilation.GetTypeByMetadataName(typeof(Task<>).FullName!), compilation)
                || IsSameOriginalDefinition(returnType, compilation.GetTypeByMetadataName(typeof(ValueTask).FullName!), compilation)
                || IsSameOriginalDefinition(returnType, compilation.GetTypeByMetadataName(typeof(ValueTask<>).FullName!), compilation);
        }

        public static bool IsSupportedReflectionReturnType (Type returnType)
        {
            return returnType == typeof(object) || IsTaskLikeReflectionType(returnType);
        }

        public static bool IsRuntimeTaskLikeValue (Type valueType)
        {
            return typeof(Task).IsAssignableFrom(valueType)
                || valueType == typeof(ValueTask)
                || (valueType.IsGenericType
                    && valueType.GetGenericTypeDefinition() == typeof(ValueTask<>));
        }

        private static bool IsSameOriginalDefinition (
            ITypeSymbol returnType,
            INamedTypeSymbol? expectedType,
            Compilation compilation)
        {
            if (returnType is not INamedTypeSymbol namedType || expectedType == null)
            {
                return false;
            }

            if (SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition.ContainingAssembly, compilation.Assembly)
                || SymbolEqualityComparer.Default.Equals(expectedType.OriginalDefinition.ContainingAssembly, compilation.Assembly))
            {
                return false;
            }

            return SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, expectedType);
        }

        private static bool IsTaskLikeReflectionType (Type returnType)
        {
            return returnType == typeof(Task)
                || returnType == typeof(ValueTask)
                || (returnType.IsGenericType
                    && (returnType.GetGenericTypeDefinition() == typeof(Task<>)
                        || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>)));
        }
    }
}
