namespace MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;

/// <summary> Represents one machine-readable diagnostic selected as the primary cause of a daemon diagnosis projection. </summary>
/// <param name="Kind"> The normalized diagnostic kind value. </param>
/// <param name="Code"> The diagnostic code when available. </param>
/// <param name="File"> The diagnostic file path when available. </param>
/// <param name="Line"> The one-based diagnostic line number when available. </param>
/// <param name="Column"> The one-based diagnostic column number when available. </param>
/// <param name="Message"> The diagnostic message when available. </param>
internal sealed record DaemonPrimaryDiagnosticOutput (
    string Kind,
    string? Code,
    string? File,
    int? Line,
    int? Column,
    string? Message);
