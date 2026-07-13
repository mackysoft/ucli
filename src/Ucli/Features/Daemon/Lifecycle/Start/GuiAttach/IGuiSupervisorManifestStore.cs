using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Reads the project-scoped GUI supervisor manifest used for endpoint rebootstrap. </summary>
internal interface IGuiSupervisorManifestStore
{
    /// <summary> Reads the manifest after any in-progress endpoint publication has completed. </summary>
    ValueTask<GuiSupervisorManifestJsonContract?> ReadAfterEndpointPublicationAsync (
        string storageRoot,
        ProjectFingerprint projectFingerprint,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
