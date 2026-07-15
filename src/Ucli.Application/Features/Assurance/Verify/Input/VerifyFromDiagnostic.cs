using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Verify.Input;

/// <summary> Represents one operation diagnostic consumed by post-read verification. </summary>
internal sealed record VerifyFromDiagnostic
{
    public VerifyFromDiagnostic (
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
