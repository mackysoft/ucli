using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Captures repository root, ancestor, and leaf physical node identities for one artifact path. </summary>
internal sealed class ArtifactPhysicalPathSnapshot
{
    private ArtifactPhysicalPathSnapshot (
        ArtifactPhysicalDirectorySnapshot directories,
        FileSystemNodeIdentity leafIdentity)
    {
        Directories = directories;
        LeafIdentity = leafIdentity;
    }

    public ArtifactPhysicalDirectorySnapshot Directories { get; }

    private FileSystemNodeIdentity LeafIdentity { get; }

    public static ArtifactPhysicalPathSnapshot Capture (ArtifactPhysicalFileRequest request)
    {
        var directories = ArtifactPhysicalDirectorySnapshot.Capture(
            request.RepositoryFile,
            request.Subject);
        var leafIdentity = FileSystemNodeIdentityReader.ReadPath(
            request.RepositoryFile.Target,
            request.Subject);
        request.EnsureRegularSingleEntryFile(leafIdentity);
        return new ArtifactPhysicalPathSnapshot(directories, leafIdentity);
    }

    public void EnsureSamePathAs (
        ArtifactPhysicalPathSnapshot other,
        string subject)
    {
        Directories.EnsureSamePathAs(other.Directories, subject);
        if (LeafIdentity != other.LeafIdentity)
        {
            throw new IOException(
                $"{subject} leaf physical node changed while it was in use.");
        }
    }

    public void EnsureLeafIs (
        FileSystemNodeIdentity identity,
        string subject)
    {
        if (LeafIdentity != identity)
        {
            throw new IOException(
                $"{subject} path and open handle identify different physical nodes.");
        }
    }
}
