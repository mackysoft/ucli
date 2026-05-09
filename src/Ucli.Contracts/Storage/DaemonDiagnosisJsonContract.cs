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
internal sealed record DaemonDiagnosisJsonContract (
    string? Reason,
    string? Message,
    string? ReportedBy,
    bool? IsInferred,
    DateTimeOffset UpdatedAtUtc,
    int? ProcessId,
    string? EditorInstancePath,
    DateTimeOffset SessionIssuedAtUtc,
    DateTimeOffset? ProcessStartedAtUtc = null,
    string? UnityLogPath = null,
    string? StartupPhase = null,
    string? ActionRequired = null,
    DaemonDiagnosisPrimaryDiagnosticJsonContract? PrimaryDiagnostic = null);
