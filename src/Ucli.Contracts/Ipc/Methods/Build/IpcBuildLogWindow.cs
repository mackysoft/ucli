namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the timestamp window used for a build log artifact. </summary>
/// <param name="StartedAtUtc"> The UTC timestamp captured immediately before BuildPipeline execution. </param>
/// <param name="CompletedAtUtc"> The UTC timestamp captured immediately after BuildPipeline execution. </param>
/// <param name="CursorStart"> The log cursor captured immediately before runner invocation, or <see langword="null" /> when unavailable. </param>
/// <param name="CursorEnd"> The log cursor captured immediately after runner terminal result observation, or <see langword="null" /> when unavailable. </param>
public sealed record IpcBuildLogWindow (
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    string? CursorStart = null,
    string? CursorEnd = null);
