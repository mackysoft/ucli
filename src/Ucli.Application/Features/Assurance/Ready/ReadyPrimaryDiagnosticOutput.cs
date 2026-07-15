using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one machine-readable diagnostic selected as ready lifecycle evidence. </summary>
/// <param name="Kind"> The normalized diagnostic kind value. </param>
/// <param name="Code"> The diagnostic code when available. </param>
/// <param name="File"> The diagnostic file path when available. </param>
/// <param name="Line"> The one-based diagnostic line number when available. </param>
/// <param name="Column"> The one-based diagnostic column number when available. </param>
/// <param name="Message"> The diagnostic message when available. </param>
internal sealed record ReadyPrimaryDiagnosticOutput (
    DaemonDiagnosisPrimaryDiagnosticKind Kind,
    string? Code,
    string? File,
    int? Line,
    int? Column,
    string? Message);
