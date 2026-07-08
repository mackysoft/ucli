using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Requests daemon endpoint rebootstrap from a project-scoped GUI supervisor endpoint. </summary>
internal sealed class DaemonGuiRebootstrapClient : IDaemonGuiRebootstrapClient
{
    private readonly GuiSupervisorManifestStore manifestStore;

    private readonly IIpcTransportClient transportClient;

    public DaemonGuiRebootstrapClient (
        GuiSupervisorManifestStore manifestStore,
        IIpcTransportClient transportClient)
    {
        this.manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
    }

    /// <inheritdoc />
    public async ValueTask<DaemonGuiRebootstrapRequestResult> RequestRebootstrapAsync (
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expectedProcessId, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        GuiSupervisorManifestJsonContract? manifest;
        try
        {
            manifest = await manifestStore.ReadOrNullAsync(
                    unityProject.RepositoryRoot,
                    unityProject.ProjectFingerprint,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return DaemonGuiRebootstrapRequestResult.Unavailable(ExecutionError.InternalError(
                $"GUI supervisor manifest could not be read. {exception.Message}",
                DaemonErrorCodes.DaemonEndpointNotRegistered));
        }

        var validationError = ValidateManifest(
            manifest,
            unityProject,
            expectedProcessId,
            expectedProcessStartedAtUtc);
        if (validationError != null)
        {
            return DaemonGuiRebootstrapRequestResult.Unavailable(validationError);
        }

        try
        {
            var request = new IpcRequest(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                RequestId: $"gui-rebootstrap-{Guid.NewGuid():N}",
                SessionToken: manifest!.SessionToken,
                Method: IpcMethodNames.GuiRebootstrap,
                Payload: IpcPayloadCodec.SerializeToElement(new IpcGuiRebootstrapRequest(
                    ProjectFingerprint: unityProject.ProjectFingerprint,
                    ReplaceExistingSession: true)),
                responseMode: IpcResponseMode.Single);
            var response = await transportClient.SendAsync(
                    ResolveEndpoint(manifest),
                    request,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (IpcResponseFailureReader.TryRead(response, out var firstError, out _))
            {
                return DaemonGuiRebootstrapRequestResult.Unavailable(ExecutionError.InternalError(
                    firstError?.Message ?? "GUI supervisor rebootstrap request failed.",
                    DaemonErrorCodes.DaemonEndpointNotRegistered));
            }

            if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcGuiRebootstrapResponse payload, out var payloadError)
                || !payload.Accepted
                || payload.ProcessId != expectedProcessId
                || !string.Equals(payload.ProjectFingerprint, unityProject.ProjectFingerprint, StringComparison.Ordinal))
            {
                return DaemonGuiRebootstrapRequestResult.Unavailable(ExecutionError.InternalError(
                    $"GUI supervisor rebootstrap response is invalid. {payloadError.Message}",
                    DaemonErrorCodes.DaemonEndpointNotRegistered));
            }

            return DaemonGuiRebootstrapRequestResult.Accepted();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is TimeoutException or IpcConnectTimeoutException or IOException or UnauthorizedAccessException or InvalidDataException or SocketException)
        {
            return DaemonGuiRebootstrapRequestResult.Unavailable(ExecutionError.InternalError(
                $"GUI supervisor rebootstrap request could not be completed. {exception.Message}",
                DaemonErrorCodes.DaemonEndpointNotRegistered));
        }
    }

    private static ExecutionError? ValidateManifest (
        GuiSupervisorManifestJsonContract? manifest,
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc)
    {
        if (manifest == null)
        {
            return ExecutionError.InternalError(
                "GUI supervisor manifest does not exist.",
                DaemonErrorCodes.DaemonEndpointNotRegistered);
        }

        if (manifest.SchemaVersion != GuiSupervisorManifestJsonContract.CurrentSchemaVersion)
        {
            return ExecutionError.InternalError(
                $"GUI supervisor manifest schema version is unsupported. Actual={manifest.SchemaVersion}.",
                DaemonErrorCodes.DaemonEndpointNotRegistered);
        }

        if (!string.Equals(manifest.ProjectFingerprint, unityProject.ProjectFingerprint, StringComparison.Ordinal))
        {
            return ExecutionError.InternalError(
                "GUI supervisor manifest projectFingerprint does not match the target project.",
                DaemonErrorCodes.DaemonEndpointNotRegistered);
        }

        if (manifest.ProcessId != expectedProcessId)
        {
            return ExecutionError.InternalError(
                "GUI supervisor manifest processId does not match the detected GUI process.",
                DaemonErrorCodes.DaemonEndpointNotRegistered);
        }

        if (expectedProcessStartedAtUtc is not null
            && (manifest.ProcessStartedAtUtc is not DateTimeOffset manifestProcessStartedAtUtc
                || !DaemonProcessStartTimeMatcher.Matches(
                    manifestProcessStartedAtUtc,
                    expectedProcessStartedAtUtc.Value)))
        {
            return ExecutionError.InternalError(
                "GUI supervisor manifest process start timestamp does not match the detected GUI process.",
                DaemonErrorCodes.DaemonEndpointNotRegistered);
        }

        if (string.IsNullOrWhiteSpace(manifest.SessionToken)
            || string.IsNullOrWhiteSpace(manifest.EndpointTransportKind)
            || string.IsNullOrWhiteSpace(manifest.EndpointAddress))
        {
            return ExecutionError.InternalError(
                "GUI supervisor manifest is missing endpoint or token metadata.",
                DaemonErrorCodes.DaemonEndpointNotRegistered);
        }

        return null;
    }

    private static IpcEndpoint ResolveEndpoint (GuiSupervisorManifestJsonContract manifest)
    {
        if (!ContractLiteralCodec.TryParse<IpcTransportKind>(manifest.EndpointTransportKind, out var transportKind))
        {
            throw new InvalidDataException($"GUI supervisor endpointTransportKind is invalid: {manifest.EndpointTransportKind}.");
        }

        return new IpcEndpoint(transportKind, manifest.EndpointAddress);
    }
}
