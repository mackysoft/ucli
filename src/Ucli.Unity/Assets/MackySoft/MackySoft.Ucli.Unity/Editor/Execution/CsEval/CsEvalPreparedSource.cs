using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.CsEval
{
    /// <summary> Holds source text after eval source form normalization. </summary>
    internal sealed class CsEvalPreparedSource
    {
        public CsEvalPreparedSource (
            CsEvalSourceKind sourceKind,
            string compilationSource,
            string wrapperVersion)
        {
            SourceKind = sourceKind;
            CompilationSource = compilationSource ?? throw new ArgumentNullException(nameof(compilationSource));
            WrapperVersion = string.IsNullOrWhiteSpace(wrapperVersion)
                ? throw new ArgumentException("Wrapper version must not be empty.", nameof(wrapperVersion))
                : wrapperVersion;
        }

        public CsEvalSourceKind SourceKind { get; }

        public string CompilationSource { get; }

        public string WrapperVersion { get; }
    }
}
