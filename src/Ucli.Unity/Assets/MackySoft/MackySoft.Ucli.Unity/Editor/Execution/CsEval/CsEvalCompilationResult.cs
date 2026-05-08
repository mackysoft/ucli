using Microsoft.CodeAnalysis.CSharp;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Holds C# eval compilation and entry point validation output. </summary>
    internal sealed class CsEvalCompilationResult
    {
        public CsEvalCompilationResult (
            string sourceDigest,
            string? sourceKind,
            string? resolvedEntryPoint,
            CsEvalEntryPointName? entryPointName,
            string executionDigest,
            CsEvalCompileResult compile,
            CSharpCompilation compilation,
            bool isSuccess,
            string? failureMessage)
        {
            SourceDigest = sourceDigest;
            SourceKind = sourceKind;
            ResolvedEntryPoint = resolvedEntryPoint;
            EntryPointName = entryPointName;
            ExecutionDigest = executionDigest;
            Compile = compile;
            Compilation = compilation;
            IsSuccess = isSuccess;
            FailureMessage = failureMessage;
        }

        public string SourceDigest { get; }

        public string? SourceKind { get; }

        public string? ResolvedEntryPoint { get; }

        public CsEvalEntryPointName? EntryPointName { get; }

        public string ExecutionDigest { get; }

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
