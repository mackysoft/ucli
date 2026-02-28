namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one touched persistence-unit resource in an operation result. </summary>
/// <param name="Kind"> The touched resource kind. </param>
/// <param name="Path"> The project-relative path for the touched resource. </param>
/// <param name="Guid"> The optional asset guid when available. </param>
public sealed record IpcExecuteTouchedResource (
    string Kind,
    string Path,
    string? Guid);