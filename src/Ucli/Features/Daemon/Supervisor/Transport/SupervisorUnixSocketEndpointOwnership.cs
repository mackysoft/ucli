using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Owns one generation-specific supervisor Unix socket node and its canonical publication link. </summary>
internal sealed class SupervisorUnixSocketEndpointOwnership
{
    private const string GenerationDirectoryPrefix = "ucli-supervisor-generation-";

    private const string PublicationLockDirectoryPrefix = "ucli-supervisor-publication-lock-";

    private readonly string canonicalAddress;

    private readonly string publicationLockPath;

    private readonly UnixSocketAccessBoundary generationAccessBoundary;

    private readonly string publicationToken = Guid.NewGuid().ToString("N");

    private string? replacedGenerationAddress;

    private bool endpointPublished;

    private bool publicationCommitted;

    public SupervisorUnixSocketEndpointOwnership (string canonicalAddress)
    {
        if (string.IsNullOrWhiteSpace(canonicalAddress))
        {
            throw new ArgumentException("Canonical Unix socket address must not be empty.", nameof(canonicalAddress));
        }

        this.canonicalAddress = Path.GetFullPath(canonicalAddress);
        BoundAddress = UnixSocketPathUtilities.BuildFallbackSocketPath(
            GenerationDirectoryPrefix,
            $"{this.canonicalAddress}\n{publicationToken}");
        generationAccessBoundary = new UnixSocketAccessBoundary(
            BoundAddress,
            GenerationDirectoryPrefix);
        publicationLockPath = Path.ChangeExtension(
            UnixSocketPathUtilities.BuildFallbackSocketPath(
                PublicationLockDirectoryPrefix,
                this.canonicalAddress),
            ".lock");
    }

    /// <summary> Gets the generation-specific path that the listener must bind. </summary>
    public string BoundAddress { get; }

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
        var canonicalDirectoryPath = Path.GetDirectoryName(canonicalAddress)
            ?? throw new InvalidOperationException(
                $"Canonical Unix socket directory could not be resolved. Address={canonicalAddress}");
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
            && !PathIdentity.IsSamePath(retiredGenerationAddress, BoundAddress))
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

    private void ReplaceCanonicalLink (string targetAddress)
    {
        var canonicalDirectoryPath = Path.GetDirectoryName(canonicalAddress)
            ?? throw new InvalidOperationException(
                $"Canonical Unix socket directory could not be resolved. Address={canonicalAddress}");
        var temporaryLinkPath = Path.Combine(
            canonicalDirectoryPath,
            $".{Path.GetFileName(canonicalAddress)}.{publicationToken}.link");
        try
        {
            File.Delete(temporaryLinkPath);
            File.CreateSymbolicLink(temporaryLinkPath, targetAddress);
            File.Move(temporaryLinkPath, canonicalAddress, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryLinkPath);
        }
    }

    /// <summary> Removes this generation without deleting a successor's canonical publication. </summary>
    public void Cleanup ()
    {
        try
        {
            using var publicationLock = AcquirePublicationLock();
            if (TryResolvePublishedGenerationAddress(canonicalAddress, out var publishedGenerationAddress)
                && PathIdentity.IsSamePath(publishedGenerationAddress, BoundAddress))
            {
                if (!publicationCommitted
                    && replacedGenerationAddress is not null
                    && File.Exists(replacedGenerationAddress))
                {
                    ReplaceCanonicalLink(replacedGenerationAddress);
                }
                else
                {
                    File.Delete(canonicalAddress);
                }

                UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
                    canonicalAddress,
                    UcliIpcEndpointNames.SupervisorAddressPrefix);
            }
        }
        catch (TimeoutException)
        {
            // A publisher holding the lock owns the canonical path decision. Never delete it without that decision.
        }
        finally
        {
            generationAccessBoundary.Cleanup();
        }
    }

    /// <summary> Resolves a validated generation-specific target from one canonical publication link. </summary>
    public static bool TryResolvePublishedGenerationAddress (
        string canonicalAddress,
        out string generationAddress)
    {
        generationAddress = string.Empty;
        if (string.IsNullOrWhiteSpace(canonicalAddress))
        {
            return false;
        }

        string? linkTarget;
        try
        {
            linkTarget = new FileInfo(canonicalAddress).LinkTarget;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or IOException)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(linkTarget))
        {
            return false;
        }

        var canonicalDirectoryPath = Path.GetDirectoryName(Path.GetFullPath(canonicalAddress));
        if (string.IsNullOrWhiteSpace(canonicalDirectoryPath))
        {
            return false;
        }

        var resolvedTarget = Path.IsPathFullyQualified(linkTarget)
            ? Path.GetFullPath(linkTarget)
            : Path.GetFullPath(linkTarget, canonicalDirectoryPath);
        if (!IsGenerationAddress(resolvedTarget))
        {
            return false;
        }

        generationAddress = resolvedTarget;
        return true;
    }

    /// <summary> Deletes a validated abandoned generation node and its empty fallback directory. </summary>
    public static void DeleteGenerationNode (string generationAddress)
    {
        if (!IsGenerationAddress(generationAddress))
        {
            throw new InvalidOperationException(
                $"Supervisor generation socket address is outside the owned endpoint boundary. Address={generationAddress}");
        }

        File.Delete(generationAddress);
        UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
            generationAddress,
            GenerationDirectoryPrefix);
    }

    /// <summary> Deletes one canonical publication link before deleting its validated generation node. </summary>
    public static bool DeletePublishedGenerationIfPresent (
        string canonicalAddress,
        Action<string> deleteIfExists)
    {
        ArgumentNullException.ThrowIfNull(deleteIfExists);
        if (!TryResolvePublishedGenerationAddress(canonicalAddress, out var generationAddress))
        {
            return false;
        }

        File.Delete(canonicalAddress);
        deleteIfExists(generationAddress);
        UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
            generationAddress,
            GenerationDirectoryPrefix);
        return true;
    }

    private FileExclusiveLock AcquirePublicationLock ()
    {
        return FileExclusiveLock.Acquire(
            publicationLockPath,
            SupervisorConstants.ManifestMutationLockTimeout,
            CancellationToken.None);
    }

    private static bool IsGenerationAddress (string generationAddress)
    {
        if (string.IsNullOrWhiteSpace(generationAddress)
            || !string.Equals(
                Path.GetFileName(generationAddress),
                UcliIpcEndpointNames.UnixSocketFileName,
                StringComparison.Ordinal))
        {
            return false;
        }

        var normalizedGenerationAddress = Path.GetFullPath(generationAddress);
        var generationDirectoryPath = Path.GetDirectoryName(normalizedGenerationAddress);
        if (string.IsNullOrWhiteSpace(generationDirectoryPath)
            || !Path.GetFileName(generationDirectoryPath).StartsWith(
                GenerationDirectoryPrefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        var generationDirectoryParent = Path.GetDirectoryName(generationDirectoryPath);
        return !string.IsNullOrWhiteSpace(generationDirectoryParent)
            && PathIdentity.IsSamePath(generationDirectoryParent, Path.GetTempPath());
    }
}
