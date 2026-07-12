using System.Net.Sockets;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process.Identity;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;

/// <summary> Requests daemon endpoint rebootstrap from a project-scoped GUI supervisor endpoint. </summary>
internal sealed class DaemonGuiRebootstrapClient : IDaemonGuiRebootstrapClient
{
    private readonly IGuiSupervisorManifestStore manifestStore;

    private readonly IIpcTransportClient transportClient;

    private readonly TimeProvider timeProvider;

    public DaemonGuiRebootstrapClient (
        IGuiSupervisorManifestStore manifestStore,
        IIpcTransportClient transportClient,
        TimeProvider timeProvider)
    {
        this.manifestStore = manifestStore ?? throw new ArgumentNullException(nameof(manifestStore));
        this.transportClient = transportClient ?? throw new ArgumentNullException(nameof(transportClient));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
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

        var deadline = ExecutionDeadline.Start(timeout, timeProvider);
        var canReloadAfterTokenRejection = true;
        string? rejectedSessionToken = null;
        IpcResponse? sessionTokenRejection = null;
        IpcRequest? request = null;

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
                expectedProcessStartedAtUtc);
            if (validationError != null)
            {
                return DaemonGuiRebootstrapRequestResult.Unavailable(validationError);
            }

            if (rejectedSessionToken is not null
                && string.Equals(manifest!.SessionToken, rejectedSessionToken, StringComparison.Ordinal))
            {
                return CreateResponseFailureResult(sessionTokenRejection!);
            }

            try
            {
                if (!deadline.TryGetRemainingTimeout(out var requestTimeout))
                {
                    return CreateTimeoutResult("Timed out before GUI supervisor rebootstrap request could begin.");
                }

                request ??= new IpcRequest(
                    protocolVersion: IpcProtocol.CurrentVersion,
                    requestId: Guid.NewGuid(),
                    sessionToken: manifest!.SessionToken,
                    method: IpcMethodNames.GuiRebootstrap,
                    payload: IpcPayloadCodec.SerializeToElement(new IpcGuiRebootstrapRequest(
                        ProjectFingerprint: unityProject.ProjectFingerprint,
                        ReplaceExistingSession: true)),
                    responseMode: ContractLiteralCodec.ToValue(IpcResponseMode.Single));
                var requestForManifest = string.Equals(
                    request.SessionToken,
                    manifest!.SessionToken,
                    StringComparison.Ordinal)
                        ? request
                        : new IpcRequest(
                            request.ProtocolVersion,
                            request.RequestId,
                            manifest.SessionToken,
                            request.Method,
                            request.Payload,
                            request.ResponseMode);
                var response = await transportClient.SendAsync(
                        ResolveEndpoint(manifest),
                        requestForManifest,
                        requestTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (deadline.IsExpired)
                {
                    return CreateTimeoutResult("Timed out while requesting GUI supervisor rebootstrap.");
                }

                if (IsSessionTokenInvalid(response) && canReloadAfterTokenRejection)
                {
                    canReloadAfterTokenRejection = false;
                    rejectedSessionToken = manifest.SessionToken;
                    sessionTokenRejection = response;
                    continue;
                }

                if (IpcResponseFailureReader.TryRead(response, out _, out _))
                {
                    return CreateResponseFailureResult(response);
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
        _ = IpcResponseFailureReader.TryRead(response, out var firstError, out _);
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
