using System.Security.Cryptography;
using System.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Identifies the exact serialized contents observed in one daemon session file. </summary>
internal sealed record DaemonSessionArtifactIdentity
{
    private DaemonSessionArtifactIdentity (string contentSha256)
    {
        ContentSha256 = contentSha256;
    }

    /// <summary> Gets the lowercase SHA-256 digest of the UTF-8 serialized contents. </summary>
    public string ContentSha256 { get; }

    /// <summary> Creates an identity for exact serialized daemon session contents. </summary>
    /// <param name="serializedContent"> The serialized session file contents. </param>
    /// <returns> The content identity. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="serializedContent" /> is <see langword="null" />. </exception>
    public static DaemonSessionArtifactIdentity Create (string serializedContent)
    {
        ArgumentNullException.ThrowIfNull(serializedContent);
        var contentBytes = Encoding.UTF8.GetBytes(serializedContent);
        var digestBytes = SHA256.HashData(contentBytes);
        return new DaemonSessionArtifactIdentity(Convert.ToHexString(digestBytes).ToLowerInvariant());
    }

    /// <summary> Determines whether serialized contents have this exact identity. </summary>
    /// <param name="serializedContent"> The serialized session file contents to compare. </param>
    /// <returns> <see langword="true" /> when the contents match; otherwise <see langword="false" />. </returns>
    public bool Matches (string serializedContent)
    {
        return string.Equals(
            ContentSha256,
            Create(serializedContent).ContentSha256,
            StringComparison.Ordinal);
    }
}
