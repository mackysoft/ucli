using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Requests daemon endpoint rebootstrap from a project-scoped GUI supervisor endpoint. </summary>
internal sealed class DaemonGuiRebootstrapClient : IDaemonGuiRebootstrapClient
{
    private readonly IGuiSupervisorManifestStore manifestStore;

    private readonly IIpcTransportClient transportClient;

    public DaemonGuiRebootstrapClient (
        IGuiSupervisorManifestStore manifestStore,
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
        ExecutionDeadline deadline,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(expectedProcessId, 0);
        ArgumentNullException.ThrowIfNull(deadline);

        var canReloadAfterTokenRejection = true;
        IpcSessionToken? rejectedSessionToken = null;
        IpcResponse? sessionTokenRejection = null;
        var requestId = Guid.NewGuid();
        var requestPayload = IpcPayloadCodec.SerializeToElement(new IpcGuiRebootstrapRequest(
            ProjectFingerprint: unityProject.ProjectFingerprint,
            ReplaceExistingSession: true));

        while (true)
        {
            GuiSupervisorManifestJsonContract? manifest;
            try
            {
                if (!deadline.TryGetRemainingTimeout(out var manifestReadTimeout))
                {
                    return CreateTimeoutResult("Timed out before GUI supervisor manifest read could begin.");
                }

                var manifestReadOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                        deadline,
                        cancellationToken,
                        "Timed out before GUI supervisor manifest read could begin.",
                        "Timed out while reading GUI supervisor manifest.",
                        token => manifestStore.ReadAfterEndpointPublicationAsync(
                            unityProject.RepositoryRoot,
                            unityProject.ProjectFingerprint,
                            manifestReadTimeout,
                            token))
                    .ConfigureAwait(false);
                if (!manifestReadOperation.IsSuccess)
                {
                    return CreateTimeoutResult(manifestReadOperation.Error!.Message);
                }

                manifest = manifestReadOperation.Value;
            }
            catch (TimeoutException exception)
            {
                return CreateTimeoutResult(
                    $"Timed out while waiting for GUI supervisor manifest publication. {exception.Message}");
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
                expectedProcessStartedAtUtc,
                out var sessionToken,
                out var endpoint);
            if (validationError != null)
            {
                return DaemonGuiRebootstrapRequestResult.Unavailable(validationError);
            }

            if (rejectedSessionToken is not null
                && sessionToken!.Equals(rejectedSessionToken))
            {
                return CreateResponseFailureResult(sessionTokenRejection!);
            }

            try
            {
                if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
                {
                    return CreateTimeoutResult("Timed out before GUI supervisor rebootstrap request could begin.");
                }

                if (!deadline.TryGetRemainingMilliseconds(out var requestDeadlineRemainingMilliseconds))
                {
                    return CreateTimeoutResult("Timed out before GUI supervisor rebootstrap request could begin.");
                }

                var request = UnityIpcRequestFactory.Create(
                    sessionToken!,
                    UnityIpcMethod.GuiRebootstrap,
                    requestPayload,
                    requestId,
                    IpcResponseMode.Single,
                    deadline.UtcDeadline,
                    requestDeadlineRemainingMilliseconds);
                var sendOperation = await ExecutionDeadlineOperation.ExecuteAsync(
                        deadline,
                        cancellationToken,
                        "Timed out before GUI supervisor rebootstrap request could begin.",
                        "Timed out while requesting GUI supervisor rebootstrap.",
                        token => transportClient.SendAsync(
                            endpoint!,
                            request,
                            requestTimeout,
                            token))
                    .ConfigureAwait(false);
                if (!sendOperation.IsSuccess)
                {
                    return CreateTimeoutResult(sendOperation.Error!.Message);
                }

                var response = sendOperation.Value!;
                if (IsSessionTokenInvalid(response) && canReloadAfterTokenRejection)
                {
                    canReloadAfterTokenRejection = false;
                    rejectedSessionToken = sessionToken;
                    sessionTokenRejection = response;
                    continue;
                }

                if (IpcResponseFailureReader.TryRead(response, out _))
                {
                    return CreateResponseFailureResult(response);
                }

                if (!IpcPayloadCodec.TryDeserialize(response.Payload, out IpcGuiRebootstrapResponse payload, out var payloadError)
                    || !payload.Accepted
                    || payload.ProcessId != expectedProcessId
                    || payload.ProjectFingerprint != unityProject.ProjectFingerprint)
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
            catch (TimeoutException exception)
            {
                return CreateTimeoutResult(
                    $"Timed out while requesting GUI supervisor rebootstrap. {exception.Message}");
            }
            catch (Exception exception) when (exception is IpcConnectTimeoutException or IOException or UnauthorizedAccessException or InvalidDataException or SocketException)
            {
                return DaemonGuiRebootstrapRequestResult.Unavailable(ExecutionError.InternalError(
                    $"GUI supervisor rebootstrap request could not be completed. {exception.Message}",
                    DaemonErrorCodes.DaemonEndpointNotRegistered));
            }
        }
    }

    private static DaemonGuiRebootstrapRequestResult CreateResponseFailureResult (IpcResponse response)
    {
        _ = IpcResponseFailureReader.TryRead(response, out var firstError);
        return DaemonGuiRebootstrapRequestResult.Unavailable(ExecutionError.InternalError(
            firstError?.Message ?? "GUI supervisor rebootstrap request failed.",
            DaemonErrorCodes.DaemonEndpointNotRegistered));
    }

    private static bool IsSessionTokenInvalid (IpcResponse response)
    {
        return response.Errors.Any(static error => error.Code == IpcSessionErrorCodes.SessionTokenInvalid);
    }

    private static DaemonGuiRebootstrapRequestResult CreateTimeoutResult (string message)
    {
        return DaemonGuiRebootstrapRequestResult.Unavailable(ExecutionError.Timeout(
            message,
            DaemonErrorCodes.DaemonEndpointNotRegistered));
    }

    private static ExecutionError? ValidateManifest (
        GuiSupervisorManifestJsonContract? manifest,
        ResolvedUnityProjectContext unityProject,
        int expectedProcessId,
        DateTimeOffset? expectedProcessStartedAtUtc,
        out IpcSessionToken? sessionToken,
        out IpcTransportEndpoint? endpoint)
    {
        sessionToken = null;
        endpoint = null;
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

        if (manifest.ProjectFingerprint != unityProject.ProjectFingerprint)
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

        sessionToken = manifest.SessionToken;
        try
        {
            endpoint = IpcTransportEndpoint.FromContract(manifest.Endpoint);
        }
        catch (ArgumentException exception)
        {
            return ExecutionError.InternalError(
                $"GUI supervisor manifest endpoint is invalid. {exception.Message}",
                DaemonErrorCodes.DaemonEndpointNotRegistered);
        }

        return null;
    }
}
