using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Artifacts;

/// <summary> Opens and measures local artifacts through physical-node-bound read sessions. </summary>
internal static class ImmutableArtifactFileReadBoundary
{
    /// <summary> Opens a stable read session for an existing regular artifact inside a repository root. </summary>
    public static ArtifactPhysicalFileSession OpenSession (
        ContainedPath repositoryFile,
        string subject,
        CancellationToken cancellationToken)
    {
        return ArtifactPhysicalFileSession.Open(
            ArtifactPhysicalFileRequest.Create(repositoryFile, subject),
            cancellationToken);
    }

    /// <summary> Measures bytes while verifying the physical path before and after reading. </summary>
    public static async ValueTask<Measurement> MeasureAsync (
        ContainedPath repositoryFile,
        string subject,
        CancellationToken cancellationToken)
    {
        using var session = OpenSession(repositoryFile, subject, cancellationToken);
        return await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
    }

    internal readonly record struct Measurement (
        Sha256Digest Digest,
        ulong SizeBytes)
    {
        public void EnsureMatches (
            Measurement actual,
            AbsolutePath path,
            string diagnostic)
        {
            if (this != actual)
            {
                throw new IOException($"{diagnostic}: {path.Value}");
            }
        }
    }
}
