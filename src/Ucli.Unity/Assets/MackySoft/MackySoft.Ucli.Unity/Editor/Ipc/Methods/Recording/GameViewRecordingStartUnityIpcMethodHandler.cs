using System;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Recording;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Handles <c>recording.start</c> IPC requests without owning the recording lifetime. </summary>
    internal sealed class GameViewRecordingStartUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly GameViewRecordingAdapterRegistry registry;

        private readonly GameViewRecordingIpcProjection projection;

        private readonly UnityDaemonBootstrapContext bootstrapContext;

        private readonly IUnityMutationLaneControl mutationLaneControl;

        public GameViewRecordingStartUnityIpcMethodHandler (
            GameViewRecordingAdapterRegistry registry,
            GameViewRecordingIpcProjection projection,
            UnityDaemonBootstrapContext bootstrapContext,
            IUnityMutationLaneControl mutationLaneControl)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.projection = projection ?? throw new ArgumentNullException(nameof(projection));
            this.bootstrapContext = bootstrapContext ?? throw new ArgumentNullException(nameof(bootstrapContext));
            this.mutationLaneControl = mutationLaneControl
                ?? throw new ArgumentNullException(nameof(mutationLaneControl));
        }

        public UnityIpcMethod Method => UnityIpcMethod.RecordingStart;

        public ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeGameViewRecordingStartRequest(
                    request,
                    out IpcGameViewRecordingStartRequest payload,
                    out var decodeError))
            {
                return new ValueTask<IpcResponse>(decodeError);
            }

            GameViewRecordingOperationResult result;
            var mutation = mutationLaneControl.BeginMutation();
            try
            {
                var observedAtUtc = DateTimeOffset.UtcNow;
                registry.RemoveExpiredStopIntents(observedAtUtc);
                if (observedAtUtc >= payload.DispatchDeadlineUtc)
                {
                    return DispatchDeadlineExceeded(request);
                }

                if (!projection.IsCurrentBinding(payload.StartBinding))
                {
                    return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        GameViewRecordingErrorCodes.BindingMismatch,
                        "The recording start binding does not match the current Unity process, runtime, and Editor generation.",
                        null));
                }

                if (registry.TryObserveStopBeforeStart(
                        payload.RecordingId,
                        payload.RequestDigest,
                        payload.Request.MaxDurationSeconds,
                        payload.StartBinding,
                        out var stopIntent))
                {
                    var stopped = projection.ProjectStopBeforeStartTerminal(stopIntent);
                    return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(
                        request,
                        new IpcGameViewRecordingStartResponse(stopped)));
                }

                if (!registry.TryGet(out var adapter))
                {
                    return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                        request,
                        GameViewRecordingErrorCodes.Unavailable,
                        "No compatible GameView recording adapter is registered.",
                        null));
                }

                var effectiveMaximumDuration = payload.Request.MaxDurationSeconds;
                var stagingOutput = UcliStoragePathResolver.ResolveGameViewRecordingProviderOutputPath(
                    bootstrapContext.RepositoryRoot,
                    bootstrapContext.ProjectFingerprint,
                    payload.RecordingId);
                var adapterRequest = new GameViewRecordingStartRequest(
                    payload.RecordingId,
                    payload.RequestDigest,
                    payload.Request.Resolution,
                    payload.Request.FrameRate,
                    TimeSpan.FromSeconds(effectiveMaximumDuration),
                    stagingOutput,
                    payload.StartBinding);
                if (DateTimeOffset.UtcNow >= payload.DispatchDeadlineUtc)
                {
                    return DispatchDeadlineExceeded(request);
                }
                result = adapter.Start(adapterRequest);
            }
            finally
            {
                mutation.Complete();
            }

            if (GameViewRecordingIpcProjection.TryGetRejection(result, out var rejected))
            {
                return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    GameViewRecordingIpcProjection.MapErrorCode(rejected.Failure),
                    rejected.Message,
                    null));
            }

            var observed = GameViewRecordingIpcProjection.RequireObserved(result);

            var response = new IpcGameViewRecordingStartResponse(
                projection.Project(observed, payload.StartBinding));
            return new ValueTask<IpcResponse>(
                UnityIpcResponseFactory.CreateSuccessResponse(request, response));
        }

        private static ValueTask<IpcResponse> DispatchDeadlineExceeded (
            ValidatedUnityIpcRequest request)
        {
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                request,
                GameViewRecordingErrorCodes.DispatchDeadlineExceeded,
                "The recording start dispatch deadline elapsed before Recorder admission.",
                null));
        }
    }
}
