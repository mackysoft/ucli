namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents one report reference in a ready assurance payload. </summary>
internal sealed record ReadyReportOutput (
    string? Path = null,
    string? Uri = null,
    string? Digest = null);
