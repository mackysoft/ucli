using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.Shared.Execution.Timeout;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Shared.Storage;

namespace MackySoft.Ucli.Features.Daemon.Supervisor;

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
    public SupervisorManifestStore ()
        : this(
            static (path, cancellationToken) => FileUtilities.ReadAllTextOrNull(path, cancellationToken),
            static (path, contents, cancellationToken) => FileUtilities.WriteAllTextAtomically(path, contents, cancellationToken),
            static path => FileUtilities.DeleteIfExists(path),
            timeProvider: null)
    {
    }

    /// <summary> Initializes a new instance of the <see cref="SupervisorManifestStore" /> class for tests. </summary>
    /// <param name="readAllTextOrNull"> Delegate that reads manifest JSON. </param>
    /// <param name="writeAllTextAtomically"> Delegate that writes manifest JSON atomically. </param>
    /// <param name="deleteIfExists"> Delegate that deletes a manifest file when present. </param>
    /// <param name="timeProvider"> The time provider used for timeout interpretation. </param>
    internal SupervisorManifestStore (
        Func<string, CancellationToken, ValueTask<string?>> readAllTextOrNull,
        Func<string, string, CancellationToken, ValueTask> writeAllTextAtomically,
        Action<string> deleteIfExists,
        TimeProvider? timeProvider = null)
    {
        this.readAllTextOrNull = readAllTextOrNull ?? throw new ArgumentNullException(nameof(readAllTextOrNull));
        this.writeAllTextAtomically = writeAllTextAtomically ?? throw new ArgumentNullException(nameof(writeAllTextAtomically));
        this.deleteIfExists = deleteIfExists ?? throw new ArgumentNullException(nameof(deleteIfExists));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary> Reads one supervisor manifest when present. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The manifest when present; otherwise <see langword="null" />. </returns>
    public async ValueTask<SupervisorInstanceManifest?> ReadOrNull (
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

        var manifest = JsonSerializer.Deserialize<SupervisorInstanceManifest>(json, SerializerOptions)
            ?? throw new JsonException("Supervisor manifest JSON is null.");
        Validate(manifest, manifestPath);
        return manifest;
    }

    /// <summary> Reads one supervisor manifest when present within the specified timeout budget. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="timeout"> The timeout budget for the read operation. Must be greater than <see cref="TimeSpan.Zero" />. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The manifest when present; otherwise <see langword="null" />. </returns>
    /// <exception cref="TimeoutException"> Thrown when the read operation exceeds <paramref name="timeout" />. </exception>
    public async ValueTask<SupervisorInstanceManifest?> ReadOrNull (
        string storageRoot,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        using var timeoutCancellationScope = TimeProviderCancellationScope.CreateLinked(
            cancellationToken,
            timeout,
            timeProvider);

        try
        {
            return await ReadOrNull(storageRoot, timeoutCancellationScope.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested
                                                 && timeoutCancellationScope.HasTimedOut)
        {
            throw new TimeoutException(
                $"Timed out while reading supervisor manifest. Timeout={timeout.TotalMilliseconds:0}ms.");
        }
    }

    /// <summary> Writes one supervisor manifest atomically. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    /// <param name="manifest"> The manifest to persist. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that completes when persistence finishes. </returns>
    public async ValueTask Write (
        string storageRoot,
        SupervisorInstanceManifest manifest,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(manifest);

        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(storageRoot);
        Validate(manifest, manifestPath);
        var json = JsonSerializer.Serialize(manifest, SerializerOptions) + Environment.NewLine;
        var manifestDirectoryPath = Path.GetDirectoryName(manifestPath)
            ?? throw new InvalidOperationException($"Supervisor manifest directory path could not be resolved: {manifestPath}");
        FileSystemAccessBoundary.EnsureSecureDirectory(manifestDirectoryPath);
        await writeAllTextAtomically(manifestPath, json, cancellationToken).ConfigureAwait(false);
        FileSystemAccessBoundary.EnsureSecureFile(manifestPath);
    }

    /// <summary> Deletes the persisted supervisor manifest when it exists. </summary>
    /// <param name="storageRoot"> The storage-root path. </param>
    public void DeleteIfExists (string storageRoot)
    {
        var manifestPath = UcliStoragePathResolver.ResolveSupervisorManifestPath(storageRoot);
        deleteIfExists(manifestPath);
    }

    private static void Validate (
        SupervisorInstanceManifest manifest,
        string manifestPath)
    {
        if (manifest.ProcessId <= 0)
        {
            throw new InvalidDataException($"Supervisor manifest processId must be greater than zero. {manifestPath}");
        }

        if (!StringValueNormalizer.TryTrimToNonEmpty(manifest.SessionToken, out _)
            || !StringValueNormalizer.TryTrimToNonEmpty(manifest.EndpointAddress, out _)
            || !StringValueNormalizer.TryTrimToNonEmpty(manifest.EndpointTransportKind, out _))
        {
            throw new InvalidDataException($"Supervisor manifest contains required empty values. {manifestPath}");
        }

        if (manifest.IssuedAtUtc == default)
        {
            throw new InvalidDataException($"Supervisor manifest issuedAtUtc is invalid. {manifestPath}");
        }

        if (!IpcTransportKindCodec.TryParse(manifest.EndpointTransportKind, out _))
        {
            throw new InvalidDataException(
                $"Supervisor manifest endpointTransportKind is invalid: {manifest.EndpointTransportKind}. {manifestPath}");
        }
    }
}