using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents persisted daemon <c>daemon-diagnosis.json</c> contract fields. </summary>
/// <param name="Reason"> The normalized daemon diagnosis reason value. </param>
/// <param name="Message"> The human-readable daemon diagnosis message. </param>
/// <param name="ReportedBy"> The normalized subject that reported this daemon diagnosis. </param>
/// <param name="IsInferred"> <see langword="true" /> when CLI inferred the diagnosis after observation; otherwise <see langword="false" />. </param>
/// <param name="UpdatedAtUtc"> The UTC timestamp when the diagnosis was recorded. </param>
/// <param name="ProcessId"> The daemon process identifier when available. </param>
/// <param name="EditorInstancePath"> The Unity <c>Library/EditorInstance.json</c> path associated with the diagnosis when available. </param>
/// <param name="SessionIssuedAtUtc"> The daemon session issuance timestamp associated with the diagnosis. </param>
/// <param name="ProcessStartedAtUtc"> The daemon process start timestamp associated with the diagnosis when available. </param>
/// <param name="UnityLogPath"> The Unity log path associated with the diagnosis when available. </param>
/// <param name="StartupPhase"> The normalized startup phase associated with the diagnosis when available. </param>
/// <param name="ActionRequired"> The normalized user action required to resolve the diagnosis when available. </param>
/// <param name="PrimaryDiagnostic"> The primary machine-readable diagnostic associated with this diagnosis when available. </param>
internal sealed record DaemonDiagnosisJsonContract
{
    /// <summary> Initializes persisted daemon diagnosis fields. </summary>
    [JsonConstructor]
    public DaemonDiagnosisJsonContract (
        DaemonDiagnosisReason? Reason,
        string? Message,
        DaemonDiagnosisReportedBy? ReportedBy,
        bool? IsInferred,
        DateTimeOffset UpdatedAtUtc,
        int? ProcessId,
        string? EditorInstancePath,
        DateTimeOffset SessionIssuedAtUtc,
        DateTimeOffset? ProcessStartedAtUtc,
        string? UnityLogPath,
        DaemonDiagnosisStartupPhase? StartupPhase,
        DaemonDiagnosisActionRequired? ActionRequired,
        DaemonDiagnosisPrimaryDiagnosticJsonContract? PrimaryDiagnostic)
    {
        if (Reason.HasValue && !TextVocabulary.IsDefined(Reason.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(Reason), Reason, "Unsupported daemon diagnosis reason.");
        }

        if (ReportedBy.HasValue && !TextVocabulary.IsDefined(ReportedBy.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(ReportedBy), ReportedBy, "Unsupported daemon diagnosis reporter.");
        }

        if (StartupPhase.HasValue && !TextVocabulary.IsDefined(StartupPhase.Value))
        {
            throw new ArgumentOutOfRangeException(nameof(StartupPhase), StartupPhase, "Unsupported daemon diagnosis startup phase.");
        }

        if (ActionRequired.HasValue && !TextVocabulary.IsDefined(ActionRequired.Value))
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

    [JsonInclude]
    [JsonRequired]
    public DaemonDiagnosisReason? Reason { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public string? Message { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public DaemonDiagnosisReportedBy? ReportedBy { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public bool? IsInferred { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset UpdatedAtUtc { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public int? ProcessId { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public string? EditorInstancePath { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset SessionIssuedAtUtc { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public DateTimeOffset? ProcessStartedAtUtc { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public string? UnityLogPath { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public DaemonDiagnosisStartupPhase? StartupPhase { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public DaemonDiagnosisActionRequired? ActionRequired { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public DaemonDiagnosisPrimaryDiagnosticJsonContract? PrimaryDiagnostic { get; private init; }
}
