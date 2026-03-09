namespace MackySoft.Ucli.Daemon;

/// <summary> Represents one structured daemon termination diagnosis associated with one daemon lifecycle. </summary>
/// <param name="Reason"> The normalized daemon diagnosis reason value. </param>
/// <param name="Message"> The human-readable daemon diagnosis message. </param>
/// <param name="UpdatedAtUtc"> The UTC timestamp when the diagnosis was recorded or derived. </param>
/// <param name="ProcessId"> The daemon process identifier when available. </param>
/// <param name="SessionIssuedAtUtc"> The daemon session issuance timestamp associated with the diagnosis. </param>
internal sealed record DaemonDiagnosis (
    string Reason,
    string Message,
    DateTimeOffset UpdatedAtUtc,
    int? ProcessId,
    DateTimeOffset SessionIssuedAtUtc);
