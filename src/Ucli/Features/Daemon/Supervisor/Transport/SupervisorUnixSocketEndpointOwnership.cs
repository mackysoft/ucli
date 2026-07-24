using System.Diagnostics.CodeAnalysis;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Owns one generation-specific supervisor Unix socket node and its canonical publication link. </summary>
internal sealed class SupervisorUnixSocketEndpointOwnership
{
    private static readonly AbsolutePath TemporaryDirectoryPath =
        AbsolutePath.Parse(Path.GetTempPath());

    private readonly AbsolutePath canonicalAddress;

    private readonly AbsolutePath canonicalDirectoryPath;

    private readonly AbsolutePath publicationLockPath;

    private readonly UnixSocketAccessBoundary generationAccessBoundary;

    private readonly UnixSocketFallbackPath generationFallbackPath;

    private readonly Guid publicationToken = Guid.NewGuid();

    private AbsolutePath? replacedGenerationAddress;

    private bool endpointPublished;

    private bool publicationCommitted;

    public SupervisorUnixSocketEndpointOwnership (AbsolutePath canonicalAddress)
    {
        ArgumentNullException.ThrowIfNull(canonicalAddress);
        if (!canonicalAddress.TryGetParent(out var resolvedCanonicalDirectoryPath))
        {
            throw new ArgumentException(
                "Canonical Unix socket address must have a parent directory.",
                nameof(canonicalAddress));
        }

        this.canonicalAddress = canonicalAddress;
        canonicalDirectoryPath = resolvedCanonicalDirectoryPath;
        generationFallbackPath = new UnixSocketFallbackPath(
            TemporaryDirectoryPath,
            UnixSocketFallbackPurpose.SupervisorGeneration,
            $"{canonicalAddress.Value}\n{publicationToken:N}");
        BoundAddress = generationFallbackPath.SocketPath;
        generationAccessBoundary = new UnixSocketAccessBoundary(BoundAddress);
        var publicationLockFallbackPath = new UnixSocketFallbackPath(
            TemporaryDirectoryPath,
            UnixSocketFallbackPurpose.SupervisorPublicationLock,
            canonicalAddress.Value);
        publicationLockPath = ContainedPath.Create(
            publicationLockFallbackPath.DirectoryPath,
            RootRelativePath.Parse(
                Path.ChangeExtension(
                    UcliIpcEndpointNames.UnixSocketFileName,
                    ".lock"))).Target;
    }

    /// <summary> Gets the generation-specific path that the listener must bind. </summary>
    public AbsolutePath BoundAddress { get; }

    /// <summary> Prepares the generation-specific node before the listener binds it. </summary>
    public void PrepareForBind ()
    {
        generationAccessBoundary.PrepareForBind();
    }

    /// <summary> Applies owner-only permissions to this generation's bound socket node. </summary>
    public void HardenBoundSocket ()
    {
        generationAccessBoundary.HardenBoundSocket();
    }

    /// <summary> Atomically publishes this generation at the canonical endpoint path. </summary>
    public void PublishBoundEndpoint ()
    {
        using var publicationLock = AcquirePublicationLock();
        FileSystemAccessBoundary.EnsureSecureDirectory(canonicalDirectoryPath);

        replacedGenerationAddress = TryResolvePublishedGenerationAddress(
            canonicalAddress,
            out var publishedGenerationAddress)
                ? publishedGenerationAddress
                : null;
        ReplaceCanonicalLink(BoundAddress);
        endpointPublished = true;
    }

    /// <summary> Commits this publication after the matching manifest has been published successfully. </summary>
    public void CommitPublication ()
    {
        using var publicationLock = AcquirePublicationLock();
        if (!endpointPublished)
        {
            throw new InvalidOperationException("Supervisor endpoint publication has not started.");
        }

        if (publicationCommitted)
        {
            return;
        }

        publicationCommitted = true;
        var retiredGenerationAddress = replacedGenerationAddress;
        replacedGenerationAddress = null;
        if (retiredGenerationAddress is not null
            && retiredGenerationAddress != BoundAddress)
        {
            try
            {
                DeleteGenerationNode(retiredGenerationAddress);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // The retired listener remains responsible for its own generation cleanup.
            }
        }
    }

    private void ReplaceCanonicalLink (AbsolutePath targetAddress)
    {
        var temporaryLinkPath = ContainedPath.Create(
            canonicalDirectoryPath,
            RootRelativePath.Parse(
                $".{Path.GetFileName(canonicalAddress.Value)}.{publicationToken:N}.link")).Target;
        try
        {
            File.Delete(temporaryLinkPath.Value);
            File.CreateSymbolicLink(temporaryLinkPath.Value, targetAddress.Value);
            File.Move(temporaryLinkPath.Value, canonicalAddress.Value, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryLinkPath.Value);
        }
    }

