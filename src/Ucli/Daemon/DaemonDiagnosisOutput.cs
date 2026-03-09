namespace MackySoft.Ucli.Daemon;

/// <summary> Represents normalized payload values for one daemon diagnosis projection. </summary>
/// <param name="Reason"> The normalized daemon diagnosis reason value. </param>
/// <param name="Message"> The human-readable daemon diagnosis message. </param>
/// <param name="UpdatedAtUtc"> The UTC timestamp when the diagnosis was recorded or derived. </param>
/// <param name="ProcessId"> The daemon process identifier when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonDiagnosisOutput (
    string Reason,
    string Message,
    DateTimeOffset UpdatedAtUtc,
    int? ProcessId);
