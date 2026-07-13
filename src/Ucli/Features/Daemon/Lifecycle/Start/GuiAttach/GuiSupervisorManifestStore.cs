using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Reads persisted GUI supervisor manifests for existing GUI Editor attach. </summary>
internal sealed class GuiSupervisorManifestStore : IGuiSupervisorManifestStore
{
    private static async ValueTask<GuiSupervisorManifestJsonContract?> ReadWithoutPublicationLockAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentNullException.ThrowIfNull(projectFingerprint);

        var manifestPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(
            storageRoot,
            projectFingerprint);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var json = await FileUtilities.ReadAllTextOrNullAsync(manifestPath, cancellationToken).ConfigureAwait(false);
        return json is null
            ? null
            : GuiSupervisorManifestJsonContractSerializer.Deserialize(json);
    }

    /// <inheritdoc />
    public async ValueTask<GuiSupervisorManifestJsonContract?> ReadAfterEndpointPublicationAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentNullException.ThrowIfNull(projectFingerprint);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        var manifestLockPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestLockPath(
            storageRoot,
            projectFingerprint);
        using var manifestLock = await FileExclusiveLock.AcquireAsync(
                manifestLockPath,
                timeout,
                cancellationToken)
            .ConfigureAwait(false);
        return await ReadWithoutPublicationLockAsync(
                storageRoot,
                projectFingerprint,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
