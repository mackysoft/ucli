namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;

/// <summary> Represents the primary machine-readable diagnostic included in compile evidence. </summary>
internal sealed record CompilePrimaryDiagnosticOutput (
    string Kind,
    string? Code,
    string? File,
    int? Line,
    int? Column,
    string? Message);
