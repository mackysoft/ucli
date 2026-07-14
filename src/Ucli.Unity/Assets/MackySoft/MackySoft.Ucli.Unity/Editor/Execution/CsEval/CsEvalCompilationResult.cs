using System;
using Microsoft.CodeAnalysis.CSharp;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Holds C# eval compilation and entry point validation output. </summary>
    internal sealed class CsEvalCompilationResult
    {
        public CsEvalCompilationResult (
            Sha256Digest sourceDigest,
            string? sourceKind,
            string? resolvedEntryPoint,
            CsEvalEntryPointName? entryPointName,
            Sha256Digest executionDigest,
            CsEvalCompileResult compile,
            CSharpCompilation compilation,
            bool isSuccess,
            string? failureMessage)
        {
            SourceDigest = sourceDigest ?? throw new ArgumentNullException(nameof(sourceDigest));
            SourceKind = sourceKind;
            ResolvedEntryPoint = resolvedEntryPoint;
            EntryPointName = entryPointName;
            ExecutionDigest = executionDigest ?? throw new ArgumentNullException(nameof(executionDigest));
            Compile = compile;
            Compilation = compilation;
            IsSuccess = isSuccess;
            FailureMessage = failureMessage;
        }

        public Sha256Digest SourceDigest { get; }

        public string? SourceKind { get; }

        public string? ResolvedEntryPoint { get; }

        public CsEvalEntryPointName? EntryPointName { get; }

        public Sha256Digest ExecutionDigest { get; }

        public CsEvalCompileResult Compile { get; }

        public CSharpCompilation Compilation { get; }

        public bool IsSuccess { get; }

        public string? FailureMessage { get; }

        public CsEvalResult CreatePlanResult ()
        {
            return new CsEvalResult(
                SourceDigest,
                SourceKind,
                ResolvedEntryPoint,
                ExecutionDigest,
                Compile,
                durationMilliseconds: null,
                logs: null,
                returnValue: null,
                touchedResources: null);
        }
    }
}
