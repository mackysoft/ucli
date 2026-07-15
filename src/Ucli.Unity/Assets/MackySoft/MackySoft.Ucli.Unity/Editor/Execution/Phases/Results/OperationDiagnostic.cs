using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one non-fatal diagnostic emitted by Unity-side operation execution. </summary>
    /// <param name="Code"> The stable diagnostic code. </param>
    /// <param name="Severity"> The diagnostic severity. </param>
    /// <param name="CoverageImpact"> The diagnostic coverage impact. </param>
    /// <param name="Message"> The human-readable diagnostic message. </param>
    public sealed record OperationDiagnostic
    {
        public OperationDiagnostic (
            UcliCode Code,
            UcliDiagnosticSeverity Severity,
            IpcExecuteDiagnosticCoverageImpact CoverageImpact,
            string Message)
        {
            this.Code = Code ?? throw new ArgumentNullException(nameof(Code));
            this.Severity = Severity;
            this.CoverageImpact = CoverageImpact;
            this.Message = Message;
        }

        public UcliCode Code { get; }

        public UcliDiagnosticSeverity Severity { get; }

        public IpcExecuteDiagnosticCoverageImpact CoverageImpact { get; }

        public string Message { get; }
    }
}
