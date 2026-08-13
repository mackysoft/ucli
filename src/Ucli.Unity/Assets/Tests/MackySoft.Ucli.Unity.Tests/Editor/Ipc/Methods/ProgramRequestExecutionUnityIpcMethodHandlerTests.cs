using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Execution.Program;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using UnityEngine.TestTools;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class ProgramRequestExecutionUnityIpcMethodHandlerTests
    {
        private static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

        private static readonly ProjectFingerprint ProjectFingerprint =
            ProjectFingerprintTestFactory.Create("program-request-execution-handler");

        private static readonly UnityProjectIdentity Project = new(
            ProjectPathTestValues.RepositoryUnityProject,
            ProjectFingerprint,
            "6000.1.4f1");

        private static readonly UnityEditorGenerationSnapshot Generation = new(3, 5, 7, 11);

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator StartAndAttach_WhenTransportResponsesAreLost_ExecutesOneLogicalCallAndRetainsItsTerminalResponse () =>
            UniTask.ToCoroutine(async () =>
            {
                var hostContext = CreateHostContext();
                var observationSource = new FixedObservationSource(Generation);
                var dispatcher = new BlockingCounterDispatcher(Project);
                var registry = new ProgramRequestExecutionRegistry(new ManualMonotonicClock());
                var startHandler = new ProgramRequestExecutionUnityIpcMethodHandler(
                    registry,
                    dispatcher,
                    Project,
                    hostContext,
                    observationSource,
                    new FixedConfigurationSource());
                var attachHandler = new ProgramRequestAttachUnityIpcMethodHandler(
                    registry,
                    Project,
                    hostContext,
                    observationSource);
                var contextHandler = new ProgramExecutionContextUnityIpcMethodHandler(
                    hostContext,
                    observationSource,
                    new FixedConfigurationSource());
                var executionId = Guid.NewGuid();

                var contextResponse = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    contextHandler,
                    CreateRequest(
                        UnityIpcMethod.ProgramExecutionContext,
                        new IpcProgramExecutionContextRequest(CreateAuthorization())),
                    CancellationToken.None);

                Assert.That(contextResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        contextResponse.Payload,
                        out IpcProgramExecutionContextResponse context,
                        out _),
                    Is.True);
                Assert.That(context.Host, Is.EqualTo(hostContext.CreateInitialRegistration()));
                Assert.That(context.Generation, Is.EqualTo(Generation));

                var binding = CreateBinding(context.Host, context.Generation);
                var start = new IpcProgramRequestStartRequest(
                    executionId,
                    binding,
                    CreateCallRequest());
                var ownerTask = UnityIpcMethodHandlerTestInvoker.HandleAsync(
                        startHandler,
                        CreateRequest(UnityIpcMethod.ProgramRequestStart, start),
                        CancellationToken.None)
                    .AsTask();
                await TestAwaiter.WaitAsync(
                    dispatcher.Started,
                    "Program Request owner start",
                    SignalWaitTimeout);

                var concurrentStart = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    startHandler,
                    CreateRequest(UnityIpcMethod.ProgramRequestStart, start),
                    CancellationToken.None);
                var runningAttach = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    attachHandler,
                    CreateRequest(
                        UnityIpcMethod.ProgramRequestAttach,
                        new IpcProgramRequestAttachRequest(executionId, binding)),
                    CancellationToken.None);
                var mismatchedGenerationAttach = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    attachHandler,
                    CreateRequest(
                        UnityIpcMethod.ProgramRequestAttach,
                        new IpcProgramRequestAttachRequest(
                            executionId,
                            CreateBinding(context.Host, new UnityEditorGenerationSnapshot(3, 5, 7, 12)))),
                    CancellationToken.None);

                Assert.That(ReadPayload(concurrentStart).Status, Is.EqualTo(IpcProgramRequestExecutionStatus.Running));
                Assert.That(ReadPayload(runningAttach).Status, Is.EqualTo(IpcProgramRequestExecutionStatus.Running));
                Assert.That(ReadPayload(mismatchedGenerationAttach).Status, Is.EqualTo(IpcProgramRequestExecutionStatus.GenerationMismatch));
                Assert.That(dispatcher.CallCount, Is.EqualTo(1));

                dispatcher.Complete();
                var ownerResponse = await TestAwaiter.WaitAsync(
                    ownerTask,
                    "Program Request owner terminal response",
                    SignalWaitTimeout);
                var terminalAttach = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    attachHandler,
                    CreateRequest(
                        UnityIpcMethod.ProgramRequestAttach,
                        new IpcProgramRequestAttachRequest(executionId, binding)),
                    CancellationToken.None);

                var owner = ReadPayload(ownerResponse);
                var attached = ReadPayload(terminalAttach);
                Assert.That(owner.Status, Is.EqualTo(IpcProgramRequestExecutionStatus.Terminal));
                Assert.That(attached.Status, Is.EqualTo(IpcProgramRequestExecutionStatus.Terminal));
                Assert.That(attached.ExecutionId, Is.EqualTo(executionId));
                Assert.That(attached.Host, Is.EqualTo(context.Host));
                Assert.That(attached.Generation, Is.EqualTo(context.Generation));
                Assert.That(attached.ResponseBytes, Is.EqualTo(owner.ResponseBytes));
                Assert.That(dispatcher.CallCount, Is.EqualTo(1));

                var retainedResponse = JsonSerializer.Deserialize<IpcResponse>(
                    attached.ResponseBytes!,
                    IpcJsonSerializerOptions.Default);
                Assert.That(retainedResponse, Is.Not.Null);
                Assert.That(retainedResponse!.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(
                    IpcPayloadCodec.TryDeserialize(
                        retainedResponse.Payload,
                        out IpcExecuteResponse callResult,
                        out _),
                    Is.True);
                Assert.That(callResult.OpResults, Has.Count.EqualTo(1));
                Assert.That(callResult.OpResults[0].Applied, Is.True);
                Assert.That(callResult.OpResults[0].Verdict, Is.Null);
            });

        [Test]
        [Category("Size.Small")]
        public void StartRequest_RejectsPlanTokenThatDoesNotMatchTheFrozenBinding ()
        {
            var binding = CreateBinding(
                CreateHostContext().CreateInitialRegistration(),
                Generation,
                planTokenDigest: Digest("planned-token"));

            Assert.That(
                () => new IpcProgramRequestStartRequest(Guid.NewGuid(), binding, CreateCallRequest()),
                Throws.ArgumentException);
        }

        [Test]
        [Category("Size.Small")]
        public void Registry_PreservesDeadlineAndRecoveryWindow_SuppressesAttachFirstAndCancelsOnlyMatchingExecution ()
        {
            var clock = new ManualMonotonicClock();
            var now = DateTimeOffset.UtcNow;
            var registry = new ProgramRequestExecutionRegistry(clock, () => now, TimeSpan.FromMinutes(1));
            var binding = CreateBinding(
                CreateHostContext().CreateInitialRegistration(),
                Generation,
                deadlineUtc: now.AddMinutes(10));
            var executionId = Guid.NewGuid();

            Assert.That(registry.Attach(executionId, binding), Is.EqualTo(ProgramRequestExecutionRegistration.Suppressed));
            Assert.That(
                registry.AcquireStart(executionId, binding, new CancellationTokenSource()),
                Is.EqualTo(ProgramRequestExecutionRegistration.Suppressed));

            var runningId = Guid.NewGuid();
            using var source = new CancellationTokenSource();
            Assert.That(
                registry.AcquireStart(runningId, binding, source),
                Is.EqualTo(ProgramRequestExecutionRegistration.StartOwner));
            clock.Advance(TimeSpan.FromMinutes(6));
            now = now.AddMinutes(6);
            Assert.That(registry.Attach(runningId, binding), Is.EqualTo(ProgramRequestExecutionRegistration.Attached));
            Assert.That(
                registry.RequestCancellation(runningId, binding, IpcProgramRequestCancellationReason.UserCancelled),
                Is.EqualTo(ProgramRequestExecutionCancellationDisposition.Requested));
            Assert.That(source.IsCancellationRequested, Is.True);
            Assert.That(
                registry.RequestCancellation(
                    runningId,
                    CreateBinding(binding.Host, binding.Generation, deadlineUtc: binding.DeadlineUtc, requestDigest: Digest("other")),
                    IpcProgramRequestCancellationReason.DeadlineExceeded),
                Is.EqualTo(ProgramRequestExecutionCancellationDisposition.Conflict));
            registry.Complete(runningId, binding, new byte[] { 1 });
            Assert.That(
                registry.RequestCancellation(runningId, binding, IpcProgramRequestCancellationReason.DeadlineExceeded),
                Is.EqualTo(ProgramRequestExecutionCancellationDisposition.Terminal));
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenAuthorizationOrConfigurationBindingDoesNotMatch_RejectsBeforeDispatch () =>
            UniTask.ToCoroutine(async () =>
            {
                var host = CreateHostContext();
                var dispatcher = new BlockingCounterDispatcher(Project);
                var handler = new ProgramRequestExecutionUnityIpcMethodHandler(
                    new ProgramRequestExecutionRegistry(new ManualMonotonicClock()),
                    dispatcher,
                    Project,
                    host,
                    new FixedObservationSource(Generation),
                    new FixedConfigurationSource());
                foreach (var binding in new[]
                {
                    CreateBinding(host.CreateInitialRegistration(), Generation, configurationDigest: Digest("different")),
                    CreateBinding(host.CreateInitialRegistration(), Generation, authorizationDigest: IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(true, false)),
                })
                {
                    var request = new IpcProgramRequestStartRequest(Guid.NewGuid(), binding, CreateCallRequest());
                    var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                        handler,
                        CreateRequest(UnityIpcMethod.ProgramRequestStart, request),
                        CancellationToken.None);
                    Assert.That(ReadPayload(response).Status, Is.EqualTo(IpcProgramRequestExecutionStatus.GenerationMismatch));
                }
                Assert.That(dispatcher.CallCount, Is.Zero);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Start_WhenConfigurationChangesBeforeAdmission_RejectsBeforeDispatch () =>
            UniTask.ToCoroutine(async () =>
            {
                var host = CreateHostContext();
                var dispatcher = new BlockingCounterDispatcher(Project);
                var configurationSource = new MutableConfigurationSource(CreateConfiguration());
                var handler = new ProgramRequestExecutionUnityIpcMethodHandler(
                    new ProgramRequestExecutionRegistry(new ManualMonotonicClock()),
                    dispatcher,
                    Project,
                    host,
                    new FixedObservationSource(Generation),
                    configurationSource);
                var binding = CreateBinding(host.CreateInitialRegistration(), Generation);
                configurationSource.Configuration = CreateConfiguration(defaultTimeout: 3001);

                var response = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRequest(
                        UnityIpcMethod.ProgramRequestStart,
                        new IpcProgramRequestStartRequest(Guid.NewGuid(), binding, CreateCallRequest())),
                    CancellationToken.None);

                Assert.That(ReadPayload(response).Status, Is.EqualTo(IpcProgramRequestExecutionStatus.GenerationMismatch));
                Assert.That(dispatcher.CallCount, Is.Zero);
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator AttachAndCancel_WhenConfigurationChangesAfterStart_UseTheAdmittedBindingWithoutRedispatch () =>
            UniTask.ToCoroutine(async () =>
            {
                var host = CreateHostContext();
                var dispatcher = new BlockingCounterDispatcher(Project);
                var registry = new ProgramRequestExecutionRegistry(new ManualMonotonicClock());
                var configurationSource = new MutableConfigurationSource(CreateConfiguration());
                var observation = new FixedObservationSource(Generation);
                var start = new ProgramRequestExecutionUnityIpcMethodHandler(registry, dispatcher, Project, host, observation, configurationSource);
                var attach = new ProgramRequestAttachUnityIpcMethodHandler(registry, Project, host, observation);
                var cancel = new ProgramRequestCancelUnityIpcMethodHandler(registry, Project, host, observation);
                var binding = CreateBinding(host.CreateInitialRegistration(), Generation);
                var executionId = Guid.NewGuid();
                var ownerTask = UnityIpcMethodHandlerTestInvoker.HandleAsync(
                        start,
                        CreateRequest(UnityIpcMethod.ProgramRequestStart, new IpcProgramRequestStartRequest(executionId, binding, CreateCallRequest())),
                        CancellationToken.None)
                    .AsTask();
                await TestAwaiter.WaitAsync(dispatcher.Started, "Program Request owner start", SignalWaitTimeout);

                configurationSource.Configuration = CreateConfiguration(defaultTimeout: 3001);
                var cancellation = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    cancel,
                    CreateRequest(
                        UnityIpcMethod.ProgramRequestCancel,
                        new IpcProgramRequestCancelRequest(executionId, binding, IpcProgramRequestCancellationReason.UserCancelled)),
                    CancellationToken.None);

                Assert.That(ReadCancellationPayload(cancellation).Status, Is.EqualTo(IpcProgramRequestCancellationStatus.Requested));
                Assert.That(dispatcher.CallCount, Is.EqualTo(1));
                dispatcher.Complete();
                var owner = await TestAwaiter.WaitAsync(ownerTask, "Program Request cancellation terminal response", SignalWaitTimeout);
                var recovered = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    attach,
                    CreateRequest(UnityIpcMethod.ProgramRequestAttach, new IpcProgramRequestAttachRequest(executionId, binding)),
                    CancellationToken.None);

                Assert.That(ReadPayload(owner).Status, Is.EqualTo(IpcProgramRequestExecutionStatus.Terminal));
                Assert.That(ReadPayload(recovered).Status, Is.EqualTo(IpcProgramRequestExecutionStatus.Terminal));
                Assert.That(dispatcher.CallCount, Is.EqualTo(1));
            });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Cancel_WhenGenerationDiffersOrExecutionCannotAcceptCancellation_DoesNotDispatchCall () =>
            UniTask.ToCoroutine(async () =>
            {
                var host = CreateHostContext();
                var clock = new ManualMonotonicClock();
                var registry = new ProgramRequestExecutionRegistry(clock);
                var dispatcher = new BlockingCounterDispatcher(Project);
                var cancellation = new ProgramRequestCancelUnityIpcMethodHandler(
                    registry, Project, host, new FixedObservationSource(Generation));
                var binding = CreateBinding(host.CreateInitialRegistration(), Generation);

                var generationMismatch = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    cancellation,
                    CreateRequest(
                        UnityIpcMethod.ProgramRequestCancel,
                        new IpcProgramRequestCancelRequest(
                            Guid.NewGuid(),
                            CreateBinding(host.CreateInitialRegistration(), new UnityEditorGenerationSnapshot(3, 5, 7, 12)),
                            IpcProgramRequestCancellationReason.UserCancelled)),
                    CancellationToken.None);
                Assert.That(
                    ReadCancellationPayload(generationMismatch).Status,
                    Is.EqualTo(IpcProgramRequestCancellationStatus.GenerationMismatch));

                var executionId = Guid.NewGuid();
                var source = new CancellationTokenSource();
                Assert.That(registry.AcquireStart(executionId, binding, source), Is.EqualTo(ProgramRequestExecutionRegistration.StartOwner));
                source.Dispose();
                var unsupported = await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    cancellation,
                    CreateRequest(
                        UnityIpcMethod.ProgramRequestCancel,
                        new IpcProgramRequestCancelRequest(executionId, binding, IpcProgramRequestCancellationReason.DeadlineExceeded)),
                    CancellationToken.None);
                Assert.That(ReadCancellationPayload(unsupported).Status, Is.EqualTo(IpcProgramRequestCancellationStatus.Unsupported));
                Assert.That(dispatcher.CallCount, Is.Zero);
            });

        private static IpcProgramRequestExecutionBinding CreateBinding (
            MackySoft.Ucli.Contracts.Execution.Lifecycle.LifecycleExecutionHostRegistration host,
            UnityEditorGenerationSnapshot generation,
            DateTimeOffset? deadlineUtc = null,
            Sha256Digest? requestDigest = null,
            Sha256Digest? planTokenDigest = null,
            Sha256Digest? configurationDigest = null,
            Sha256Digest? authorizationDigest = null)
        {
            return new IpcProgramRequestExecutionBinding(
                Project,
                host,
                generation,
                deadlineUtc ?? DateTimeOffset.UtcNow.AddMinutes(1),
                requestDigest ?? Digest("request"),
                Digest("plan"),
                planTokenDigest,
                new[] { Digest("operation") },
                authorizationDigest ?? CreateAuthorization().Digest,
                configurationDigest ?? CreateConfiguration().Digest);
        }

        private static IpcExecuteRequest CreateCallRequest ()
        {
            return new IpcExecuteRequest(
                UcliCommandIds.Call.Name,
                IpcPayloadCodec.SerializeToElement(new
                {
                    protocolVersion = IpcProtocol.CurrentVersion,
                    steps = Array.Empty<object>(),
                }));
        }

        private static IpcRequestEnvelope CreateRequest (UnityIpcMethod method, object payload)
        {
            return new IpcRequestEnvelope(
                IpcProtocol.CurrentVersion,
                Guid.NewGuid(),
                "session-token",
                TextVocabulary.GetText(method),
                IpcPayloadCodec.SerializeToElement(payload),
                "single",
                DateTimeOffset.UtcNow.AddMinutes(1),
                requestDeadlineRemainingMilliseconds: 30_000);
        }

        private static IpcProgramRequestExecutionResponse ReadPayload (IpcResponse response)
        {
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(
                IpcPayloadCodec.TryDeserialize(
                    response.Payload,
                    out IpcProgramRequestExecutionResponse payload,
                    out _),
                Is.True);
            return payload;
        }

        private static IpcProgramRequestCancelResponse ReadCancellationPayload (IpcResponse response)
        {
            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserialize(response.Payload, out IpcProgramRequestCancelResponse payload, out _), Is.True);
            return payload;
        }

        private static UnityLifecycleExecutionHostContext CreateHostContext ()
        {
            return new UnityLifecycleExecutionHostContext(
                new ProcessIdentity(42, 123),
                Guid.Parse("56894d61-e45c-4347-930b-524605cf0f7c"),
                Guid.Parse("54c43b66-4990-41b9-b37f-a0ed8bde65c3"),
                recoveryLease: null);
        }

        private static Sha256Digest Digest (string value) =>
            Sha256Digest.Compute(Encoding.UTF8.GetBytes(value));

        private static IpcProgramEffectiveAuthorizationSnapshot CreateAuthorization () =>
            new(false, false, IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(false, false));

        private static IpcProgramEffectiveConfigurationSnapshot CreateConfiguration (int defaultTimeout = 3000)
        {
            var timeouts = new Dictionary<string, int> { ["call"] = 60000 };
            return new IpcProgramEffectiveConfigurationSnapshot(
                1,
                "safe",
                "optional",
                "requireFresh",
                new[] { "^ucli\\." },
                defaultTimeout,
                timeouts,
                IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(
                    1, "safe", "optional", "requireFresh", new[] { "^ucli\\." }, defaultTimeout, timeouts));
        }

        private sealed class FixedConfigurationSource : IUnityProgramEffectiveConfigurationSource
        {
            public bool TryCapture (out IpcProgramEffectiveConfigurationSnapshot? configuration)
            {
                configuration = CreateConfiguration();
                return true;
            }
        }

        private sealed class MutableConfigurationSource : IUnityProgramEffectiveConfigurationSource
        {
            public MutableConfigurationSource (IpcProgramEffectiveConfigurationSnapshot configuration)
            {
                Configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            }

            public IpcProgramEffectiveConfigurationSnapshot Configuration { get; set; }

            public bool TryCapture (out IpcProgramEffectiveConfigurationSnapshot? configuration)
            {
                configuration = Configuration;
                return true;
            }
        }

        private sealed class FixedObservationSource : IUnityEditorAvailabilityObservationSource
        {
            private readonly UnityEditorRuntimeObservation observation;

            public FixedObservationSource (UnityEditorGenerationSnapshot generation)
            {
                observation = new UnityEditorRuntimeObservation(
                    new UnityEditorStateSnapshot(
                        UnityEditorMode.Batchmode,
                        UnityEditorLifecycleState.Ready,
                        UnityEditorCompileState.Ready,
                        generation,
                        new UnityEditorPlayModeSnapshot(
                            UnityEditorPlayModeState.Stopped,
                            UnityEditorPlayModeTransition.None,
                            IsPlaying: false,
                            IsPlayingOrWillChangePlaymode: false)),
                    DateTimeOffset.UtcNow);
            }

            public UnityEditorRuntimeObservation CaptureAvailabilityObservation () => observation;
        }

        private sealed class BlockingCounterDispatcher : IExecuteRequestDispatcher
        {
            private readonly UnityProjectIdentity project;
            private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public BlockingCounterDispatcher (UnityProjectIdentity project)
            {
                this.project = project ?? throw new ArgumentNullException(nameof(project));
            }

            public int CallCount { get; private set; }

            public Task Started => started.Task;

            public void Complete () => release.TrySetResult(true);

            public Task<IpcResponse> DispatchAsync (
                IpcExecuteRequest request,
                ExecuteDispatchContext context,
                CancellationToken cancellationToken = default)
            {
                return DispatchProgramRequestAsync(request, context, cancellationToken);
            }

            public async Task<IpcResponse> DispatchProgramRequestAsync (
                IpcExecuteRequest request,
                ExecuteDispatchContext context,
                CancellationToken cancellationToken = default)
            {
                Assert.That(request.Command, Is.EqualTo(UcliCommandIds.Call.Name));
                CallCount++;
                started.TrySetResult(true);
                await release.Task.ConfigureAwait(false);
                return new IpcResponse(
                    IpcProtocol.CurrentVersion,
                    context.RequestId,
                    IpcResponseStatus.Ok,
                    IpcPayloadCodec.SerializeToElement(
                        new IpcExecuteResponse(
                            new[]
                            {
                                IpcExecuteOperationResultFactory.CreateDirectWithoutVerdict(
                                    op: "ucli.test.counter",
                                    phase: IpcExecuteOperationPhase.Call,
                                    applied: true,
                                    changed: true,
                                    touched: Array.Empty<IpcExecuteTouchedResource>(),
                                    operationDescriptorDigest: Digest("operation"),
                                    result: null,
                                    diagnostics: Array.Empty<IpcExecuteDiagnostic>()),
                            },
                            project,
                            planToken: null,
                            readPostcondition: null,
                            postReadSource: null,
                            contractViolations: null)),
                    Array.Empty<IpcError>());
            }
        }
    }
}
