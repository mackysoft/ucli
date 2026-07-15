using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one project file mutation observed during build runner invocation. </summary>
public sealed record IpcBuildProjectMutationAuditItem
{
    /// <summary> Initializes one structurally valid project file mutation. </summary>
    /// <param name="Path"> The normalized project-relative audited path. </param>
    /// <param name="ChangeKind"> The mutation change kind. </param>
    /// <param name="BeforeSha256"> The digest before runner invocation, or <see langword="null" /> for an added file. </param>
    /// <param name="AfterSha256"> The digest after runner invocation, or <see langword="null" /> for a deleted file. </param>
    /// <exception cref="ArgumentException"> Thrown when the path or digest shape does not match <paramref name="ChangeKind" />. </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="ChangeKind" /> is not a contract value. </exception>
    [JsonConstructor]
    public IpcBuildProjectMutationAuditItem (
        string Path,
        IpcBuildProjectMutationChangeKind ChangeKind,
        Sha256Digest? BeforeSha256,
        Sha256Digest? AfterSha256)
    {
        if (!RelativePathContract.IsNormalized(Path) || !IsAuditedProjectPath(Path))
        {
            throw new ArgumentException("Project mutation path must be a normalized path under an audited project root.", nameof(Path));
        }

        if (!ContractLiteralCodec.IsDefined(ChangeKind))
        {
            throw new ArgumentOutOfRangeException(nameof(ChangeKind), ChangeKind, "Project mutation change kind must be specified.");
        }

        ValidateDigestShape(ChangeKind, BeforeSha256, AfterSha256);
        this.Path = Path;
        this.ChangeKind = ChangeKind;
        this.BeforeSha256 = BeforeSha256;
        this.AfterSha256 = AfterSha256;
    }

    public string Path { get; }

    public IpcBuildProjectMutationChangeKind ChangeKind { get; }

    public Sha256Digest? BeforeSha256 { get; }

    public Sha256Digest? AfterSha256 { get; }

    private static bool IsAuditedProjectPath (string path)
    {
        return path.StartsWith("Assets/", StringComparison.Ordinal)
            || path.StartsWith("ProjectSettings/", StringComparison.Ordinal)
            || path.StartsWith("Packages/", StringComparison.Ordinal);
    }

    private static void ValidateDigestShape (
        IpcBuildProjectMutationChangeKind changeKind,
        Sha256Digest? beforeSha256,
        Sha256Digest? afterSha256)
    {
        var isValid = changeKind switch
        {
            IpcBuildProjectMutationChangeKind.Added => beforeSha256 == null && afterSha256 != null,
            IpcBuildProjectMutationChangeKind.Modified => beforeSha256 != null
                && afterSha256 != null
                && beforeSha256 != afterSha256,
            IpcBuildProjectMutationChangeKind.Deleted => beforeSha256 != null && afterSha256 == null,
            _ => false,
        };

        if (!isValid)
        {
            throw new ArgumentException("Project mutation digests must match the change-kind contract.");
        }
    }
}
