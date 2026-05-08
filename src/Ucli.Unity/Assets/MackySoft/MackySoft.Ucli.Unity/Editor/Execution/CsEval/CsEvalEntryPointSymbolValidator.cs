using System.Linq;
using MackySoft.Ucli.Contracts.Ipc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Validates eval entry point signatures from Roslyn symbols without executing user code. </summary>
    internal sealed class CsEvalEntryPointSymbolValidator
    {
        public bool TryValidate (
            CSharpCompilation compilation,
            string entryPoint,
            out CsEvalDiagnostic diagnostic)
        {
            if (!CsEvalEntryPointName.TryParse(entryPoint, out var entryPointName, out var entryPointError))
            {
                diagnostic = CsEvalDiagnosticMapper.EntryPointError(entryPointError);
                return false;
            }

            var typeSymbol = compilation.GetTypeByMetadataName(entryPointName.TypeName);
            if (typeSymbol == null)
            {
                diagnostic = CsEvalDiagnosticMapper.EntryPointError($"Entry point type '{entryPointName.TypeName}' was not found.");
                return false;
            }

            var contextSymbol = compilation.GetTypeByMetadataName(typeof(UcliCsEvalContext).FullName!);
            if (contextSymbol == null)
            {
                diagnostic = CsEvalDiagnosticMapper.EntryPointError($"Context type '{typeof(UcliCsEvalContext).FullName}' was not available to the compilation.");
                return false;
            }

            var matches = typeSymbol.GetMembers(entryPointName.MethodName)
                .OfType<IMethodSymbol>()
                .Where(method => IsCandidate(method, contextSymbol))
                .ToArray();
            if (matches.Length == 0)
            {
                diagnostic = CsEvalDiagnosticMapper.EntryPointError(
                    $"Entry point '{entryPoint}' must be a public static object? Run method with one {typeof(UcliCsEvalContext).FullName} parameter.");
                return false;
            }

            if (matches.Length > 1)
            {
                diagnostic = CsEvalDiagnosticMapper.EntryPointError($"Entry point '{entryPoint}' is ambiguous.");
                return false;
            }

            diagnostic = null!;
            return true;
        }

        private static bool IsCandidate (
            IMethodSymbol method,
            ITypeSymbol contextSymbol)
        {
            return method.DeclaredAccessibility == Accessibility.Public
                && method.IsStatic
                && !method.IsGenericMethod
                && !method.IsAsync
                && !IsTaskLike(method.ReturnType)
                && method.ReturnType.SpecialType == SpecialType.System_Object
                && method.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextSymbol);
        }

        private static bool IsTaskLike (ITypeSymbol returnType)
        {
            if (returnType is not INamedTypeSymbol namedType)
            {
                return false;
            }

            var fullName = namedType.OriginalDefinition.ToDisplayString();
            return fullName == "System.Threading.Tasks.Task"
                || fullName == "System.Threading.Tasks.Task<TResult>"
                || fullName == "System.Threading.Tasks.ValueTask"
                || fullName == "System.Threading.Tasks.ValueTask<TResult>";
        }
    }
}
