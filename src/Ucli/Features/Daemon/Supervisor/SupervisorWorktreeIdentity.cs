using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Cryptography;

namespace MackySoft.Ucli.Features.Daemon.Supervisor;

/// <summary> Identifies one worktree-local supervisor from its normalized storage root. </summary>
internal sealed record SupervisorWorktreeIdentity
{
    private const int LaunchServiceNameSuffixLength = 16;
    private const int NamedPipeAddressSegmentLength = 24;

    private SupervisorWorktreeIdentity (
        AbsolutePath normalizedStorageRoot,
        string launchServiceNameSuffix,
        string namedPipeAddressSegment)
    {
        NormalizedStorageRoot = normalizedStorageRoot;
        LaunchServiceNameSuffix = launchServiceNameSuffix;
        NamedPipeAddressSegment = namedPipeAddressSegment;
    }

    /// <summary> Gets the normalized absolute storage-root path used to derive this identity. </summary>
    public AbsolutePath NormalizedStorageRoot { get; }

    /// <summary> Gets the fixed-length suffix used by launchd and systemd service names. </summary>
    public string LaunchServiceNameSuffix { get; }

    /// <summary> Gets the fixed-length worktree segment used by the supervisor named-pipe address. </summary>
    public string NamedPipeAddressSegment { get; }

    /// <summary> Creates one identity after normalizing the supplied storage-root path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The worktree-local supervisor identity. </returns>
    public static SupervisorWorktreeIdentity Create (AbsolutePath storageRoot)
    {
        ArgumentNullException.ThrowIfNull(storageRoot);
        var identityText = DeterministicPathText.ForIdentity(storageRoot);
        var digest = Sha256Digest.Compute(Encoding.UTF8.GetBytes(identityText)).ToString();
        return new SupervisorWorktreeIdentity(
            storageRoot,
            digest[..LaunchServiceNameSuffixLength],
            digest[..NamedPipeAddressSegmentLength]);
    }
}
