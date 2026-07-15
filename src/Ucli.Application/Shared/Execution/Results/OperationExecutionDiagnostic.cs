using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.Results;

/// <summary> Represents one non-fatal diagnostic attached to a public operation result. </summary>
/// <param name="Code"> The stable diagnostic code. </param>
/// <param name="Severity"> The diagnostic severity. </param>
/// <param name="CoverageImpact"> The diagnostic coverage impact. </param>
/// <param name="Message"> The human-readable diagnostic message. </param>
internal sealed record OperationExecutionDiagnostic
{
    public OperationExecutionDiagnostic (
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
