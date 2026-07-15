using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor;

/// <summary> Identifies one worktree-local supervisor from its normalized storage root. </summary>
internal sealed record SupervisorWorktreeIdentity
{
    private const int LaunchServiceNameSuffixLength = 16;
    private const int NamedPipeAddressSegmentLength = 24;

    private SupervisorWorktreeIdentity (
        string normalizedStorageRoot,
        string launchServiceNameSuffix,
        string namedPipeAddressSegment)
    {
        NormalizedStorageRoot = normalizedStorageRoot;
        LaunchServiceNameSuffix = launchServiceNameSuffix;
        NamedPipeAddressSegment = namedPipeAddressSegment;
    }

    /// <summary> Gets the normalized absolute storage-root path used to derive this identity. </summary>
    public string NormalizedStorageRoot { get; }

    /// <summary> Gets the fixed-length suffix used by launchd and systemd service names. </summary>
    public string LaunchServiceNameSuffix { get; }

    /// <summary> Gets the fixed-length worktree segment used by the supervisor named-pipe address. </summary>
    public string NamedPipeAddressSegment { get; }

    /// <summary> Creates one identity after normalizing the supplied storage-root path. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <returns> The worktree-local supervisor identity. </returns>
    public static SupervisorWorktreeIdentity Create (string storageRoot)
    {
        var normalizedStorageRoot = UcliStoragePathResolver.NormalizeStorageRootPath(storageRoot);
        var digest = Sha256Digest.Compute(Encoding.UTF8.GetBytes(normalizedStorageRoot)).ToString();
        return new SupervisorWorktreeIdentity(
            normalizedStorageRoot,
            digest[..LaunchServiceNameSuffixLength],
            digest[..NamedPipeAddressSegmentLength]);
    }
}
