using System.Linq;
using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Validates eval entry point signatures from Roslyn symbols without executing user code. </summary>
    internal sealed class CsEvalEntryPointSymbolValidator
    {
        public bool TryResolve (
            CSharpCompilation compilation,
            out CsEvalEntryPointName entryPoint,
            out IReadOnlyList<CsEvalDiagnostic> diagnostics)
        {
            entryPoint = default;

            var contextSymbol = compilation.GetTypeByMetadataName(typeof(UcliCsEvalContext).FullName!);
            if (contextSymbol == null)
            {
                diagnostics = new[]
                {
                    CsEvalDiagnosticMapper.Create(
                        CsEvalDiagnosticIds.EntryPointContextUnavailable,
                        $"Context type '{typeof(UcliCsEvalContext).FullName}' was not available to the compilation."),
                };
                return false;
            }

            var candidates = EnumerateTypes(compilation.Assembly.GlobalNamespace)
                .SelectMany(static type => type.GetMembers(CsEvalEntryPointName.RequiredMethodName).OfType<IMethodSymbol>())
                .ToArray();
            var matches = candidates
                .Where(method => IsCandidate(method, contextSymbol))
                .ToArray();
            if (matches.Length == 0)
            {
                diagnostics = candidates.Length == 0
                    ? new[]
                    {
                        CsEvalDiagnosticMapper.Create(
                            CsEvalDiagnosticIds.EntryPointMissing,
                            $"C# eval source must contain exactly one {CsEvalEntryPointName.RequiredSignature} entry point."),
                    }
                    : candidates
                        .Select(method => CsEvalDiagnosticMapper.Create(
                            CsEvalDiagnosticIds.EntryPointRejected,
                            $"C# eval Run candidate '{CreateDisplayName(method)}' was rejected: {CreateRejectionReason(method, contextSymbol)}."))
                        .ToArray();
                return false;
            }

            if (matches.Length > 1)
            {
                var candidateNames = string.Join(", ", matches.Select(CreateDisplayName));
                diagnostics = new[]
                {
                    CsEvalDiagnosticMapper.Create(
                        CsEvalDiagnosticIds.EntryPointAmbiguous,
                        $"C# eval source contains multiple matching entry points: {candidateNames}."),
                };
                return false;
            }

            var method = matches[0];
            entryPoint = new CsEvalEntryPointName(
                CreateDisplayName(method),
                CreateReflectionTypeName(method.ContainingType),
                method.Name);
            diagnostics = System.Array.Empty<CsEvalDiagnostic>();
            return true;
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateTypes (INamespaceSymbol namespaceSymbol)
        {
            foreach (var type in namespaceSymbol.GetTypeMembers())
            {
                foreach (var nestedType in EnumerateTypes(type))
                {
                    yield return nestedType;
                }
            }

            foreach (var childNamespace in namespaceSymbol.GetNamespaceMembers())
            {
                foreach (var type in EnumerateTypes(childNamespace))
                {
                    yield return type;
                }
            }
        }

        private static IEnumerable<INamedTypeSymbol> EnumerateTypes (INamedTypeSymbol typeSymbol)
        {
            yield return typeSymbol;

            foreach (var nestedType in typeSymbol.GetTypeMembers())
            {
                foreach (var descendant in EnumerateTypes(nestedType))
                {
                    yield return descendant;
                }
            }
        }

        private static bool IsCandidate (
            IMethodSymbol method,
            ITypeSymbol contextSymbol)
        {
            return method.DeclaredAccessibility == Accessibility.Public
                && method.IsStatic
                && !method.IsGenericMethod
                && !method.ContainingType.IsGenericType
                && !method.IsAsync
                && !IsTaskLike(method.ReturnType)
                && method.ReturnType.SpecialType == SpecialType.System_Object
                && method.Parameters.Length == 1
                && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextSymbol);
        }

        private static string CreateRejectionReason (
            IMethodSymbol method,
            ITypeSymbol contextSymbol)
        {
            var reasons = new List<string>();
            if (method.DeclaredAccessibility != Accessibility.Public)
            {
                reasons.Add("method is not public");
            }

            if (!method.IsStatic)
            {
                reasons.Add("method is not static");
            }

            if (method.IsGenericMethod)
            {
                reasons.Add("method is generic");
            }

            if (method.ContainingType.IsGenericType)
            {
                reasons.Add("containing type is generic");
            }

            if (method.IsAsync)
            {
                reasons.Add("method is async");
            }

            if (IsTaskLike(method.ReturnType))
            {
                reasons.Add("return type is Task or ValueTask");
            }
            else if (method.ReturnType.SpecialType != SpecialType.System_Object)
            {
                reasons.Add($"return type is '{method.ReturnType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}', expected object");
            }

            if (method.Parameters.Length != 1)
            {
                reasons.Add($"parameter count is {method.Parameters.Length}, expected 1");
            }
            else if (!SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, contextSymbol))
            {
                reasons.Add($"parameter type is '{method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}', expected {typeof(UcliCsEvalContext).FullName}");
            }

            return string.Join("; ", reasons);
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

        private static string CreateDisplayName (IMethodSymbol method)
        {
            return method.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) + "." + method.Name;
        }

        private static string CreateReflectionTypeName (INamedTypeSymbol typeSymbol)
        {
            var typeName = CreateNestedTypeName(typeSymbol);
            if (typeSymbol.ContainingNamespace.IsGlobalNamespace)
            {
                return typeName;
            }

            return typeSymbol.ContainingNamespace.ToDisplayString() + "." + typeName;
        }

        private static string CreateNestedTypeName (INamedTypeSymbol typeSymbol)
        {
            if (typeSymbol.ContainingType == null)
            {
                return typeSymbol.MetadataName;
            }

            return CreateNestedTypeName(typeSymbol.ContainingType) + "+" + typeSymbol.MetadataName;
        }
    }
}