    /// <summary> Removes this generation without deleting a successor's canonical publication. </summary>
    public void Cleanup ()
    {
        try
        {
            using var publicationLock = AcquirePublicationLock();
            if (TryResolvePublishedGenerationAddress(canonicalAddress, out var publishedGenerationAddress)
                && publishedGenerationAddress == BoundAddress)
            {
                if (!publicationCommitted
                    && replacedGenerationAddress is not null
                    && File.Exists(replacedGenerationAddress.Value))
                {
                    ReplaceCanonicalLink(replacedGenerationAddress);
                }
                else
                {
                    File.Delete(canonicalAddress.Value);
                }
            }
        }
        catch (TimeoutException)
        {
            // A publisher holding the lock owns the canonical path decision. Never delete it without that decision.
        }
        finally
        {
            generationAccessBoundary.Cleanup();
            DeleteOwnedGenerationDirectoryIfEmpty();
        }
    }

    /// <summary> Resolves a validated generation-specific target from one canonical publication link. </summary>
    public static bool TryResolvePublishedGenerationAddress (
        AbsolutePath canonicalAddress,
        [NotNullWhen(true)] out AbsolutePath? generationAddress)
    {
        ArgumentNullException.ThrowIfNull(canonicalAddress);
        generationAddress = null;
        if (!canonicalAddress.TryGetParent(out var canonicalDirectoryPath))
        {
            return false;
        }

        string? linkTarget;
        try
        {
            linkTarget = new FileInfo(canonicalAddress.Value).LinkTarget;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or IOException)
        {
            return false;
        }

        if (!AbsolutePath.TryResolve(
                canonicalDirectoryPath,
                linkTarget,
                out var resolvedTarget,
                out _))
        {
            return false;
        }

        if (!IsGenerationAddress(resolvedTarget))
        {
            return false;
        }

        generationAddress = resolvedTarget;
        return true;
    }

    /// <summary> Deletes a validated abandoned generation node. </summary>
    public static void DeleteGenerationNode (AbsolutePath generationAddress)
    {
        ArgumentNullException.ThrowIfNull(generationAddress);
        if (!IsGenerationAddress(generationAddress))
        {
            throw new InvalidOperationException(
                $"Supervisor generation socket address is outside the owned endpoint boundary. Address={generationAddress}");
        }

        File.Delete(generationAddress.Value);
    }

    /// <summary> Deletes one canonical publication link before deleting its validated generation node. </summary>
    public static bool DeletePublishedGenerationIfPresent (
        AbsolutePath canonicalAddress,
        Action<AbsolutePath> deleteIfExists)
    {
        ArgumentNullException.ThrowIfNull(deleteIfExists);
        if (!TryResolvePublishedGenerationAddress(canonicalAddress, out var generationAddress))
        {
            return false;
        }

        File.Delete(canonicalAddress.Value);
        deleteIfExists(generationAddress);
        return true;
    }

    private void DeleteOwnedGenerationDirectoryIfEmpty ()
    {
        if (!Directory.Exists(generationFallbackPath.DirectoryPath.Value))
        {
            return;
        }

        using var enumerator = Directory.EnumerateFileSystemEntries(generationFallbackPath.DirectoryPath.Value).GetEnumerator();
        if (!enumerator.MoveNext())
        {
            Directory.Delete(generationFallbackPath.DirectoryPath.Value);
        }
    }

    private FileExclusiveLock AcquirePublicationLock ()
    {
        return FileExclusiveLock.Acquire(
            publicationLockPath,
            SupervisorConstants.ManifestMutationLockTimeout,
            CancellationToken.None);
    }

    private static bool IsGenerationAddress (AbsolutePath generationAddress)
    {
        if (!string.Equals(
                Path.GetFileName(generationAddress.Value),
                UcliIpcEndpointNames.UnixSocketFileName,
                StringComparison.Ordinal))
        {
            return false;
        }

        if (!generationAddress.TryGetParent(out var generationDirectoryPath)
            || !UnixSocketFallbackPath.IsDirectoryNameForPurpose(
                Path.GetFileName(generationDirectoryPath.Value),
                UnixSocketFallbackPurpose.SupervisorGeneration))
        {
            return false;
        }

        return generationDirectoryPath.TryGetParent(out var generationDirectoryParent)
            && generationDirectoryParent == TemporaryDirectoryPath;
    }
}
