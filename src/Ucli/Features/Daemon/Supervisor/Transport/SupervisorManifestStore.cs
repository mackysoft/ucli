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

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    private readonly Func<string, CancellationToken, ValueTask<ReadOnlyMemory<byte>?>> readAllBytesOrNull;

    private readonly Func<string, ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeAllBytesAtomically;

    private readonly Action<string> deleteIfExists;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes a new instance of the <see cref="SupervisorManifestStore" /> class. </summary>
    /// <param name="timeProvider"> The time provider used for timeout interpretation. </param>
    /// <param name="readAllBytesOrNull"> Delegate that reads exact manifest bytes into newly owned read-only memory. </param>
    /// <param name="writeAllBytesAtomically"> Delegate that writes exact manifest bytes atomically. </param>
    /// <param name="deleteIfExists"> Delegate that deletes a manifest file when present. </param>
    public SupervisorManifestStore (
        TimeProvider timeProvider,
        Func<string, CancellationToken, ValueTask<ReadOnlyMemory<byte>?>> readAllBytesOrNull,
        Func<string, ReadOnlyMemory<byte>, CancellationToken, ValueTask> writeAllBytesAtomically,
        Action<string> deleteIfExists)
    {
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        this.readAllBytesOrNull = readAllBytesOrNull ?? throw new ArgumentNullException(nameof(readAllBytesOrNull));
        this.writeAllBytesAtomically = writeAllBytesAtomically ?? throw new ArgumentNullException(nameof(writeAllBytesAtomically));
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
        var artifact = await ReadArtifactOrNullAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        if (artifact == null)
        {
            return null;
        }

        try
        {
            return DeserializeAndValidate(artifact.DecodeUtf8Json(), manifestPath);
        }
        catch (Exception exception) when (exception is DecoderFallbackException or JsonException or InvalidDataException)
        {
            throw new SupervisorManifestFormatException(
                $"Supervisor manifest content is not valid UTF-8 JSON. {manifestPath}",
                artifact.Identity,
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
            var replacedManifestArtifact = await ReadArtifactOrNullAsync(manifestPath, cancellationToken)
                .ConfigureAwait(false);
            return new SupervisorEndpointPublicationLease(
                this,
                manifestPath,
                replacedManifestArtifact,
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
    /// <param name="unixSocketCleanupTarget"> The canonical Unix socket cleanup target, or <see langword="null" /> when no filesystem endpoint exists. </param>
    /// <param name="timeout"> The timeout shared by runtime ownership and manifest mutation lock acquisition. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The outcome of the compare-and-delete operation. </returns>
    public ValueTask<SupervisorManifestCleanupStatus> CleanupObservedRuntimeIfManifestMatchesAsync (
        string storageRoot,
        SupervisorInstanceManifest expectedManifest,
        SupervisorUnixSocketCleanupTarget? unixSocketCleanupTarget,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedManifest);
        return CleanupObservedRuntimeIfArtifactMatchesAsync(
            storageRoot,
            artifact => IsExpectedManifest(storageRoot, artifact, expectedManifest),
            unixSocketCleanupTarget,
            timeout,
            cancellationToken);
    }

    /// <summary> Removes externally observed malformed runtime state after acquiring exclusive runtime ownership. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="expectedArtifactIdentity"> The content identity captured when parsing failed. </param>
    /// <param name="unixSocketCleanupTarget"> The canonical Unix socket cleanup target, or <see langword="null" /> when no filesystem endpoint exists. </param>
    /// <param name="timeout"> The timeout shared by runtime ownership and manifest mutation lock acquisition. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The outcome of the compare-and-delete operation. </returns>
    public ValueTask<SupervisorManifestCleanupStatus> CleanupObservedRuntimeIfMalformedArtifactMatchesAsync (
        string storageRoot,
        Sha256Digest expectedArtifactIdentity,
        SupervisorUnixSocketCleanupTarget? unixSocketCleanupTarget,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedArtifactIdentity);
        return CleanupObservedRuntimeIfArtifactMatchesAsync(
            storageRoot,
            artifact => artifact.Identity == expectedArtifactIdentity,
            unixSocketCleanupTarget,
            timeout,
            cancellationToken);
    }

    /// <summary> Removes runtime state owned by the calling supervisor host when its manifest generation still matches. </summary>
    /// <param name="storageRoot"> The storage-root path whose runtime ownership is already held by the caller. </param>
    /// <param name="expectedManifest"> The exact manifest generation published by the caller. </param>
    /// <param name="unixSocketCleanupTarget"> The canonical Unix socket cleanup target, or <see langword="null" /> when no filesystem endpoint exists. </param>
    /// <param name="mutationLockTimeout"> The maximum time to wait for the manifest mutation lock. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by the caller. </param>
    /// <returns> The outcome of the compare-and-delete operation. </returns>
    /// <remarks> The caller must retain the storage root's runtime ownership lease until this operation completes. </remarks>
    public ValueTask<SupervisorManifestCleanupStatus> CleanupOwnedRuntimeIfManifestMatchesAsync (
        string storageRoot,
        SupervisorInstanceManifest expectedManifest,
        SupervisorUnixSocketCleanupTarget? unixSocketCleanupTarget,
        TimeSpan mutationLockTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expectedManifest);
        return CleanupOwnedRuntimeIfArtifactMatchesAsync(
            storageRoot,
            artifact => IsExpectedManifest(storageRoot, artifact, expectedManifest),
            unixSocketCleanupTarget,
            mutationLockTimeout,
            cancellationToken);
    }

    private async ValueTask<SupervisorManifestCleanupStatus> CleanupObservedRuntimeIfArtifactMatchesAsync (
        string storageRoot,
        Func<SupervisorManifestArtifact, bool> isExpectedArtifact,
        SupervisorUnixSocketCleanupTarget? unixSocketCleanupTarget,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(isExpectedArtifact);
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
                unixSocketCleanupTarget,
                manifestMutationTimeout,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<SupervisorManifestCleanupStatus> CleanupOwnedRuntimeIfArtifactMatchesAsync (
        string storageRoot,
        Func<SupervisorManifestArtifact, bool> isExpectedArtifact,
        SupervisorUnixSocketCleanupTarget? unixSocketCleanupTarget,
        TimeSpan mutationLockTimeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(isExpectedArtifact);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(mutationLockTimeout, TimeSpan.Zero);

        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(storageRoot);
        var manifestLockPath = UcliStoragePathResolver.ResolveSupervisorManifestLockPath(storageRoot);
        using var manifestLock = await FileExclusiveLock.AcquireAsync(
                manifestLockPath,
                mutationLockTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var currentArtifact = await ReadArtifactOrNullAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        if (currentArtifact == null)
        {
            return SupervisorManifestCleanupStatus.Missing;
        }

        if (!isExpectedArtifact(currentArtifact))
        {
            return SupervisorManifestCleanupStatus.GenerationMismatch;
        }

        if (unixSocketCleanupTarget is not null)
        {
            if (!SupervisorUnixSocketEndpointOwnership.DeletePublishedGenerationIfPresent(
                    unixSocketCleanupTarget.SocketPath,
                    deleteIfExists))
            {
                deleteIfExists(unixSocketCleanupTarget.SocketPath);
            }

            UnixSocketPathUtilities.DeleteEmptyFallbackDirectoryIfPresent(
                unixSocketCleanupTarget.SocketPath,
                UcliIpcEndpointNames.SupervisorAddressPrefix);
        }

        deleteIfExists(manifestPath);
        return SupervisorManifestCleanupStatus.Removed;
    }

    private static bool IsExpectedManifest (
        string storageRoot,
        SupervisorManifestArtifact artifact,
        SupervisorInstanceManifest expectedManifest)
    {
        try
        {
            var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(storageRoot);
            return DeserializeAndValidate(artifact.DecodeUtf8Json(), manifestPath) == expectedManifest;
        }
        catch (Exception exception) when (exception is DecoderFallbackException or JsonException or InvalidDataException)
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
        var bytes = StrictUtf8.GetBytes(json);
        var manifestDirectoryPath = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidOperationException($"Supervisor manifest directory path could not be resolved: {manifestPath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(manifestDirectoryPath);
        await writeAllBytesAtomically(manifestPath, bytes, cancellationToken).ConfigureAwait(false);
        FileSystemAccessBoundary.EnsureSecureFile(manifestPath);
    }

    private async ValueTask RestoreWhileMutationLockIsHeldAsync (
        string manifestPath,
        SupervisorManifestArtifact? replacedManifestArtifact)
    {
        if (replacedManifestArtifact is null)
        {
            deleteIfExists(manifestPath);
            return;
        }

        var manifestDirectoryPath = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidOperationException($"Supervisor manifest directory path could not be resolved: {manifestPath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(manifestDirectoryPath);
        try
        {
            await writeAllBytesAtomically(
                    manifestPath,
                    replacedManifestArtifact.CreateWriteCopy(),
                    CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception writeException)
        {
            var restoredArtifact = await ReadArtifactOrNullAsync(manifestPath, CancellationToken.None)
                .ConfigureAwait(false);
            if (!replacedManifestArtifact.ContentEquals(restoredArtifact))
            {
                throw new InvalidOperationException(
                    "Supervisor manifest publication rollback did not restore the replaced artifact.",
                    writeException);
            }
        }

        FileSystemAccessBoundary.EnsureSecureFile(manifestPath);
    }

    private async ValueTask<SupervisorManifestArtifact?> ReadArtifactOrNullAsync (
        string manifestPath,
        CancellationToken cancellationToken)
    {
        var externalBytes = await readAllBytesOrNull(manifestPath, cancellationToken).ConfigureAwait(false);
        return externalBytes is null
            ? null
            : new SupervisorManifestArtifact(externalBytes.Value.Span);
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

    /// <summary> Owns one immutable snapshot of exact manifest file bytes and its content identity. </summary>
    internal sealed class SupervisorManifestArtifact
    {
        private readonly ReadOnlyMemory<byte> bytes;

        public SupervisorManifestArtifact (ReadOnlySpan<byte> externalBytes)
        {
            bytes = externalBytes.ToArray();
            Identity = Sha256Digest.Compute(bytes.Span);
        }

        public Sha256Digest Identity { get; }

        public string DecodeUtf8Json ()
        {
            var utf8Bytes = bytes.Span;

            // NOTE:
            // A single UTF-8 BOM is accepted for decoding, while the artifact identity and rollback
            // retain those three bytes. Other encodings and malformed UTF-8 are rejected strictly.
            if (utf8Bytes.Length >= 3
                && utf8Bytes[0] == 0xEF
                && utf8Bytes[1] == 0xBB
                && utf8Bytes[2] == 0xBF)
            {
                utf8Bytes = utf8Bytes[3..];
            }

            return StrictUtf8.GetString(utf8Bytes);
        }

        public ReadOnlyMemory<byte> CreateWriteCopy ()
        {
            return bytes.ToArray();
        }

        public bool ContentEquals (SupervisorManifestArtifact? other)
        {
            return other is not null && bytes.Span.SequenceEqual(other.bytes.Span);
        }
    }

    internal sealed class SupervisorEndpointPublicationLease : IDisposable
    {
        private readonly SupervisorManifestStore owner;

        private readonly string manifestPath;

        private readonly SupervisorManifestArtifact? replacedManifestArtifact;

        private FileExclusiveLock? manifestLock;

        internal SupervisorEndpointPublicationLease (
            SupervisorManifestStore owner,
            string manifestPath,
            SupervisorManifestArtifact? replacedManifestArtifact,
            FileExclusiveLock manifestLock)
        {
            this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
            this.manifestPath = !string.IsNullOrWhiteSpace(manifestPath)
                ? manifestPath
                : throw new ArgumentException("Manifest path must not be empty.", nameof(manifestPath));
            this.replacedManifestArtifact = replacedManifestArtifact;
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
                            replacedManifestArtifact)
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
