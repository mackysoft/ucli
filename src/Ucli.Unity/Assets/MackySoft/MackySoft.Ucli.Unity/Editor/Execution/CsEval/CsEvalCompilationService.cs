using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Compiles and validates dedicated C# eval source without invoking user code. </summary>
    internal sealed class CsEvalCompilationService
    {
        private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
            .WithKind(SourceCodeKind.Regular);

        private static readonly CSharpCompilationOptions CompilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            allowUnsafe: false,
            nullableContextOptions: NullableContextOptions.Annotations);

        private readonly CsEvalReferenceResolver referenceResolver;

        private readonly CsEvalEntryPointSymbolValidator entryPointValidator;

        private readonly CsEvalSourcePreparer sourcePreparer;

        public CsEvalCompilationService (
            CsEvalReferenceResolver referenceResolver,
            CsEvalEntryPointSymbolValidator entryPointValidator,
            CsEvalSourcePreparer sourcePreparer)
        {
            this.referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
            this.entryPointValidator = entryPointValidator ?? throw new ArgumentNullException(nameof(entryPointValidator));
            this.sourcePreparer = sourcePreparer ?? throw new ArgumentNullException(nameof(sourcePreparer));
        }

        public CsEvalCompilationResult CompileAndValidate (
            string source,
            CsEvalSourceKind sourceKind,
            bool allowDangerous,
            bool allowPlayMode,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceDigest = Sha256Digest.Compute(Encoding.UTF8.GetBytes(source));
            var references = referenceResolver.Resolve();

            var sourceByteCount = Encoding.UTF8.GetByteCount(source);
            CsEvalPreparedSource compilationUnitSource;
            if (sourceKind == CsEvalSourceKind.CompilationUnit)
            {
                compilationUnitSource = sourcePreparer.CreateCompilationUnit(source);
            }
            else if (!sourcePreparer.TryCreateSnippet(source, out compilationUnitSource, out var diagnostic))
            {
                // Source-form violations are expected user input errors. Return the same structured
                // compilation failure shape as syntax and entry-point failures so IPC handlers never
                // turn them into a generic internal error.
                var rejectedSource = new CsEvalPreparedSource(
                    CsEvalSourceKind.Snippet,
                    string.Empty,
                    CsEvalSourcePreparer.NoWrapperVersion);
                var executionDigest = CsEvalExecutionDigestCalculator.Compute(
                    source,
                    CsEvalSourceKind.Snippet,
                    allowDangerous,
                    allowPlayMode);
                return CreateFailure(
                    sourceDigest,
                    CsEvalSourceKind.Snippet,
                    resolvedEntryPoint: null,
                    entryPointName: null,
                    executionDigest,
                    CreateCompilation(rejectedSource, references, cancellationToken),
                    new[] { diagnostic },
                    diagnostic.Message);
            }
            if (sourceByteCount > CsEvalSafetyLimits.MaxSourceBytes)
            {
                var sizeDiagnostic = CsEvalDiagnosticMapper.Create(
                    CsEvalDiagnosticIds.SourceTooLarge,
                    $"C# eval source exceeds internal IPC safety guardrail. SourceBytes={sourceByteCount}.");
                return CreateFailure(
                    sourceDigest,
                    compilationUnitSource.SourceKind,
                    resolvedEntryPoint: null,
                    entryPointName: null,
                    CsEvalExecutionDigestCalculator.Compute(
                        source,
                        compilationUnitSource.SourceKind,
                        allowDangerous,
                        allowPlayMode),
                    CreateCompilation(compilationUnitSource, references, cancellationToken),
                    new[] { sizeDiagnostic },
                    sizeDiagnostic.Message);
            }

            return CompilePreparedSource(source, sourceDigest, compilationUnitSource, allowDangerous, allowPlayMode, references, cancellationToken);
        }

        private CsEvalCompilationResult CompilePreparedSource (
            string source,
            Sha256Digest sourceDigest,
            CsEvalPreparedSource preparedSource,
            bool allowDangerous,
            bool allowPlayMode,
            CsEvalReferenceSet references,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var executionDigest = CsEvalExecutionDigestCalculator.Compute(
                source,
                preparedSource.SourceKind,
                allowDangerous,
                allowPlayMode);
            var compilation = CreateCompilation(preparedSource, references, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var diagnostics = CsEvalDiagnosticMapper.Limit(compilation.GetDiagnostics(cancellationToken)
                    .Where(static diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
                    .Select(CsEvalDiagnosticMapper.Map))
                .ToList();

            if (diagnostics.Any(static diagnostic => diagnostic.Severity == UcliDiagnosticSeverity.Error))
            {
                return CreateFailure(
                    sourceDigest,
                    preparedSource.SourceKind,
                    resolvedEntryPoint: null,
                    entryPointName: null,
                    executionDigest,
                    compilation,
                    diagnostics,
                    "C# eval source failed to compile.");
            }

            if (!entryPointValidator.TryResolve(compilation, out var entryPointName, out var entryPointDiagnostics))
            {
                diagnostics.AddRange(entryPointDiagnostics);
                diagnostics = CsEvalDiagnosticMapper.Limit(diagnostics).ToList();
                return CreateFailure(
                    sourceDigest,
                    preparedSource.SourceKind,
                    resolvedEntryPoint: null,
                    entryPointName: null,
                    executionDigest,
                    compilation,
                    diagnostics,
                    entryPointDiagnostics.Count == 0 ? "C# eval source did not resolve an entry point." : entryPointDiagnostics[0].Message);
            }

            return new CsEvalCompilationResult(
                sourceDigest,
                preparedSource.SourceKind,
                entryPointName.DisplayName,
                entryPointName,
                executionDigest,
                new CsEvalPlanCompileResult(true, diagnostics),
                compilation,
                isSuccess: true,
                failureMessage: null);
        }

        private static CSharpCompilation CreateCompilation (
            CsEvalPreparedSource preparedSource,
            CsEvalReferenceSet references,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var syntaxTree = CSharpSyntaxTree.ParseText(
                SourceText.From(preparedSource.CompilationSource, Encoding.UTF8),
                ParseOptions,
                path: "ucli-csharp-eval.cs",
                cancellationToken: cancellationToken);
            return CSharpCompilation.Create(
                "UcliCsEval_" + Sha256Digest.Compute(Encoding.UTF8.GetBytes(preparedSource.CompilationSource)).ToString().Substring(0, 12),
                new[] { syntaxTree },
                references.References,
                CompilationOptions);
        }

        public bool TryEmitAssembly (
            CSharpCompilation compilation,
            CancellationToken cancellationToken,
            out byte[] assemblyBytes,
            out IReadOnlyList<CsEvalDiagnostic> diagnostics,
            out string errorMessage)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var stream = new System.IO.MemoryStream();
            EmitResult emitResult = compilation.Emit(stream, cancellationToken: cancellationToken);
            diagnostics = CsEvalDiagnosticMapper.Limit(emitResult.Diagnostics
                .Where(static diagnostic => diagnostic.Severity != DiagnosticSeverity.Hidden)
                .Select(CsEvalDiagnosticMapper.Map));
            if (!emitResult.Success)
            {
                assemblyBytes = Array.Empty<byte>();
                errorMessage = "C# eval source failed to emit an in-memory assembly.";
                return false;
            }

            assemblyBytes = stream.ToArray();
            errorMessage = string.Empty;
            return true;
        }

        private static CsEvalCompilationResult CreateFailure (
            Sha256Digest sourceDigest,
            CsEvalSourceKind sourceKind,
            string? resolvedEntryPoint,
            CsEvalEntryPointName? entryPointName,
            Sha256Digest executionDigest,
            CSharpCompilation compilation,
            IReadOnlyList<CsEvalDiagnostic> diagnostics,
            string failureMessage)
        {
            return new CsEvalCompilationResult(
                sourceDigest,
                sourceKind,
                resolvedEntryPoint,
                entryPointName,
                executionDigest,
                new CsEvalPlanCompileResult(false, diagnostics),
                compilation,
                isSuccess: false,
                failureMessage: failureMessage);
        }
    }
}
