using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor.Transport;

/// <summary> Persists worktree-local supervisor runtime metadata under <c>.ucli/local/supervisor</c>. </summary>
internal sealed class SupervisorManifestStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly Func<string, CancellationToken, ValueTask<string?>> readAllTextOrNull;

    private readonly Func<string, string, CancellationToken, ValueTask> writeAllTextAtomically;

    private readonly Action<string> deleteIfExists;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="SupervisorManifestStore" /> class. </summary>
    /// <param name="timeProvider"> The time provider used for timeout interpretation. </param>
    /// <param name="readAllTextOrNull"> Delegate that reads manifest JSON. </param>
    /// <param name="writeAllTextAtomically"> Delegate that writes manifest JSON atomically. </param>
    /// <param name="deleteIfExists"> Delegate that deletes a manifest file when present. </param>
    public SupervisorManifestStore (
        TimeProvider timeProvider,
        Func<string, CancellationToken, ValueTask<string?>> readAllTextOrNull,
        Func<string, string, CancellationToken, ValueTask> writeAllTextAtomically,
        Action<string> deleteIfExists)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.readAllTextOrNull = readAllTextOrNull ?? throw new ArgumentNullException(nameof(readAllTextOrNull));
        this.writeAllTextAtomically = writeAllTextAtomically ?? throw new ArgumentNullException(nameof(writeAllTextAtomically));
        this.deleteIfExists = deleteIfExists ?? throw new ArgumentNullException(nameof(deleteIfExists));
    }

    /// <summary> Reads one supervisor manifest when present. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The manifest when present; otherwise <see langword="null" />. </returns>
    public async ValueTask<SupervisorInstanceManifest?> ReadOrNullAsync (
        string storageRoot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(storageRoot);
        var json = await readAllTextOrNull(manifestPath, cancellationToken).ConfigureAwait(false);
        if (json == null)
        {
            return null;
        }

        try
        {
            return DeserializeAndValidate(json, manifestPath);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            throw new SupervisorManifestFormatException(
                $"Supervisor manifest JSON is invalid. {manifestPath}",
                ComputeArtifactIdentity(json),
                exception);
        }
    }

    /// <summary> Reads one supervisor manifest when present within the specified timeout budget. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="timeout"> The timeout budget for the read operation. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The manifest when present; otherwise <see langword="null" />. </returns>
    /// <exception cref="TimeoutException"> Thrown when the read operation exceeds <paramref name="timeout" />. </exception>
    public async ValueTask<SupervisorInstanceManifest?> ReadOrNullAsync (
        string storageRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var readOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before supervisor manifest read could begin.",
                $"Timed out while reading supervisor manifest. Timeout={timeout.TotalMilliseconds:0}ms.",
                token => ReadOrNullAsync(storageRoot, token))
            .ConfigureAwait(false);
        if (!readOperation.IsSuccess)
        {
            throw new TimeoutException(readOperation.Error!.Message);
        }

        return readOperation.Value;
    }

    /// <summary> Reads the manifest after any in-progress endpoint generation publication has completed. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="timeout"> The timeout shared by publication-lock acquisition and manifest reading. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The consistently published manifest when present; otherwise <see langword="null" />. </returns>
    /// <exception cref="TimeoutException"> Thrown when publication or reading exceeds <paramref name="timeout" />. </exception>
    public async ValueTask<SupervisorInstanceManifest?> ReadAfterEndpointPublicationAsync (
        string storageRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var manifestLockPath = UcliStoragePathResolver.ResolveSupervisorManifestLockPath(storageRoot);
        using var manifestLock = await FileExclusiveLock.AcquireAsync(
                manifestLockPath,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        var readOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                deadline,
                cancellationToken,
                "Timed out before consistently reading the published supervisor manifest.",
                "Timed out while consistently reading the published supervisor manifest.",
                token => ReadOrNullAsync(storageRoot, token))
            .ConfigureAwait(false);
        if (!readOperation.IsSuccess)
        {
            throw new TimeoutException(readOperation.Error!.Message);
        }

        return readOperation.Value;
    }

    /// <summary> Writes one supervisor manifest atomically. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="manifest"> The manifest to persist. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when persistence finishes. </returns>
    public async ValueTask WriteAsync (
        string storageRoot,
        SupervisorInstanceManifest manifest,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(manifest);

        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(storageRoot);
        var manifestLockPath = UcliStoragePathResolver.ResolveSupervisorManifestLockPath(storageRoot);
        using var manifestLock = await FileExclusiveLock.AcquireAsync(
                manifestLockPath,
                SupervisorConstants.ManifestMutationLockTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        await WriteWhileMutationLockIsHeldAsync(
                manifestPath,
                manifest,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary> Acquires exclusive ownership spanning endpoint bind and manifest publication. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="timeout"> The maximum time to wait for the manifest mutation lock. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> A lease that can publish the successor manifest before releasing mutation ownership. </returns>
    public async ValueTask<SupervisorEndpointPublicationLease> AcquireEndpointPublicationLeaseAsync (
        string storageRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(storageRoot);
        var manifestLockPath = UcliStoragePathResolver.ResolveSupervisorManifestLockPath(storageRoot);
        var manifestLock = await FileExclusiveLock.AcquireAsync(
                manifestLockPath,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        try
        {
            var replacedManifestJson = await readAllTextOrNull(manifestPath, cancellationToken)
                .ConfigureAwait(false);
            return new SupervisorEndpointPublicationLease(
                this,
                manifestPath,
                replacedManifestJson,
                manifestLock);
        }
        catch
        {
            manifestLock.Dispose();
            throw;
        }
    }

    /// <summary> Removes externally observed runtime state after acquiring exclusive runtime ownership. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="expectedManifest"> The exact manifest generation observed by the caller. </param>
    /// <param name="canonicalEndpoint"> The canonical endpoint resolved from <paramref name="storageRoot" />. </param>
    /// <param name="timeout"> The timeout shared by runtime ownership and manifest mutation lock acquisition. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The outcome of the compare-and-delete operation. </returns>
    public ValueTask<SupervisorManifestCleanupStatus> CleanupObservedRuntimeIfManifestMatchesAsync (
        string storageRoot,
        SupervisorInstanceManifest expectedManifest,
        IpcEndpoint canonicalEndpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedManifest);
        return CleanupObservedRuntimeIfArtifactMatchesAsync(
            storageRoot,
            json => IsExpectedManifest(storageRoot, json, expectedManifest),
            canonicalEndpoint,
            timeout,
            cancellationToken);
    }

    /// <summary> Removes externally observed malformed runtime state after acquiring exclusive runtime ownership. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="expectedArtifactIdentity"> The content identity captured when parsing failed. </param>
    /// <param name="canonicalEndpoint"> The canonical endpoint resolved from <paramref name="storageRoot" />. </param>
    /// <param name="timeout"> The timeout shared by runtime ownership and manifest mutation lock acquisition. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The outcome of the compare-and-delete operation. </returns>
    public ValueTask<SupervisorManifestCleanupStatus> CleanupObservedRuntimeIfMalformedArtifactMatchesAsync (
        string storageRoot,
        Sha256Digest expectedArtifactIdentity,
        IpcEndpoint canonicalEndpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedArtifactIdentity);
        return CleanupObservedRuntimeIfArtifactMatchesAsync(
            storageRoot,
            json => ComputeArtifactIdentity(json) == expectedArtifactIdentity,
            canonicalEndpoint,
            timeout,
            cancellationToken);
    }

    /// <summary> Removes runtime state owned by the calling supervisor host when its manifest generation still matches. </summary>
    /// <param name="storageRoot"> The storage-root path whose runtime ownership is already held by the caller. </param>
    /// <param name="expectedManifest"> The exact manifest generation published by the caller. </param>
    /// <param name="canonicalEndpoint"> The canonical endpoint resolved from <paramref name="storageRoot" />. </param>
    /// <param name="mutationLockTimeout"> The maximum time to wait for the manifest mutation lock. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The outcome of the compare-and-delete operation. </returns>
    /// <remarks> The caller must retain the storage root's runtime ownership lease until this operation completes. </remarks>
    public ValueTask<SupervisorManifestCleanupStatus> CleanupOwnedRuntimeIfManifestMatchesAsync (
        string storageRoot,
        SupervisorInstanceManifest expectedManifest,
        IpcEndpoint canonicalEndpoint,
        TimeSpan mutationLockTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedManifest);
        return CleanupOwnedRuntimeIfArtifactMatchesAsync(
            storageRoot,
            json => IsExpectedManifest(storageRoot, json, expectedManifest),
            canonicalEndpoint,
            mutationLockTimeout,
            cancellationToken);
    }

    private async ValueTask<SupervisorManifestCleanupStatus> CleanupObservedRuntimeIfArtifactMatchesAsync (
        string storageRoot,
        Func<string, bool> isExpectedArtifact,
        IpcEndpoint canonicalEndpoint,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(isExpectedArtifact);
        ArgumentNullException.ThrowIfNull(canonicalEndpoint);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        using var runtimeOwnership = await FileExclusiveLock.AcquireAsync(
                UcliStoragePathResolver.ResolveSupervisorRuntimeOwnershipLockPath(storageRoot),
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        if (!deadline.TryGetRemainingTimeout(out var manifestMutationTimeout))
        {
            throw new TimeoutException(
                $"Timed out before observed supervisor manifest mutation could begin. Timeout={timeout.TotalMilliseconds:0}ms.");
        }

        return await CleanupOwnedRuntimeIfArtifactMatchesAsync(
                storageRoot,
                isExpectedArtifact,
                canonicalEndpoint,
                manifestMutationTimeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<SupervisorManifestCleanupStatus> CleanupOwnedRuntimeIfArtifactMatchesAsync (
        string storageRoot,
        Func<string, bool> isExpectedArtifact,
        IpcEndpoint canonicalEndpoint,
        TimeSpan mutationLockTimeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(isExpectedArtifact);
        ArgumentNullException.ThrowIfNull(canonicalEndpoint);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(mutationLockTimeout, TimeSpan.Zero);

        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(storageRoot);
        var manifestLockPath = UcliStoragePathResolver.ResolveSupervisorManifestLockPath(storageRoot);
        using var manifestLock = await FileExclusiveLock.AcquireAsync(
                manifestLockPath,
                mutationLockTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var currentJson = await readAllTextOrNull(manifestPath, cancellationToken).ConfigureAwait(false);
        if (currentJson == null)
        {
            return SupervisorManifestCleanupStatus.Missing;
        }

        if (!isExpectedArtifact(currentJson))
        {
            return SupervisorManifestCleanupStatus.GenerationMismatch;
        }

        if (canonicalEndpoint.TransportKind == IpcTransportKind.UnixDomainSocket)
        {
            if (!SupervisorUnixSocketEndpointOwnership.DeletePublishedGenerationIfPresent(
                    canonicalEndpoint.Address,
                    deleteIfExists))
            {
                deleteIfExists(canonicalEndpoint.Address);
            }

            UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
                canonicalEndpoint.Address,
                UcliIpcEndpointNames.SupervisorAddressPrefix);
        }

        deleteIfExists(manifestPath);
        return SupervisorManifestCleanupStatus.Removed;
    }

    private static bool IsExpectedManifest (
        string storageRoot,
        string json,
        SupervisorInstanceManifest expectedManifest)
    {
        try
        {
            var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(storageRoot);
            return DeserializeAndValidate(json, manifestPath) == expectedManifest;
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return false;
        }
    }

    private static SupervisorInstanceManifest DeserializeAndValidate (
        string json,
        string manifestPath)
    {
        var contract = JsonSerializer.Deserialize<SupervisorInstanceManifestJsonContract>(json, SerializerOptions)
            ?? throw new JsonException("Supervisor manifest JSON is null.");
        Validate(contract, manifestPath);
        if (!IpcSessionToken.TryParse(contract.SessionToken, out var sessionToken))
        {
            throw new InvalidDataException($"Supervisor manifest sessionToken is invalid. {manifestPath}");
        }

        if (!ContractLiteralCodec.TryParse<IpcTransportKind>(contract.EndpointTransportKind, out var transportKind))
        {
            throw new InvalidDataException(
                $"Supervisor manifest endpointTransportKind is invalid: {contract.EndpointTransportKind}. {manifestPath}");
        }

        IpcEndpoint endpoint;
        try
        {
            endpoint = new IpcEndpoint(transportKind, contract.EndpointAddress!);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                $"Supervisor manifest endpoint is invalid. {manifestPath}",
                exception);
        }

        return new SupervisorInstanceManifest(
            contract.ProcessId,
            sessionToken,
            endpoint,
            contract.IssuedAtUtc);
    }

    private async ValueTask WriteWhileMutationLockIsHeldAsync (
        string manifestPath,
        SupervisorInstanceManifest manifest,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(manifest);

        var contract = new SupervisorInstanceManifestJsonContract(
            manifest.ProcessId,
            manifest.SessionToken.GetEncodedValue(),
            ContractLiteralCodec.ToValue(manifest.Endpoint.TransportKind),
            manifest.Endpoint.Address,
            manifest.IssuedAtUtc);
        var json = JsonSerializer.Serialize(contract, SerializerOptions) + Environment.NewLine;
        var manifestDirectoryPath = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidOperationException($"Supervisor manifest directory path could not be resolved: {manifestPath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(manifestDirectoryPath);
        await writeAllTextAtomically(manifestPath, json, cancellationToken).ConfigureAwait(false);
        FileSystemAccessBoundary.EnsureSecureFile(manifestPath);
    }

    private async ValueTask RestoreWhileMutationLockIsHeldAsync (
        string manifestPath,
        string? replacedManifestJson)
    {
        if (replacedManifestJson is null)
        {
            deleteIfExists(manifestPath);
            return;
        }

        var manifestDirectoryPath = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidOperationException($"Supervisor manifest directory path could not be resolved: {manifestPath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(manifestDirectoryPath);
        try
        {
            await writeAllTextAtomically(manifestPath, replacedManifestJson, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception writeException)
        {
            var restoredJson = await readAllTextOrNull(manifestPath, CancellationToken.None)
                .ConfigureAwait(false);
            if (!string.Equals(restoredJson, replacedManifestJson, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Supervisor manifest publication rollback did not restore the replaced artifact.",
                    writeException);
            }
        }

        FileSystemAccessBoundary.EnsureSecureFile(manifestPath);
    }

    private static Sha256Digest ComputeArtifactIdentity (string json)
    {
        return Sha256Digest.Compute(Encoding.UTF8.GetBytes(json));
    }

    private static void Validate (
        SupervisorInstanceManifestJsonContract manifest,
        string manifestPath)
    {
        if (manifest.ProcessId <= 0)
        {
            throw new InvalidDataException($"Supervisor manifest processId must be greater than zero. {manifestPath}");
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(manifest.EndpointAddress, out _)
            || !StringValueNormalizer.TryTrimToNonEmpty(manifest.EndpointTransportKind, out _))
        {
            throw new InvalidDataException($"Supervisor manifest contains required empty values. {manifestPath}");
        }

        if (manifest.IssuedAtUtc == default)
        {
            throw new InvalidDataException($"Supervisor manifest issuedAtUtc is invalid. {manifestPath}");
        }

    }

    internal sealed class SupervisorEndpointPublicationLease : IDisposable
    {
        private readonly SupervisorManifestStore owner;

        private readonly string manifestPath;

        private readonly string? replacedManifestJson;

        private FileExclusiveLock? manifestLock;

        public SupervisorEndpointPublicationLease (
            SupervisorManifestStore owner,
            string manifestPath,
            string? replacedManifestJson,
            FileExclusiveLock manifestLock)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.manifestPath = !string.IsNullOrWhiteSpace(manifestPath)
                ? manifestPath
                : throw new ArgumentException("Manifest path must not be empty.", nameof(manifestPath));
            this.replacedManifestJson = replacedManifestJson;
            this.manifestLock = manifestLock ?? throw new ArgumentNullException(nameof(manifestLock));
        }

        public async ValueTask PublishAsync (
            SupervisorInstanceManifest manifest,
            CancellationToken cancellationToken = default)
        {
            if (Volatile.Read(ref manifestLock) == null)
            {
                throw new ObjectDisposedException(nameof(SupervisorEndpointPublicationLease));
            }

            try
            {
                await owner.WriteWhileMutationLockIsHeldAsync(
                        manifestPath,
                        manifest,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception publicationException)
            {
                try
                {
                    await owner.RestoreWhileMutationLockIsHeldAsync(
                            manifestPath,
                            replacedManifestJson)
                        .ConfigureAwait(false);
                }
                catch (Exception rollbackException)
                {
                    throw new InvalidOperationException(
                        "Supervisor manifest publication failed and the replaced artifact could not be restored.",
                        new AggregateException(publicationException, rollbackException));
                }

                ExceptionDispatchInfo.Capture(publicationException).Throw();
            }
        }

        public void Dispose ()
        {
            Interlocked.Exchange(ref manifestLock, null)?.Dispose();
        }
    }
}

/// <summary> Identifies the outcome of a supervisor runtime compare-and-delete operation. </summary>
internal enum SupervisorManifestCleanupStatus
{
    /// <summary> The expected artifact was current and its runtime state was removed. </summary>
    Removed,

    /// <summary> No manifest artifact existed while the mutation lock was held. </summary>
    Missing,

    /// <summary> A different manifest generation replaced the expected artifact. </summary>
    GenerationMismatch,
}

/// <summary> Reports malformed supervisor metadata together with its immutable content identity. </summary>
internal sealed class SupervisorManifestFormatException : Exception
{
    public SupervisorManifestFormatException (
        string message,
        Sha256Digest artifactIdentity,
        Exception innerException)
        : base(message, innerException)
    {
        ArtifactIdentity = artifactIdentity ?? throw new ArgumentNullException(nameof(artifactIdentity));
    }

    /// <summary> Gets the SHA-256 identity of the exact malformed file contents that failed parsing. </summary>
    public Sha256Digest ArtifactIdentity { get; }
}
