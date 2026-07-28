using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Infrastructure.Paths;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Validates the same-directory source and destination paths for repository publication. </summary>
internal sealed class RepositoryArtifactPublicationPaths
{
    private RepositoryArtifactPublicationPaths (
        ContainedPath temporaryFile,
        ContainedPath destinationFile,
        ArtifactPath artifactPath)
    {
        TemporaryFile = temporaryFile;
        DestinationFile = destinationFile;
        ArtifactPath = artifactPath;
    }

    public ContainedPath TemporaryFile { get; }

    public ContainedPath DestinationFile { get; }

    public ArtifactPath ArtifactPath { get; }

    public static RepositoryArtifactPublicationPaths Create (
        ContainedPath temporaryFile,
        ContainedPath destinationFile)
    {
        EnsureSameRepositoryRoot(temporaryFile, destinationFile);
        EnsureDifferentFiles(temporaryFile, destinationFile);
        EnsureSameDirectory(temporaryFile, destinationFile);
        return new RepositoryArtifactPublicationPaths(
            temporaryFile,
            destinationFile,
            CreateArtifactPath(destinationFile));
    }

    private static void EnsureSameRepositoryRoot (
        ContainedPath temporaryFile,
        ContainedPath destinationFile)
    {
        if (!temporaryFile.BoundaryRoot.IsSameAs(destinationFile.BoundaryRoot))
        {
            throw new ArgumentException(
                "Artifact temporary and destination paths must have the same repository root.",
                nameof(destinationFile));
        }
    }

    private static void EnsureDifferentFiles (
        ContainedPath temporaryFile,
        ContainedPath destinationFile)
    {
        if (temporaryFile.Target.IsSameAs(destinationFile.Target))
        {
            throw new ArgumentException(
                "Artifact temporary and destination paths must identify different files.",
                nameof(destinationFile));
        }
    }

    private static void EnsureSameDirectory (
        ContainedPath temporaryFile,
        ContainedPath destinationFile)
    {
        var temporaryDirectory = GetParentDirectory(temporaryFile, nameof(temporaryFile));
        var destinationDirectory = GetParentDirectory(destinationFile, nameof(destinationFile));
        if (!temporaryDirectory.Target.IsSameAs(destinationDirectory.Target))
        {
            throw new ArgumentException(
                "Artifact temporary and destination files must share one directory.",
                nameof(destinationFile));
        }
    }

    private static ContainedPath GetParentDirectory (
        ContainedPath file,
        string parameterName)
    {
        if (!file.Target.TryGetParent(out var parent))
        {
            throw new ArgumentException(
                "Artifact file parent directory could not be resolved.",
                parameterName);
        }

        return CreateContainedParent(file, parent, parameterName);
    }

    private static ContainedPath CreateContainedParent (
        ContainedPath file,
        AbsolutePath parent,
        string parameterName)
    {
        try
        {
            return ContainedPath.Create(file.BoundaryRoot, parent);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException(
                "Artifact file must be below its repository root.",
                parameterName,
                exception);
        }
    }

    private static ArtifactPath CreateArtifactPath (ContainedPath destinationFile)
    {
        if (!UcliPortablePathAdapter.TryFormat(destinationFile.RelativePath, out var portablePath))
        {
            throw new ArgumentException(
                "Artifact destination cannot be represented by the portable path contract.",
                nameof(destinationFile));
        }

        return new ArtifactPath(portablePath);
    }
}
