using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Captures the repository root and ancestor identities for one contained artifact file. </summary>
internal sealed class ArtifactPhysicalDirectorySnapshot
{
    private readonly DirectoryIdentity[] directories;

    private ArtifactPhysicalDirectorySnapshot (DirectoryIdentity[] directories)
    {
        this.directories = directories;
    }

    public static ArtifactPhysicalDirectorySnapshot Capture (
        ContainedPath repositoryFile,
        string subject)
    {
        var paths = ResolveDirectoryChain(repositoryFile, subject);
        var identities = new DirectoryIdentity[paths.Count];
        for (var index = 0; index < paths.Count; index++)
        {
            var path = paths[index];
            identities[index] = new DirectoryIdentity(
                path,
                ReadDirectoryIdentity(path, subject));
        }

        return new ArtifactPhysicalDirectorySnapshot(identities);
    }

    public void EnsureSamePathAs (
        ArtifactPhysicalDirectorySnapshot other,
        string subject)
    {
        if (directories.Length != other.directories.Length)
        {
            throw CreateChangedDirectoryException(subject);
        }

        for (var index = 0; index < directories.Length; index++)
        {
            if (!directories[index].IsSameAs(other.directories[index]))
            {
                throw CreateChangedDirectoryException(subject);
            }
        }
    }

    private static IReadOnlyList<AbsolutePath> ResolveDirectoryChain (
        ContainedPath repositoryFile,
        string subject)
    {
        var current = GetParent(repositoryFile);
        var reverseDirectories = new Stack<AbsolutePath>();
        reverseDirectories.Push(current);
        while (!current.IsSameAs(repositoryFile.BoundaryRoot))
        {
            current = GetRepositoryParent(current, subject);
            reverseDirectories.Push(current);
        }

        return reverseDirectories.ToArray();
    }

    private static AbsolutePath GetParent (ContainedPath file)
    {
        if (!file.Target.TryGetParent(out var parent))
        {
            throw new InvalidOperationException(
                $"Artifact file parent directory could not be resolved: {file.Target.Value}");
        }

        return parent;
    }

    private static AbsolutePath GetRepositoryParent (
        AbsolutePath current,
        string subject)
    {
        if (!current.TryGetParent(out var parent))
        {
            throw new InvalidOperationException(
                $"{subject} escaped its repository root while resolving a parent: {current.Value}");
        }

        return parent;
    }

    private static FileSystemNodeIdentity ReadDirectoryIdentity (
        AbsolutePath path,
        string subject)
    {
        var identity = FileSystemNodeIdentityReader.ReadPath(path, subject);
        if (identity.IsReparsePoint)
        {
            throw new IOException(
                $"{subject} directory must not contain a reparse point: {path.Value}");
        }

        if (!identity.IsDirectory)
        {
            throw new IOException(
                $"{subject} path must contain only directories: {path.Value}");
        }

        return identity;
    }

    private static IOException CreateChangedDirectoryException (string subject)
    {
        return new IOException(
            $"{subject} repository root or ancestor physical node changed while the file was in use.");
    }

    private readonly record struct DirectoryIdentity (
        AbsolutePath Path,
        FileSystemNodeIdentity Identity)
    {
        public bool IsSameAs (DirectoryIdentity other)
        {
            return Path.IsSameAs(other.Path)
                && Identity.IsSamePhysicalNodeAs(other.Identity);
        }
    }
}
