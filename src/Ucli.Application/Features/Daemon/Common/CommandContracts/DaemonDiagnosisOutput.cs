namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Represents normalized payload values for one daemon diagnosis projection. </summary>
/// <param name="Reason"> The normalized daemon diagnosis reason value. </param>
/// <param name="Message"> The human-readable daemon diagnosis message. </param>
/// <param name="ReportedBy"> The normalized subject that reported this daemon diagnosis. </param>
/// <param name="IsInferred"> <see langword="true" /> when CLI inferred the diagnosis after observation; otherwise <see langword="false" />. </param>
/// <param name="UpdatedAtUtc"> The UTC timestamp when the diagnosis was recorded or derived. </param>
/// <param name="ProcessId"> The daemon process identifier when available; otherwise <see langword="null" />. </param>
/// <param name="EditorInstancePath"> The Unity <c>Library/EditorInstance.json</c> path associated with the diagnosis when available; otherwise <see langword="null" />. </param>
internal sealed record DaemonDiagnosisOutput (
    string Reason,
    string Message,
    string ReportedBy,
    bool IsInferred,
    DateTimeOffset UpdatedAtUtc,
    int? ProcessId,
    string? EditorInstancePath);
