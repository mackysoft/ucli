using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents the Unity Build Profile input selected by path. </summary>
public sealed record IpcUnityBuildProfileInput
{
    /// <summary> Initializes a Unity Build Profile input. </summary>
    /// <param name="Path"> The canonical project-relative Build Profile asset path. </param>
    /// <param name="Digest"> The lowercase SHA-256 digest of the resolved asset file when available. </param>
    /// <param name="ApplyAudit"> The profile application audit when available. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Path" /> is <see langword="null" />. </exception>
    [JsonConstructor]
    public IpcUnityBuildProfileInput (
        UnityBuildProfileAssetPath Path,
        Sha256Digest? Digest,
        IpcUnityBuildProfileApplyAudit? ApplyAudit)
    {
        this.Path = Path ?? throw new ArgumentNullException(nameof(Path));
        this.Digest = Digest;
        this.ApplyAudit = ApplyAudit;
    }

    /// <summary> Gets the canonical project-relative Build Profile asset path. </summary>
    public UnityBuildProfileAssetPath Path { get; }

    /// <summary> Gets the resolved asset digest when available. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Sha256Digest? Digest { get; }

    /// <summary> Gets the profile application audit when available. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IpcUnityBuildProfileApplyAudit? ApplyAudit { get; }
}
