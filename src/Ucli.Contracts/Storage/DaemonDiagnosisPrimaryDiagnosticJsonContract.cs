namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Represents the primary machine-readable diagnostic attached to one daemon diagnosis. </summary>
/// <param name="Kind"> The normalized diagnostic kind value. </param>
/// <param name="Code"> The diagnostic code when available. </param>
/// <param name="File"> The diagnostic file path when available. </param>
/// <param name="Line"> The one-based diagnostic line number when available. </param>
/// <param name="Column"> The one-based diagnostic column number when available. </param>
/// <param name="Message"> The diagnostic message when available. </param>
internal sealed record DaemonDiagnosisPrimaryDiagnosticJsonContract (
    string? Kind,
    string? Code,
    string? File,
    int? Line,
    int? Column,
    string? Message);
