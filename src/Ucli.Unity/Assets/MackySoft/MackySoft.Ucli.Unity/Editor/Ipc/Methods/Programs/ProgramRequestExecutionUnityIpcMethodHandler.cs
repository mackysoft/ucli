using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Execution.Program;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Starts or attaches to a Program-owned logical Request execution. The
    /// retained terminal response is written before this handler returns so a
    /// lost transport response can be recovered without another Call.
    /// </summary>
    internal sealed class ProgramRequestExecutionUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly ProgramRequestExecutionRegistry registry;
        private readonly IExecuteRequestDispatcher dispatcher;
        private readonly UnityProjectIdentity projectIdentity;
        private readonly UnityLifecycleExecutionHostContext hostContext;
        private readonly IUnityEditorAvailabilityObservationSource observationSource;
        private readonly IUnityProgramEffectiveConfigurationSource configurationSource;

        public ProgramRequestExecutionUnityIpcMethodHandler (
            ProgramRequestExecutionRegistry registry,
            IExecuteRequestDispatcher dispatcher,
            UnityProjectIdentity projectIdentity,
            UnityLifecycleExecutionHostContext hostContext,
            IUnityEditorAvailabilityObservationSource observationSource,
            IUnityProgramEffectiveConfigurationSource configurationSource)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.hostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
            this.observationSource = observationSource ?? throw new ArgumentNullException(nameof(observationSource));
            this.configurationSource = configurationSource ?? throw new ArgumentNullException(nameof(configurationSource));
        }

        public UnityIpcMethod Method => UnityIpcMethod.ProgramRequestStart;

        public async ValueTask<IpcResponse> HandleAsync (ValidatedUnityIpcRequest request, IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (!UnityIpcRequestCodec.TryDecodeProgramRequestStartRequest(request, out var start, out var decodeError))
            {
                return decodeError!;
            }

            var current = CaptureCurrent();
            if (!HasExpectedContext(start!.Binding, start.Request, current))
            {
                return Create(request, IpcProgramRequestExecutionStatus.GenerationMismatch, start.ExecutionId, current);
            }

            using var cancellationSource = new CancellationTokenSource();
            var registration = registry.AcquireStart(start.ExecutionId, start.Binding, cancellationSource);
            var response = registration switch
            {
                ProgramRequestExecutionRegistration.StartOwner => await ExecuteOwnerAsync(request, start, current, cancellationSource.Token).ConfigureAwait(false),
                ProgramRequestExecutionRegistration.Terminal => CreateTerminal(request, start.ExecutionId, start.Binding, current),
                ProgramRequestExecutionRegistration.Attached => Create(request, IpcProgramRequestExecutionStatus.Running, start.ExecutionId, current),
                ProgramRequestExecutionRegistration.Suppressed => Create(request, IpcProgramRequestExecutionStatus.NotStarted, start.ExecutionId, current),
                ProgramRequestExecutionRegistration.Conflict => Create(request, IpcProgramRequestExecutionStatus.Conflict, start.ExecutionId, current),
                _ => throw new ArgumentOutOfRangeException(),
            };
            return response;
        }

        private async ValueTask<IpcResponse> ExecuteOwnerAsync (
            ValidatedUnityIpcRequest transportRequest,
            IpcProgramRequestStartRequest start,
            CurrentContext current,
            CancellationToken cancellationToken)
        {
            // Cancellation may stop the caller's wait but must not leave an
            // invoked Call without a retained terminal response. The existing
            // request executor receives no caller cancellation for this
            // Program-owned logical execution.
            IpcResponse response;
            try
            {
                response = await dispatcher.DispatchProgramRequestAsync(
                        start.Request,
                        new ExecuteDispatchContext(transportRequest.RequestId, projectIdentity),
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                var reason = registry.GetCancellationReason(start.ExecutionId, start.Binding);
                response = UnityIpcResponseFactory.CreateErrorResponse(
                    transportRequest,
                    reason == IpcProgramRequestCancellationReason.DeadlineExceeded
                        ? ProgramRequestExecutionErrorCodes.DeadlineExceeded
                        : ProgramRequestExecutionErrorCodes.Cancelled,
                    reason == IpcProgramRequestCancellationReason.DeadlineExceeded
                        ? "Program Request execution deadline elapsed."
                        : "Program Request execution was cancelled.",
                    instancePath: null);
            }
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(response, IpcJsonSerializerOptions.Default);
            registry.Complete(start.ExecutionId, start.Binding, responseBytes);
            return CreateTerminal(transportRequest, start.ExecutionId, start.Binding, CaptureCurrent());
        }

        private IpcResponse CreateTerminal (
            ValidatedUnityIpcRequest request,
            Guid executionId,
            IpcProgramRequestExecutionBinding binding,
            CurrentContext current)
        {
            return registry.TryGetTerminal(executionId, binding, out var responseBytes)
                ? Create(request, IpcProgramRequestExecutionStatus.Terminal, executionId, current, responseBytes)
                : Create(request, IpcProgramRequestExecutionStatus.Unavailable, executionId, current);
        }

        private CurrentContext CaptureCurrent ()
        {
            return new CurrentContext(
                hostContext.CreateInitialRegistration(),
                observationSource.CaptureAvailabilityObservation().State.Generations);
        }

        private bool HasExpectedContext (
            IpcProgramRequestExecutionBinding binding,
            IpcExecuteRequest request,
            CurrentContext current)
        {
            return projectIdentity == binding.Project
                && current.Host == binding.Host
                && current.Generation == binding.Generation
                && binding.AuthorizationDigest == IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(
                    request.AllowDangerous,
                    request.AllowPlayMode)
                && configurationSource.TryCapture(out var configuration)
                && configuration!.Digest == binding.ConfigurationDigest;
        }

        private static IpcResponse Create (
            ValidatedUnityIpcRequest request,
            IpcProgramRequestExecutionStatus status,
            Guid executionId,
            CurrentContext current,
            byte[]? responseBytes = null)
        {
            return UnityIpcResponseFactory.CreateSuccessResponse(
                request,
                new IpcProgramRequestExecutionResponse(status, executionId, current.Host, current.Generation, responseBytes));
        }

        private sealed record CurrentContext (
            MackySoft.Ucli.Contracts.Execution.Lifecycle.LifecycleExecutionHostRegistration Host,
            MackySoft.Ucli.Contracts.Editor.UnityEditorGenerationSnapshot Generation);
    }

    /// <summary> Exposes attach-only recovery as a distinct IPC method. </summary>
    internal sealed class ProgramRequestAttachUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly ProgramRequestExecutionRegistry registry;
        private readonly UnityProjectIdentity projectIdentity;
        private readonly UnityLifecycleExecutionHostContext hostContext;
        private readonly IUnityEditorAvailabilityObservationSource observationSource;

        public ProgramRequestAttachUnityIpcMethodHandler (
            ProgramRequestExecutionRegistry registry,
            UnityProjectIdentity projectIdentity,
            UnityLifecycleExecutionHostContext hostContext,
            IUnityEditorAvailabilityObservationSource observationSource)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.hostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
            this.observationSource = observationSource ?? throw new ArgumentNullException(nameof(observationSource));
        }

        public UnityIpcMethod Method => UnityIpcMethod.ProgramRequestAttach;

        public ValueTask<IpcResponse> HandleAsync (ValidatedUnityIpcRequest request, IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (!UnityIpcRequestCodec.TryDecodeProgramRequestAttachRequest(request, out var attach, out var decodeError))
            {
                return new ValueTask<IpcResponse>(decodeError!);
            }

            var current = new CurrentContext(hostContext.CreateInitialRegistration(), observationSource.CaptureAvailabilityObservation().State.Generations);
            if (projectIdentity != attach!.Binding.Project
                || current.Host != attach.Binding.Host
                || current.Generation != attach.Binding.Generation)
            {
                return new ValueTask<IpcResponse>(Create(IpcProgramRequestExecutionStatus.GenerationMismatch));
            }

            var registration = registry.Attach(attach.ExecutionId, attach.Binding);
            if (registration == ProgramRequestExecutionRegistration.Terminal
                && registry.TryGetTerminal(attach.ExecutionId, attach.Binding, out var bytes))
            {
                return new ValueTask<IpcResponse>(Create(IpcProgramRequestExecutionStatus.Terminal, bytes));
            }
            return new ValueTask<IpcResponse>(Create(registration switch
            {
                ProgramRequestExecutionRegistration.Attached => IpcProgramRequestExecutionStatus.Running,
                ProgramRequestExecutionRegistration.Suppressed => IpcProgramRequestExecutionStatus.NotStarted,
                ProgramRequestExecutionRegistration.Conflict => IpcProgramRequestExecutionStatus.Conflict,
                _ => IpcProgramRequestExecutionStatus.Unavailable,
            }));

            IpcResponse Create (IpcProgramRequestExecutionStatus status, byte[]? bytes = null) => UnityIpcResponseFactory.CreateSuccessResponse(
                request,
                new IpcProgramRequestExecutionResponse(status, attach.ExecutionId, current.Host, current.Generation, bytes));
        }

        private sealed record CurrentContext (
            MackySoft.Ucli.Contracts.Execution.Lifecycle.LifecycleExecutionHostRegistration Host,
            MackySoft.Ucli.Contracts.Editor.UnityEditorGenerationSnapshot Generation);
    }

    /// <summary> Delivers a cancellation request to a matching running Program Request without replaying it. </summary>
    internal sealed class ProgramRequestCancelUnityIpcMethodHandler : IUnityIpcMethodHandler
    {
        private readonly ProgramRequestExecutionRegistry registry;
        private readonly UnityProjectIdentity projectIdentity;
        private readonly UnityLifecycleExecutionHostContext hostContext;
        private readonly IUnityEditorAvailabilityObservationSource observationSource;

        public ProgramRequestCancelUnityIpcMethodHandler (
            ProgramRequestExecutionRegistry registry,
            UnityProjectIdentity projectIdentity,
            UnityLifecycleExecutionHostContext hostContext,
            IUnityEditorAvailabilityObservationSource observationSource)
        {
            this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
            this.projectIdentity = projectIdentity ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.hostContext = hostContext ?? throw new ArgumentNullException(nameof(hostContext));
            this.observationSource = observationSource ?? throw new ArgumentNullException(nameof(observationSource));
        }

        public UnityIpcMethod Method => UnityIpcMethod.ProgramRequestCancel;

        public ValueTask<IpcResponse> HandleAsync (ValidatedUnityIpcRequest request, IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (!UnityIpcRequestCodec.TryDecodeProgramRequestCancelRequest(request, out var cancel, out var decodeError))
            {
                return new ValueTask<IpcResponse>(decodeError!);
            }

            var currentHost = hostContext.CreateInitialRegistration();
            var currentGeneration = observationSource.CaptureAvailabilityObservation().State.Generations;
            var status = projectIdentity != cancel!.Binding.Project
                || currentHost != cancel.Binding.Host
                || currentGeneration != cancel.Binding.Generation
                ? IpcProgramRequestCancellationStatus.GenerationMismatch
                : ToStatus(registry.RequestCancellation(cancel.ExecutionId, cancel.Binding, cancel.Reason));
            return new ValueTask<IpcResponse>(UnityIpcResponseFactory.CreateSuccessResponse(
                request,
                new IpcProgramRequestCancelResponse(status, cancel.ExecutionId, cancel.Reason)));
        }

        private static IpcProgramRequestCancellationStatus ToStatus (ProgramRequestExecutionCancellationDisposition disposition) => disposition switch
        {
            ProgramRequestExecutionCancellationDisposition.Requested => IpcProgramRequestCancellationStatus.Requested,
            ProgramRequestExecutionCancellationDisposition.Terminal => IpcProgramRequestCancellationStatus.Terminal,
            ProgramRequestExecutionCancellationDisposition.NotStarted => IpcProgramRequestCancellationStatus.NotStarted,
            ProgramRequestExecutionCancellationDisposition.Conflict => IpcProgramRequestCancellationStatus.Conflict,
            ProgramRequestExecutionCancellationDisposition.Unsupported => IpcProgramRequestCancellationStatus.Unsupported,
            _ => throw new ArgumentOutOfRangeException(nameof(disposition)),
        };
    }
}
