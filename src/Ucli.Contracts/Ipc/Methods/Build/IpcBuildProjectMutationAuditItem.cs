namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one project file mutation observed during build runner invocation. </summary>
/// <param name="Path"> The project-relative path. </param>
/// <param name="ChangeKind"> The mutation change kind. </param>
/// <param name="BeforeSha256"> The SHA-256 digest before runner invocation, or <see langword="null" /> for added files. </param>
/// <param name="AfterSha256"> The SHA-256 digest after runner invocation, or <see langword="null" /> for deleted files. </param>
public sealed record IpcBuildProjectMutationAuditItem (
    string Path,
    string ChangeKind,
    string? BeforeSha256,
    string? AfterSha256);
