using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using MackySoft.Ucli.Unity.Runtime;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Persists the provider-observed binding required before one lifecycle action may issue side effects.
    /// </summary>
    internal sealed class LifecycleExecutionStartUnityIpcMethodHandler :
        IUnityControlPlaneIpcMethodHandler
    {
        private static readonly object StartGatesSync = new();

        private static readonly Dictionary<
            (LifecycleExecutionKind Kind, Guid ExecutionId),
            StartGate> StartGates = new();

        private readonly FileLifecycleExecutionStore executionStore;
        private readonly UnityProjectIdentity projectIdentity;
        private readonly UnityLifecycleExecutionHostContext hostContext;
        private readonly IUnityEditorReadinessGate runtimeObservationSource;
        private readonly IReadOnlyDictionary<
            LifecycleExecutionKind,
            ILifecycleExecutionStartAdmissionPolicy> startAdmissionPolicies;
        private readonly ILifecycleExecutionHostLifetimeObserver hostLifetimeObserver;
        private readonly ILifecycleExecutionDeadlineScheduler deadlineScheduler;
        private readonly IDaemonLogger daemonLogger;

        public LifecycleExecutionStartUnityIpcMethodHandler (
            FileLifecycleExecutionStore executionStore,
            UnityProjectIdentity projectIdentity,
            UnityLifecycleExecutionHostContext hostContext,
            IUnityEditorReadinessGate runtimeObservationSource,
            IEnumerable<ILifecycleExecutionStartAdmissionPolicy> startAdmissionPolicies,
            ILifecycleExecutionHostLifetimeObserver hostLifetimeObserver,
            ILifecycleExecutionDeadlineScheduler deadlineScheduler,
            IDaemonLogger daemonLogger)
        {
            this.executionStore = executionStore
                ?? throw new ArgumentNullException(nameof(executionStore));
            this.projectIdentity = projectIdentity
                ?? throw new ArgumentNullException(nameof(projectIdentity));
            this.hostContext = hostContext
                ?? throw new ArgumentNullException(nameof(hostContext));
            this.runtimeObservationSource = runtimeObservationSource
                ?? throw new ArgumentNullException(nameof(runtimeObservationSource));
            this.startAdmissionPolicies = CreateStartAdmissionPolicies(
                startAdmissionPolicies);
            this.hostLifetimeObserver = hostLifetimeObserver
                ?? throw new ArgumentNullException(nameof(hostLifetimeObserver));
            this.deadlineScheduler = deadlineScheduler
                ?? throw new ArgumentNullException(nameof(deadlineScheduler));
            this.daemonLogger = daemonLogger
                ?? throw new ArgumentNullException(nameof(daemonLogger));
        }

        public UnityIpcMethod Method => UnityIpcMethod.LifecycleStart;

        public async ValueTask<IpcResponse> HandleAsync (
            ValidatedUnityIpcRequest request,
            IpcRequestCancellation cancellation)
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!UnityIpcRequestCodec.TryDecodeLifecycleExecutionStartRequest(
                    request,
                    out var startRequest,
                    out var errorResponse))
            {
                daemonLogger.Warning(
                    DaemonLogCategories.Ipc,
                    "Lifecycle Execution start payload decode failed.");
                return errorResponse;
            }

            LifecycleExecutionStartBinding binding;
            var startGateIdentity = (
                startRequest.Kind,
                startRequest.ExecutionId);
            // Same-identity requests must observe the Start Record established by the first admission,
            // instead of independently rejecting the execution from an earlier absent observation.
            using (await EnterStartGateAsync(
                    startGateIdentity,
                    cancellation.Token))
            {
                var definition = new LifecycleExecutionDefinition(startRequest.Kind);
                var existing = await executionStore.ReadAsync(
                    definition.Kind,
                    startRequest.ExecutionId,
                    cancellation.Token);
                if (existing?.IsTerminal == true)
                {
                    if (existing.Start.LifecycleExecutionRef.DefinitionDigest
                        != startRequest.DefinitionDigest)
                    {
                        return CreateStartFailure(
                            request,
                            LifecycleExecutionStartOutcome.DefinitionConflict);
                    }
                    if (existing.Start.Project != projectIdentity)
                    {
                        return CreateStartFailure(
                            request,
                            LifecycleExecutionStartOutcome.ProjectMismatch);
                    }
                    if (existing.Start.Host.Process != hostContext.Process
                        || existing.Start.Host.EditorInstanceId
                            != hostContext.EditorInstanceId)
                    {
                        return CreateStartFailure(
                            request,
                            LifecycleExecutionStartOutcome.HostMismatch);
                    }

                    return CreateSuccessResponse(
                        request,
                        existing.Start);
                }

                UnityEditorRuntimeObservation startObservation;
                if (existing == null
                    && startAdmissionPolicies.TryGetValue(
                        definition.Kind,
                        out var startAdmissionPolicy))
                {
                    var admission = await startAdmissionPolicy.AdmitAsync(
                        startRequest.DeadlineUtc,
                        cancellation.Token);
                    if (!admission.IsAccepted)
                    {
                        var error = admission.Error;
                        return UnityIpcResponseFactory.CreateErrorResponse(
                            request,
                            error.Code,
                            error.Message,
                            error.InstancePath);
                    }

                    startObservation = admission.Observation;
                }
                else
                {
                    // Reconnection and internal replay must resolve the existing execution regardless of
                    // the Editor's current lifecycle state.
                    startObservation = runtimeObservationSource.CaptureObservation();
                }

                var generations = startObservation.State.Generations;
                var startResult = await executionStore.StartAsync(
                    definition,
                    startRequest.ExecutionId,
                    startRequest.DefinitionDigest,
                    projectIdentity,
                    hostContext.CreateInitialRegistration(),
                    generations,
                    startRequest.DeadlineUtc,
                    startRequest.StartedAtUtc,
                    cancellation.Token);
                if (!startResult.IsSuccess)
                {
                    return CreateStartFailure(request, startResult.Outcome);
                }

                binding = startResult.Binding;
            }

            hostLifetimeObserver.OnStartAccepted(
                binding.DeadlineUtc);
            if (binding.Host.CurrentEndpointRegistrationGenerationId
                != hostContext.EndpointRegistrationGenerationId)
            {
                var advanceOutcome = await executionStore.TryAdvanceEndpointRegistrationAsync(
                    startRequest.Kind,
                    startRequest.ExecutionId,
                    projectIdentity,
                    hostContext.Process,
                    hostContext.EditorInstanceId,
                    hostContext.EndpointRegistrationGenerationId,
                    hostContext.RecoveryLease,
                    DateTimeOffset.UtcNow,
                    cancellation.Token);
                if (advanceOutcome
                    is not LifecycleExecutionEndpointAdvanceOutcome.Advanced
                    and not LifecycleExecutionEndpointAdvanceOutcome.AlreadyCurrent)
                {
                    if (advanceOutcome
                        is LifecycleExecutionEndpointAdvanceOutcome.AlreadyTerminal
                            or LifecycleExecutionEndpointAdvanceOutcome
                                .TerminalPublicationFixed)
                    {
                        var finalized = await executionStore.ReadAsync(
                            startRequest.Kind,
                            startRequest.ExecutionId,
                            CancellationToken.None);
                        if (finalized?.IsTerminal == true
                            || finalized?.IsPublishing == true)
                        {
                            return CreateSuccessResponse(
                                request,
                                finalized.Start);
                        }
                    }

                    return CreateEndpointAdvanceFailure(request, advanceOutcome);
                }

                var advancedExecution = await executionStore.ReadAsync(
                    startRequest.Kind,
                    startRequest.ExecutionId,
                    cancellation.Token);
                binding = advancedExecution?.Start
                    ?? throw new InvalidOperationException(
                        "Lifecycle Execution disappeared after endpoint registration advancement.");
            }

            deadlineScheduler.Track(
                startRequest.Kind,
                startRequest.ExecutionId);
            return CreateSuccessResponse(
                request,
                binding);
        }

        private static async ValueTask<StartGateLease> EnterStartGateAsync (
            (LifecycleExecutionKind Kind, Guid ExecutionId) identity,
            CancellationToken cancellationToken)
        {
            StartGate startGate;
            lock (StartGatesSync)
            {
                if (!StartGates.TryGetValue(identity, out startGate))
                {
                    startGate = new StartGate();
                    StartGates.Add(identity, startGate);
                }

                startGate.ReferenceCount++;
            }

            try
            {
                await startGate.Semaphore.WaitAsync(cancellationToken);
            }
            catch
            {
                ReleaseStartGateReference(identity, startGate);
                throw;
            }

            return new StartGateLease(identity, startGate);
        }

        private static void ReleaseStartGateReference (
            (LifecycleExecutionKind Kind, Guid ExecutionId) identity,
            StartGate startGate)
        {
            lock (StartGatesSync)
            {
                startGate.ReferenceCount--;
                if (startGate.ReferenceCount != 0)
                {
                    return;
                }

                if (StartGates.TryGetValue(identity, out var registeredGate)
                    && ReferenceEquals(registeredGate, startGate))
                {
                    StartGates.Remove(identity);
                }

                startGate.Semaphore.Dispose();
            }
        }

        private static IpcResponse CreateSuccessResponse (
            ValidatedUnityIpcRequest request,
            LifecycleExecutionStartBinding binding)
        {
            return UnityIpcResponseFactory.CreateSuccessResponse(
                request,
                new IpcLifecycleExecutionStartResponse(binding));
        }

        private static IReadOnlyDictionary<
            LifecycleExecutionKind,
            ILifecycleExecutionStartAdmissionPolicy>
            CreateStartAdmissionPolicies (
                IEnumerable<ILifecycleExecutionStartAdmissionPolicy> policies)
        {
            if (policies == null)
            {
                throw new ArgumentNullException(nameof(policies));
            }

            var policiesByKind = new Dictionary<
                LifecycleExecutionKind,
                ILifecycleExecutionStartAdmissionPolicy>();
            foreach (var policy in policies)
            {
                if (policy == null)
                {
                    throw new ArgumentException(
                        "Lifecycle Execution start admission policies must not contain null.",
                        nameof(policies));
                }
                if (policiesByKind.ContainsKey(policy.Kind))
                {
                    throw new ArgumentException(
                        $"Lifecycle Execution start admission policy is already registered for {policy.Kind}.",
                        nameof(policies));
                }

                policiesByKind.Add(policy.Kind, policy);
            }

            return policiesByKind;
        }

        private static IpcResponse CreateStartFailure (
            ValidatedUnityIpcRequest request,
            LifecycleExecutionStartOutcome outcome)
        {
            return outcome switch
            {
                LifecycleExecutionStartOutcome.InvalidDefinition => CreateError(
                    request,
                    UcliCoreErrorCodes.InvalidArgument,
                    "Lifecycle Execution definition digest does not match its fixed action definition."),
                LifecycleExecutionStartOutcome.DefinitionConflict => CreateError(
                    request,
                    LifecycleExecutionErrorCodes.DefinitionConflict,
                    "Lifecycle Execution id is already bound to a different definition digest."),
                LifecycleExecutionStartOutcome.ProjectMismatch => CreateError(
                    request,
                    LifecycleExecutionErrorCodes.ProjectMismatch,
                    "Lifecycle Execution id is already bound to a different project identity."),
                LifecycleExecutionStartOutcome.HostMismatch => CreateError(
                    request,
                    LifecycleExecutionErrorCodes.HostMismatch,
                    "Lifecycle Execution id is already bound to a different Unity host."),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome,
                    "Unsupported Lifecycle Execution start outcome."),
            };
        }

        private static IpcResponse CreateEndpointAdvanceFailure (
            ValidatedUnityIpcRequest request,
            LifecycleExecutionEndpointAdvanceOutcome outcome)
        {
            return outcome switch
            {
                LifecycleExecutionEndpointAdvanceOutcome.ProjectMismatch => CreateError(
                    request,
                    LifecycleExecutionErrorCodes.ProjectMismatch,
                    "Lifecycle Execution project did not match the successor endpoint."),
                LifecycleExecutionEndpointAdvanceOutcome.HostMismatch => CreateError(
                    request,
                    LifecycleExecutionErrorCodes.HostMismatch,
                    "Lifecycle Execution Unity host did not match the successor endpoint."),
                LifecycleExecutionEndpointAdvanceOutcome.GenerationMismatch
                    or LifecycleExecutionEndpointAdvanceOutcome.RecoveryLeaseExpired => CreateError(
                        request,
                        LifecycleExecutionErrorCodes.GenerationMismatch,
                        "Lifecycle Execution successor endpoint was not proven by an active recovery lease."),
                LifecycleExecutionEndpointAdvanceOutcome.AlreadyTerminal => CreateError(
                    request,
                    LifecycleExecutionErrorCodes.GenerationMismatch,
                    "Lifecycle Execution was finalized before endpoint registration advancement."),
                LifecycleExecutionEndpointAdvanceOutcome
                    .TerminalPublicationFixed => CreateError(
                        request,
                        UcliCoreErrorCodes.InternalError,
                        "Lifecycle Execution terminal publication could not be reconnected after endpoint registration advancement was blocked."),
                LifecycleExecutionEndpointAdvanceOutcome.Missing => CreateError(
                    request,
                    UcliCoreErrorCodes.InternalError,
                    "Lifecycle Execution start record was not found during endpoint registration advancement."),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(outcome),
                    outcome,
                    "Unsupported endpoint registration advancement outcome."),
            };
        }

        private static IpcResponse CreateError (
            ValidatedUnityIpcRequest request,
            UcliCode code,
            string message)
        {
            return UnityIpcResponseFactory.CreateErrorResponse(
                request,
                code,
                message,
                instancePath: null);
        }

        private sealed class StartGate
        {
            public SemaphoreSlim Semaphore { get; } = new(1, 1);

            public int ReferenceCount { get; set; }
        }

        private sealed class StartGateLease : IDisposable
        {
            private readonly (
                LifecycleExecutionKind Kind,
                Guid ExecutionId) identity;

            private StartGate startGate;

            public StartGateLease (
                (LifecycleExecutionKind Kind, Guid ExecutionId) identity,
                StartGate startGate)
            {
                this.identity = identity;
                this.startGate = startGate;
            }

            public void Dispose ()
            {
                var ownedStartGate = Interlocked.Exchange(
                    ref startGate,
                    null);
                if (ownedStartGate == null)
                {
                    return;
                }

                ownedStartGate.Semaphore.Release();
                ReleaseStartGateReference(identity, ownedStartGate);
            }
        }
    }

    /// <summary>
    /// Owns one action's provider-side conditions that must be accepted before its new Start Record is persisted.
    /// </summary>
    internal interface ILifecycleExecutionStartAdmissionPolicy
    {
        /// <summary> Gets the action whose start admission conditions are owned by this policy. </summary>
        LifecycleExecutionKind Kind { get; }

        /// <summary> Evaluates action-specific start conditions and returns the observation to persist when accepted. </summary>
        /// <param name="deadlineUtc"> The immutable execution deadline proposed for the Start Record. </param>
        /// <param name="cancellationToken"> The cancellation token for the provider-private start request. </param>
        /// <returns> The typed admission decision.</returns>
        ValueTask<LifecycleExecutionStartAdmission> AdmitAsync (
            DateTimeOffset deadlineUtc,
            CancellationToken cancellationToken);
    }

    /// <summary> Represents one action-owned provider-side Start Record admission decision. </summary>
    internal sealed record LifecycleExecutionStartAdmission
    {
        private LifecycleExecutionStartAdmission (
            UnityEditorRuntimeObservation observation,
            IpcError error)
        {
            Observation = observation;
            Error = error;
        }

        /// <summary> Gets whether the Start Record may be persisted. </summary>
        public bool IsAccepted => Error == null;

        /// <summary> Gets the provider observation to persist when admission was accepted. </summary>
        public UnityEditorRuntimeObservation Observation { get; }

        /// <summary> Gets the typed rejection when admission was not accepted. </summary>
        public IpcError Error { get; }

        /// <summary> Creates an accepted decision with its provider observation. </summary>
        /// <param name="observation"> The observation fixed by the action-owned admission policy. </param>
        /// <returns> The accepted decision.</returns>
        public static LifecycleExecutionStartAdmission Accepted (
            UnityEditorRuntimeObservation observation)
        {
            return new LifecycleExecutionStartAdmission(
                observation ?? throw new ArgumentNullException(nameof(observation)),
                error: null);
        }

        /// <summary> Creates a rejected decision with its typed error. </summary>
        /// <param name="error"> The action-owned rejection.</param>
        /// <returns> The rejected decision.</returns>
        public static LifecycleExecutionStartAdmission Rejected (IpcError error)
        {
            return new LifecycleExecutionStartAdmission(
                observation: null,
                error ?? throw new ArgumentNullException(nameof(error)));
        }
    }
}
