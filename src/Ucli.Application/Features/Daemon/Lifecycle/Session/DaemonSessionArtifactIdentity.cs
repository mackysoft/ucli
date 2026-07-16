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

    /// <summary> Creates an identity for the exact bytes observed in a daemon session file. </summary>
    /// <param name="serializedContent"> The serialized session file bytes. </param>
    /// <returns> The content identity. </returns>
    public static DaemonSessionArtifactIdentity Create (ReadOnlySpan<byte> serializedContent)
    {
        return new DaemonSessionArtifactIdentity(Sha256Digest.Compute(serializedContent));
    }
}
