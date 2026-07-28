using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Verifies path-based immutable artifact references against local files inside one repository root. </summary>
internal static class ImmutableArtifactFileVerifier
{
    /// <summary>
    /// Resolves the repository-root-relative path from a path or path-and-URI reference and verifies digest and
    /// size from one read handle. URI-only references are never fetched.
    /// </summary>
    /// <param name="repositoryRoot">
    /// The repository root against which the portable artifact path is resolved.
    /// </param>
    /// <param name="artifact"> The path or path-and-URI artifact reference to verify. </param>
    /// <param name="cancellationToken"> The cancellation token observed while reading the artifact. </param>
    /// <returns> A task that completes only when the local final bytes match the reference. </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="repositoryRoot" /> or <paramref name="artifact" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="artifact" /> is URI-only. URI locators are never fetched.
    /// </exception>
    /// <exception cref="IOException">
    /// Thrown when the referenced file is missing, the resolved path crosses a reparse-point directory, the path
    /// identifies a non-regular node or reparse point, the bytes cannot be read, or the digest or size differs.
    /// </exception>
    public static async ValueTask VerifyAsync (
        AbsolutePath repositoryRoot,
        ArtifactRef artifact,
        CancellationToken cancellationToken)
    {
        if (repositoryRoot is null)
        {
            throw new ArgumentNullException(nameof(repositoryRoot));
        }

        if (artifact is null)
        {
            throw new ArgumentNullException(nameof(artifact));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var repositoryArtifactFile = ContainedPath.Create(
            repositoryRoot,
            RootRelativePath.Parse(GetRepositoryPath(artifact).Value));

        var measurement = await ImmutableArtifactFileReadBoundary.MeasureAsync(
                repositoryArtifactFile,
                "Referenced artifact file",
                cancellationToken)
            .ConfigureAwait(false);
        if (measurement.SizeBytes != artifact.SizeBytes
            || measurement.Digest != artifact.Digest)
        {
            throw new IOException(
                $"Referenced artifact digest or size does not match the local final bytes: {repositoryArtifactFile.Target.Value}");
        }
    }

    private static ArtifactPath GetRepositoryPath (ArtifactRef artifact)
    {
        return artifact switch
        {
            PathArtifactRef pathArtifact => pathArtifact.Path,
            PathAndUriArtifactRef pathAndUriArtifact => pathAndUriArtifact.Path,
            _ => throw new InvalidOperationException(
                "Local artifact verification requires a path locator; URI locators are not fetched."),
        };
    }
}
