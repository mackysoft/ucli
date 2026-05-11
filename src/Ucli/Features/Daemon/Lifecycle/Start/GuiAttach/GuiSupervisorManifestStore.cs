using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Reads persisted GUI supervisor manifests for existing GUI Editor attach. </summary>
internal sealed class GuiSupervisorManifestStore
{
    /// <summary> Reads a GUI supervisor manifest when one exists. </summary>
    public async ValueTask<GuiSupervisorManifest?> ReadOrNullAsync (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(storageRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFingerprint);

        var manifestPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(
            storageRoot,
            projectFingerprint);
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        await using var stream = File.OpenRead(manifestPath);
        return await JsonSerializer.DeserializeAsync<GuiSupervisorManifest>(
                stream,
                IpcJsonSerializerOptions.Default,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
