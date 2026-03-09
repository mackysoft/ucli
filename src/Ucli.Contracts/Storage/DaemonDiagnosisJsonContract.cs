namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents persisted daemon <c>daemon-diagnosis.json</c> contract fields. </summary>
/// <param name="Reason"> The normalized daemon diagnosis reason value. </param>
/// <param name="Message"> The human-readable daemon diagnosis message. </param>
/// <param name="UpdatedAtUtc"> The UTC timestamp when the diagnosis was recorded. </param>
/// <param name="ProcessId"> The daemon process identifier when available. </param>
/// <param name="SessionIssuedAtUtc"> The daemon session issuance timestamp associated with the diagnosis. </param>
internal sealed record DaemonDiagnosisJsonContract (
    string? Reason,
    string? Message,
    DateTimeOffset UpdatedAtUtc,
    int? ProcessId,
    DateTimeOffset SessionIssuedAtUtc);
