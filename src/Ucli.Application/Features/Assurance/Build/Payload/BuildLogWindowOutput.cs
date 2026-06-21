namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the build log time window. </summary>
internal sealed record BuildLogWindowOutput (
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string? CursorStart = null,
    string? CursorEnd = null);
