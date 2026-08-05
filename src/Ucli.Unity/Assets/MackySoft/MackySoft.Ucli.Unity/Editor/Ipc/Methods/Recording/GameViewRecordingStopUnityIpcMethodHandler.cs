using System;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Unity.Recording;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles idempotent <c>recording.stop</c> requests. </summary>
    internal sealed class GameViewRecordingStopUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly GameViewRecordingAdapterRegistry registry;

        private readonly GameViewRecordingIpcProjection projection;

        private readonly IUnityMutationLaneControl mutationLaneControl;

        public GameViewRecordingStopUnityIpcMethodHandler (
            GameViewRecordingAdapterRegistry registry,
            GameViewRecordingIpcProjection projection,
            IUnityMutationLaneControl mutationLaneControl)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));
            this.mutationLaneControl = mutationLaneControl
                ?? throw new ArgumentNullException(nameof(mutationLaneControl));
        }

        public UnityIpcMethod Method => UnityIpcMethod.RecordingStop;

        public ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeGameViewRecordingStopRequest(
                    request,
                    out IpcGameViewRecordingStopRequest payload,
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

                IpcGameViewRecordingStopSnapshot stopped = stopIntent.StartObserved
                        || DateTimeOffset.UtcNow >= stopIntent.DispatchDeadlineUtc
                    ? projection.ProjectStopBeforeStartTerminal(stopIntent)
                    : projection.ProjectStopRequestedBeforeStart(
                        stopIntent,
                        payload.KnownRecording);
                return Success(request, stopped);
            }

            var mutation = mutationLaneControl.BeginMutation();
            try
            {
                var adapterRegistered = registry.TryGet(out var adapter);
                if (!adapterRegistered)
                {
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

                        registry.TryGetPersistedTerminalAfterAdapterUnload(
                            payload.RecordingId,
                            out persisted);
                        return Success(request, projection.ProjectTerminal(
                            persisted,
                            payload.StartBinding));
                    }

                    return RecoverMissing(
                        request,
                        payload,
                        payload.KnownRecording,
                        adapterRegistered: false);
                }

                var status = adapter.GetStatus(payload.RecordingId);
                if (GameViewRecordingIpcProjection.TryGetRejection(status, out var rejected))
                {
                    if (rejected.Failure != GameViewRecordingFailure.NotFound)
                    {
                        return Error(request, rejected);
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

                        return Success(request, projection.ProjectTerminal(
                            persisted,
                            payload.StartBinding));
                    }

                    return RecoverMissing(
                        request,
                        payload,
                        payload.KnownRecording,
                        adapterRegistered: true);
                }

                var observedStatus = GameViewRecordingIpcProjection.RequireObserved(status);
                if (!projection.HasExecutionIdentity(
                        observedStatus,
                        payload.RecordingId,
                        payload.RequestDigest,
                        payload.EffectiveMaxDurationSeconds,
                        payload.StartBinding))
                {
                    return BindingMismatch(request);
                }

                var observed = projection.Project(observedStatus, payload.StartBinding);
                if (observed is IpcGameViewRecordingTerminalSnapshot terminal)
                {
                    return Success(request, terminal);
                }

                if (!projection.IsCurrentSessionLifetime(payload.StartBinding))
                {
                    return RecoverMissing(
                        request,
                        payload,
                        observed,
                        adapterRegistered: true);
                }

                var result = adapter.Stop(payload.RecordingId);
                if (GameViewRecordingIpcProjection.TryGetRejection(result, out var stopRejected))
                {
                    return stopRejected.Failure == GameViewRecordingFailure.NotFound
                        ? RecoverMissing(
                            request,
                            payload,
                            observed,
                            adapterRegistered: true)
                        : Error(request, stopRejected);
                }

                var stopObserved = GameViewRecordingIpcProjection.RequireObserved(result);

                return Success(request, projection.ProjectStop(
                    stopObserved,
                    payload.StartBinding));
            }
            finally
            {
                mutation.Complete();
            }
        }

        private ValueTask<IpcResponse> RecoverMissing (
            ValidatedUnityIpcRequest request,
            IpcGameViewRecordingStopRequest payload,
            IpcGameViewRecordingSnapshot? known,
            bool adapterRegistered)
        {
            if (registry.TryGetStopIntent(payload.RecordingId, out var existingIntent))
            {
                if (!existingIntent.HasIdentity(
                        payload.RecordingId,
                        payload.RequestDigest,
                        payload.EffectiveMaxDurationSeconds,
                        payload.StartBinding))
                {
                    return BindingMismatch(request);
                }

                IpcGameViewRecordingStopSnapshot existingSnapshot = existingIntent.StartObserved
                        || DateTimeOffset.UtcNow >= existingIntent.DispatchDeadlineUtc
                    ? projection.ProjectStopBeforeStartTerminal(existingIntent)
                    : projection.ProjectStopRequestedBeforeStart(existingIntent, known);
                return Success(request, existingSnapshot);
            }

            var now = DateTimeOffset.UtcNow;
            var canRegisterStopIntent = adapterRegistered
                && projection.IsCurrentBinding(payload.StartBinding)
                && now < payload.DispatchDeadlineUtc
                && (known == null
                    || known.State == MackySoft.Ucli.Contracts.Recording.GameViewRecordingState.Preparing);
            if (canRegisterStopIntent
                && registry.TryRegisterStopIntent(
                    payload.RecordingId,
                    payload.RequestDigest,
                    payload.EffectiveMaxDurationSeconds,
                    payload.StartBinding,
                    payload.DispatchDeadlineUtc,
                    now,
                    out var registeredIntent))
            {
                IpcGameViewRecordingStopSnapshot registeredSnapshot =
                    now >= registeredIntent.DispatchDeadlineUtc
                    ? projection.ProjectStopBeforeStartTerminal(registeredIntent)
                    : projection.ProjectStopRequestedBeforeStart(registeredIntent, known);
                return Success(request, registeredSnapshot);
            }
            if (canRegisterStopIntent)
            {
                return BindingMismatch(request);
            }

            var missingSnapshot = projection.ProjectMissingForStop(
                payload.RecordingId,
                payload.RequestDigest,
                payload.EffectiveMaxDurationSeconds,
                payload.StartBinding,
                payload.DispatchDeadlineUtc,
                known,
                adapterRegistered);
            return Success(request, missingSnapshot);
        }

        private static ValueTask<IpcResponse> Error (
            ValidatedUnityIpcRequest request,
            GameViewRecordingRejectedOperation result)
        {
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                request,
                GameViewRecordingIpcProjection.MapErrorCode(result.Failure),
                result.Message,
                null));
        }

        private static ValueTask<IpcResponse> BindingMismatch (ValidatedUnityIpcRequest request)
        {
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                request,
                GameViewRecordingErrorCodes.BindingMismatch,
                "The observed recording does not match the requested execution identity.",
                null));
        }

        private static ValueTask<IpcResponse> Success (
            ValidatedUnityIpcRequest request,
            IpcGameViewRecordingStopSnapshot snapshot)
        {
            var response = new IpcGameViewRecordingStopResponse(snapshot);
            return new ValueTask<IpcResponse>(
                UnityIpcResponseFactory.CreateSuccessResponse(request, response));
        }
    }
}
