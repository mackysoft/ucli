using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Compiles and validates <c>ucli.cs.eval</c> source without invoking user code. </summary>
    internal sealed class CsEvalCompilationService
    {
        private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
            .WithKind(SourceCodeKind.Regular);

        private static readonly CSharpCompilationOptions CompilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            optimizationLevel: OptimizationLevel.Release,
            allowUnsafe: false);

        private readonly CsEvalReferenceResolver referenceResolver;

        private readonly CsEvalEntryPointSymbolValidator entryPointValidator;

        public CsEvalCompilationService ()
            : this(new CsEvalReferenceResolver(), new CsEvalEntryPointSymbolValidator())
        {
        }

        public CsEvalCompilationService (
            CsEvalReferenceResolver referenceResolver,
            CsEvalEntryPointSymbolValidator entryPointValidator)
        {
            this.referenceResolver = referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver));
            this.entryPointValidator = entryPointValidator ?? throw new ArgumentNullException(nameof(entryPointValidator));
        }

        public CsEvalCompilationResult CompileAndValidate (
            string source,
            string entryPoint,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourceDigest = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(source));
            var references = referenceResolver.Resolve();
            var executionDigest = CsEvalExecutionDigestCalculator.Compute(sourceDigest, entryPoint, references.Identity);
            var syntaxTree = CSharpSyntaxTree.ParseText(
                SourceText.From(source, Encoding.UTF8),
                ParseOptions,
                path: "ucli.cs.eval.cs",
                cancellationToken: cancellationToken);
            var compilation = CSharpCompilation.Create(
                "UcliCsEval_" + sourceDigest.Substring(0, 12),
                new[] { syntaxTree },
                references.References,
                CompilationOptions);
            var diagnostics = compilation.GetDiagnostics(cancellationToken)
                .Select(CsEvalDiagnosticMapper.Map)
                .ToList();

            if (diagnostics.Any(static diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.Ordinal)))
            {
                return CreateFailure(
                    sourceDigest,
                    entryPoint,
                    executionDigest,
                    compilation,
                    diagnostics,
                    "C# eval source failed to compile.");
            }

            if (!entryPointValidator.TryValidate(compilation, entryPoint, out var entryPointDiagnostic))
            {
                diagnostics.Add(entryPointDiagnostic);
                return CreateFailure(
                    sourceDigest,
                    entryPoint,
                    executionDigest,
                    compilation,
                    diagnostics,
                    entryPointDiagnostic.Message);
            }

            return new CsEvalCompilationResult(
                sourceDigest,
                entryPoint,
                executionDigest,
                new CsEvalCompileResult(CsEvalCompileStatusValues.Succeeded, diagnostics),
                compilation,
                isSuccess: true,
                failureMessage: null);
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
            diagnostics = emitResult.Diagnostics.Select(CsEvalDiagnosticMapper.Map).ToArray();
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
            string sourceDigest,
            string entryPoint,
            string executionDigest,
            CSharpCompilation compilation,
            IReadOnlyList<CsEvalDiagnostic> diagnostics,
            string failureMessage)
        {
            return new CsEvalCompilationResult(
                sourceDigest,
                entryPoint,
                executionDigest,
                new CsEvalCompileResult(CsEvalCompileStatusValues.Failed, diagnostics),
                compilation,
                isSuccess: false,
                failureMessage: failureMessage);
        }
    }
}
