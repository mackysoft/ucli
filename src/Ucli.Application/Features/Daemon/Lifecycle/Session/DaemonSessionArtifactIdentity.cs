using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Identifies the exact serialized contents observed in one daemon session file. </summary>
internal sealed record DaemonSessionArtifactIdentity
{
    private DaemonSessionArtifactIdentity (Sha256Digest contentDigest)
    {
        ContentDigest = contentDigest;
    }

    private Sha256Digest ContentDigest { get; }

    /// <summary> Creates an identity for exact serialized daemon session contents. </summary>
    /// <param name="serializedContent"> The serialized session file contents. </param>
    /// <returns> The content identity. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="serializedContent" /> is <see langword="null" />. </exception>
    public static DaemonSessionArtifactIdentity Create (string serializedContent)
    {
        ArgumentNullException.ThrowIfNull(serializedContent);
        return new DaemonSessionArtifactIdentity(
            Sha256Digest.Compute(Encoding.UTF8.GetBytes(serializedContent)));
    }

    /// <summary> Determines whether serialized contents have this exact identity. </summary>
    /// <param name="serializedContent"> The serialized session file contents to compare. </param>
    /// <returns> <see langword="true" /> when the contents match; otherwise <see langword="false" />. </returns>
    public bool Matches (string serializedContent)
    {
        ArgumentNullException.ThrowIfNull(serializedContent);
        return ContentDigest == Sha256Digest.Compute(Encoding.UTF8.GetBytes(serializedContent));
    }
}
