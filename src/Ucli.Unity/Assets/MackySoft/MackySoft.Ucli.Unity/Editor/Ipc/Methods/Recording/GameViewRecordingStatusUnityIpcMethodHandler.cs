using System;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Recording;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>recording.status</c> runtime observations. </summary>
    internal sealed class GameViewRecordingStatusUnityIpcMethodHandler : IUnityControlPlaneIpcMethodHandler
    {
        private readonly GameViewRecordingAdapterRegistry registry;

        private readonly GameViewRecordingIpcProjection projection;

        public GameViewRecordingStatusUnityIpcMethodHandler (
            GameViewRecordingAdapterRegistry registry,
            GameViewRecordingIpcProjection projection)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));
        }

        public UnityIpcMethod Method => UnityIpcMethod.RecordingStatus;

        public ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeGameViewRecordingStatusRequest(
                    request,
                    out IpcGameViewRecordingStatusRequest payload,
                    out var decodeError))
            {
                return new ValueTask<IpcResponse>(decodeError);
            }

            if (registry.TryGetStopIntent(payload.RecordingId, out var stopIntent))
            {
                if (!stopIntent.HasIdentity(
                        payload.RecordingId,
                        payload.RequestDigest,
                        payload.EffectiveMaxDurationSeconds,
                        payload.StartBinding))
                {
                    return BindingMismatch(request);
                }

                return Success(
                    request,
                    stopIntent.StartObserved || DateTimeOffset.UtcNow >= stopIntent.DispatchDeadlineUtc
                        ? projection.ProjectStopBeforeStartTerminal(stopIntent)
                        : projection.ProjectStopRequestedBeforeStart(stopIntent, payload.KnownRecording));
            }

            var adapterRegistered = registry.TryGet(out var adapter);
            if (adapterRegistered)
            {
                var result = adapter.GetStatus(payload.RecordingId);
                if (!GameViewRecordingIpcProjection.TryGetRejection(result, out var rejected))
                {
                    var observed = GameViewRecordingIpcProjection.RequireObserved(result);
                    if (projection.HasExecutionIdentity(
                            observed,
                            payload.RecordingId,
                            payload.RequestDigest,
                            payload.EffectiveMaxDurationSeconds,
                            payload.StartBinding))
                    {
                        return Success(request, projection.Project(
                            observed,
                            payload.StartBinding));
                    }

                    return BindingMismatch(request);
                }

                if (rejected.Failure != GameViewRecordingFailure.NotFound)
                {
                    return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        GameViewRecordingIpcProjection.MapErrorCode(rejected.Failure),
                        rejected.Message,
                        null));
                }
            }

            if (registry.TryGetPersistedTerminal(payload.RecordingId, out var persisted))
            {
                if (!projection.HasExecutionIdentity(
                        persisted,
                        payload.RecordingId,
                        payload.RequestDigest,
                        payload.EffectiveMaxDurationSeconds,
                        payload.StartBinding))
                {
                    return BindingMismatch(request);
                }

                if (!adapterRegistered)
                {
                    registry.TryGetPersistedTerminalAfterAdapterUnload(
                        payload.RecordingId,
                        out persisted);
                }
                return Success(request, projection.Project(persisted, payload.StartBinding));
            }

            return Success(request, RecoverMissing(payload, adapterRegistered));
        }

        private IpcGameViewRecordingSnapshot RecoverMissing (
            IpcGameViewRecordingStatusRequest payload,
            bool adapterRegistered)
        {
            return projection.ProjectMissingForStatus(
                payload.RecordingId,
                payload.RequestDigest,
                payload.EffectiveMaxDurationSeconds,
                payload.StartBinding,
                payload.DispatchDeadlineUtc,
                payload.KnownRecording,
                adapterRegistered);
        }

        private static ValueTask<IpcResponse> Success (
            ValidatedUnityIpcRequest request,
            IpcGameViewRecordingSnapshot recording)
        {
            var selection = new IpcSelectedGameViewRecordingSelection(recording);
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(
                request,
                new IpcGameViewRecordingStatusResponse(selection)));
        }

        private static ValueTask<IpcResponse> BindingMismatch (ValidatedUnityIpcRequest request)
        {
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                request,
                GameViewRecordingErrorCodes.BindingMismatch,
                "The observed recording does not match the requested execution identity.",
                null));
        }
    }
}
