using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Program;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Returns the host identity and generation atomically observed at the
    /// Program registration boundary. This is a read-only method and never
    /// starts a Program Step.
    /// </summary>
    internal sealed class ProgramExecutionContextUnityIpcMethodHandler : IUnityControlPlaneIpcMethodHandler
    {
        private readonly UnityLifecycleExecutionHostContext hostContext;
        private readonly IUnityEditorAvailabilityObservationSource observationSource;
        private readonly IUnityProgramEffectiveConfigurationSource configurationSource;

        public ProgramExecutionContextUnityIpcMethodHandler (
            UnityLifecycleExecutionHostContext hostContext,
            IUnityEditorAvailabilityObservationSource observationSource,
            IUnityProgramEffectiveConfigurationSource configurationSource)
        {
            this.hostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
            this.observationSource = observationSource ?? throw new ArgumentNullException(nameof(observationSource));
            this.configurationSource = configurationSource ?? throw new ArgumentNullException(nameof(configurationSource));
        }

        public UnityIpcMethod Method => UnityIpcMethod.ProgramExecutionContext;

        public ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (!UnityIpcRequestCodec.TryDecodeProgramExecutionContextRequest(
                    request,
                    out IpcProgramExecutionContextRequest contextRequest,
                    out var errorResponse))
            {
                return new ValueTask<IpcResponse>(errorResponse!);
            }
            if (!configurationSource.TryCapture(out var configuration))
            {
                return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateErrorResponse(
                    request,
                    new UcliCode("PROGRAM_EFFECTIVE_CONFIGURATION_UNAVAILABLE"),
                    "Unity could not resolve the effective Program configuration.",
                    instancePath: null));
            }

            var observation = observationSource.CaptureAvailabilityObservation();
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(
                request,
                new IpcProgramExecutionContextResponse(
                    hostContext.CreateInitialRegistration(),
                    observation.State.Generations,
                    contextRequest!.Authorization,
                    configuration!)));
        }
    }
}
