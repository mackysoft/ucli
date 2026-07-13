using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Infrastructure.Ipc;

/// <summary> Represents one deterministic purpose-bound Unix socket fallback path under an absolute temporary root. </summary>
internal sealed record UnixSocketFallbackPath
{
    private const int IdentityHexCharacterCount = 32;

    private const string DaemonDirectoryPrefix = "ucli-d-";

    private const string GuiSupervisorDirectoryPrefix = "ucli-g-";

    private const string SupervisorDirectoryPrefix = "ucli-s-";

    private const string SupervisorGenerationDirectoryPrefix = "ucli-sg-";

    private const string SupervisorPublicationLockDirectoryPrefix = "ucli-sl-";

    private const string ListenerOwnershipLockDirectoryPrefix = "ucli-il-";

    /// <summary> Initializes one fallback path after validating its root, purpose, identity, and transport length. </summary>
    /// <param name="temporaryDirectoryPath"> The absolute trusted temporary-directory root. </param>
    /// <param name="purpose"> The closed uCLI purpose that determines the directory prefix. </param>
    /// <param name="identitySource"> The non-empty stable identity source hashed into the directory name. </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="temporaryDirectoryPath" /> is not absolute or <paramref name="identitySource" /> is empty.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="purpose" /> is undefined. </exception>
    /// <exception cref="InvalidOperationException"> Thrown when the path cannot retain a 128-bit identity within the transport limit. </exception>
    public UnixSocketFallbackPath (
        string temporaryDirectoryPath,
        UnixSocketFallbackPurpose purpose,
        string identitySource)
    {
        if (string.IsNullOrWhiteSpace(temporaryDirectoryPath))
        {
            throw new ArgumentException(
                "Temporary directory path must not be empty.",
                nameof(temporaryDirectoryPath));
        }

        if (!Path.IsPathFullyQualified(temporaryDirectoryPath))
        {
            throw new ArgumentException(
                "Temporary directory path must be fully qualified.",
                nameof(temporaryDirectoryPath));
        }

        var directoryPrefix = ResolveDirectoryPrefix(purpose);
        if (string.IsNullOrWhiteSpace(identitySource))
        {
            throw new ArgumentException("Identity source must not be empty.", nameof(identitySource));
        }

        var normalizedTemporaryDirectoryPath = NormalizeDirectoryPath(temporaryDirectoryPath);
        var identityHex = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(identitySource))
            [..IdentityHexCharacterCount];
        var directoryPath = Path.Combine(
            normalizedTemporaryDirectoryPath,
            directoryPrefix + identityHex);
        var socketPath = Path.Combine(directoryPath, UcliIpcEndpointNames.UnixSocketFileName);
        var socketPathByteCount = Encoding.UTF8.GetByteCount(socketPath);
        if (socketPathByteCount > IpcTransportConstraints.UnixDomainSocketPathMaxBytes)
        {
            var basePath = Path.Combine(
                normalizedTemporaryDirectoryPath,
                directoryPrefix,
                UcliIpcEndpointNames.UnixSocketFileName);
            var availableIdentityHexCharacters = Math.Max(
                IpcTransportConstraints.UnixDomainSocketPathMaxBytes - Encoding.UTF8.GetByteCount(basePath),
                0);
            throw new InvalidOperationException(
                "Unix socket fallback path cannot retain the required 128-bit endpoint identity. " +
                $"TempRoot={normalizedTemporaryDirectoryPath}, Purpose={purpose}, " +
                $"AvailableIdentityHexChars={availableIdentityHexCharacters}, RequiredIdentityHexChars={IdentityHexCharacterCount}.");
        }

        Purpose = purpose;
        DirectoryPath = directoryPath;
        SocketPath = socketPath;
    }

    /// <summary> Gets the closed uCLI purpose that owns this path. </summary>
    public UnixSocketFallbackPurpose Purpose { get; }

    /// <summary> Gets the exact fallback directory path derived by this value. </summary>
    public string DirectoryPath { get; }

    /// <summary> Gets the exact Unix socket path within <see cref="DirectoryPath" />. </summary>
    public string SocketPath { get; }

    /// <summary> Determines whether a directory name has the canonical fallback shape for one defined purpose. </summary>
    /// <param name="directoryName"> The single directory name to inspect. </param>
    /// <param name="purpose"> The closed uCLI purpose that determines the expected prefix. </param>
    /// <returns> <see langword="true" /> when the name contains the purpose prefix and one 128-bit lowercase hexadecimal identity. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="purpose" /> is undefined. </exception>
    internal static bool IsDirectoryNameForPurpose (
        string? directoryName,
        UnixSocketFallbackPurpose purpose)
    {
        var directoryPrefix = ResolveDirectoryPrefix(purpose);
        if (directoryName == null
            || directoryName.Length != directoryPrefix.Length + IdentityHexCharacterCount
            || !directoryName.StartsWith(directoryPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        for (var index = directoryPrefix.Length; index < directoryName.Length; index++)
        {
            var character = directoryName[index];
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeDirectoryPath (string directoryPath)
    {
        var normalizedDirectoryPath = Path.GetFullPath(directoryPath);
        var rootPath = Path.GetPathRoot(normalizedDirectoryPath);
        var minimumLength = rootPath?.Length ?? 0;
        var normalizedLength = normalizedDirectoryPath.Length;
        while (normalizedLength > minimumLength
               && IsDirectorySeparator(normalizedDirectoryPath[normalizedLength - 1]))
        {
            normalizedLength--;
        }

        return normalizedLength == normalizedDirectoryPath.Length
            ? normalizedDirectoryPath
            : normalizedDirectoryPath[..normalizedLength];
    }

    private static string ResolveDirectoryPrefix (UnixSocketFallbackPurpose purpose)
    {
        return purpose switch
        {
            UnixSocketFallbackPurpose.Daemon => DaemonDirectoryPrefix,
            UnixSocketFallbackPurpose.GuiSupervisor => GuiSupervisorDirectoryPrefix,
            UnixSocketFallbackPurpose.Supervisor => SupervisorDirectoryPrefix,
            UnixSocketFallbackPurpose.SupervisorGeneration => SupervisorGenerationDirectoryPrefix,
            UnixSocketFallbackPurpose.SupervisorPublicationLock => SupervisorPublicationLockDirectoryPrefix,
            UnixSocketFallbackPurpose.ListenerOwnershipLock => ListenerOwnershipLockDirectoryPrefix,
            _ => throw new ArgumentOutOfRangeException(
                nameof(purpose),
                purpose,
                "Unix socket fallback purpose is undefined."),
        };
    }

    private static bool IsDirectorySeparator (char value)
    {
        return value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;
    }
}
