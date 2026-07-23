using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

/// <summary> Represents one structured daemon termination diagnosis associated with one daemon lifecycle. </summary>
/// <param name="Reason"> The normalized daemon diagnosis reason value. </param>
/// <param name="Message"> The human-readable daemon diagnosis message. </param>
/// <param name="ReportedBy"> The normalized subject that reported this daemon diagnosis. </param>
/// <param name="IsInferred"> <see langword="true" /> when CLI inferred the diagnosis after observation; otherwise <see langword="false" />. </param>
/// <param name="UpdatedAtUtc"> The UTC timestamp when the diagnosis was recorded or derived. </param>
/// <param name="ProcessId"> The daemon process identifier when available. </param>
/// <param name="EditorInstancePath"> The Unity <c>Library/EditorInstance.json</c> path associated with the diagnosis when available. </param>
/// <param name="SessionIssuedAtUtc"> The daemon session issuance timestamp associated with the diagnosis. </param>
/// <param name="ProcessStartedAtUtc"> The daemon process start timestamp associated with the diagnosis when available. </param>
/// <param name="UnityLogPath"> The Unity log path associated with the diagnosis when available. </param>
/// <param name="StartupPhase"> The normalized startup phase associated with the diagnosis when available. </param>
/// <param name="ActionRequired"> The normalized user action required to resolve the diagnosis when available. </param>
/// <param name="PrimaryDiagnostic"> The primary machine-readable diagnostic associated with this diagnosis when available. </param>
internal sealed record DaemonDiagnosis
{
    public DaemonDiagnosis (
        DaemonDiagnosisReason Reason,
        string Message,
        DaemonDiagnosisReportedBy ReportedBy,
        bool IsInferred,
        DateTimeOffset UpdatedAtUtc,
        int? ProcessId,
        AbsolutePath? EditorInstancePath,
        DateTimeOffset SessionIssuedAtUtc,
        DateTimeOffset? ProcessStartedAtUtc,
        AbsolutePath? UnityLogPath,
        DaemonDiagnosisStartupPhase? StartupPhase,
        DaemonDiagnosisActionRequired? ActionRequired,
        DaemonPrimaryDiagnostic? PrimaryDiagnostic)
    {
        if (!ContractLiteralCodec.IsDefined(Reason))
        {
            throw new ArgumentOutOfRangeException(nameof(Reason), Reason, "Unsupported daemon diagnosis reason.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(Message);

        if (!ContractLiteralCodec.IsDefined(ReportedBy))
        {
            throw new ArgumentOutOfRangeException(nameof(ReportedBy), ReportedBy, "Unsupported daemon diagnosis reporter.");
        }

        if (UpdatedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(UpdatedAtUtc), UpdatedAtUtc, "Diagnosis update time must be specified.");
        }

        if (SessionIssuedAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(nameof(SessionIssuedAtUtc), SessionIssuedAtUtc, "Session issue time must be specified.");
        }

        if (StartupPhase.HasValue && !ContractLiteralCodec.IsDefined(StartupPhase.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(StartupPhase), StartupPhase, "Unsupported daemon diagnosis startup phase.");
        }

        if (ActionRequired.HasValue && !ContractLiteralCodec.IsDefined(ActionRequired.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(ActionRequired), ActionRequired, "Unsupported daemon diagnosis action.");
        }

        this.Reason = Reason;
        this.Message = Message;
        this.ReportedBy = ReportedBy;
        this.IsInferred = IsInferred;
        this.UpdatedAtUtc = UpdatedAtUtc;
        this.ProcessId = ProcessId;
        this.EditorInstancePath = EditorInstancePath;
        this.SessionIssuedAtUtc = SessionIssuedAtUtc;
        this.ProcessStartedAtUtc = ProcessStartedAtUtc;
        this.UnityLogPath = UnityLogPath;
        this.StartupPhase = StartupPhase;
        this.ActionRequired = ActionRequired;
        this.PrimaryDiagnostic = PrimaryDiagnostic;
    }

    public DaemonDiagnosisReason Reason { get; }

    public string Message { get; }

    public DaemonDiagnosisReportedBy ReportedBy { get; }

    public bool IsInferred { get; }

    public DateTimeOffset UpdatedAtUtc { get; }

    public int? ProcessId { get; }

    public AbsolutePath? EditorInstancePath { get; }

    public DateTimeOffset SessionIssuedAtUtc { get; }

    public DateTimeOffset? ProcessStartedAtUtc { get; }

    public AbsolutePath? UnityLogPath { get; }

    public DaemonDiagnosisStartupPhase? StartupPhase { get; }

    public DaemonDiagnosisActionRequired? ActionRequired { get; }

    public DaemonPrimaryDiagnostic? PrimaryDiagnostic { get; }
}
