namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one dirty build input item. </summary>
/// <param name="Kind"> The item kind. </param>
/// <param name="Path"> The project-relative item path. </param>
public sealed record IpcBuildDirtyStateItem (
    string Kind,
    string Path);
