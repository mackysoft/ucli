using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Binds one diagnostic subject and physical-file invariant to a contained artifact path. </summary>
internal sealed class ArtifactPhysicalFileRequest
{
    private ArtifactPhysicalFileRequest (ContainedPath repositoryFile, string subject)
    {
        RepositoryFile = repositoryFile;
        Subject = subject;
    }

    public ContainedPath RepositoryFile { get; }

    public string Subject { get; }

    public static ArtifactPhysicalFileRequest Create (
        ContainedPath repositoryFile,
        string subject)
    {
        if (repositoryFile is null)
        {
            throw new ArgumentNullException(nameof(repositoryFile));
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException(
                "Artifact file subject must not be empty or whitespace.",
                nameof(subject));
        }

        return new ArtifactPhysicalFileRequest(repositoryFile, subject);
    }

    public void EnsureRegularSingleEntryFile (FileSystemNodeIdentity identity)
    {
        if (!identity.IsRegularFile || identity.IsReparsePoint)
        {
            throw new IOException(
                $"{Subject} must identify a regular physical file: {RepositoryFile.Target.Value}");
        }

        if (identity.LinkCount != 1)
        {
            throw new IOException(
                $"{Subject} must identify a physical file with exactly one directory entry: {RepositoryFile.Target.Value}");
        }
    }
}
