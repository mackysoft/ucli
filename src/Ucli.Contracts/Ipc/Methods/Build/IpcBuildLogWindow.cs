namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the timestamp window used for a build log artifact. </summary>
/// <param name="StartedAtUtc"> The UTC timestamp captured immediately before BuildPipeline execution. </param>
/// <param name="CompletedAtUtc"> The UTC timestamp captured immediately after BuildPipeline execution. </param>
public sealed record IpcBuildLogWindow (
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);
