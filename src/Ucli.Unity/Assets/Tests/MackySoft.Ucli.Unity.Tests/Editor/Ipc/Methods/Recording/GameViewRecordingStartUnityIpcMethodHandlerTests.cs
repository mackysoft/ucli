using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.FileSystem;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Presentation;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Unity.Ipc;
using MackySoft.Ucli.Unity.Recording;
using MackySoft.Ucli.Unity.Runtime;
using NUnit.Framework;
using ContractCaptureProfile = MackySoft.Ucli.Contracts.Recording.GameViewRecordingCaptureProfile;
using ContractCodec = MackySoft.Ucli.Contracts.Recording.GameViewRecordingCodec;
using ContractContainer = MackySoft.Ucli.Contracts.Recording.GameViewRecordingContainer;
using ContractLimits = MackySoft.Ucli.Contracts.Recording.GameViewRecordingLimits;
using ContractTimingMode = MackySoft.Ucli.Contracts.Recording.GameViewRecordingTimingMode;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;

namespace MackySoft.Ucli.Unity.Tests
{
    [TestFixture]
    internal sealed class GameViewRecordingStartUnityIpcMethodHandlerTests
    {
        private static readonly Sha256Digest RequestDigest =
            Sha256Digest.Parse(new string('d', 64));

        [Test]
        public void Capability_WhenAdapterAdmissionIsReady_ReturnsCurrentStartBinding ()
        {
            var fixture = CreateFixture();
            var handler = new GameViewRecordingCapabilityUnityIpcMethodHandler(
                fixture.Registry,
                fixture.Projection);
            var request = CreateRequest(
                UnityIpcMethod.RecordingCapability,
                new IpcGameViewRecordingCapabilityRequest());

            var response = UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    request,
                    CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcGameViewRecordingCapabilityResponse payload,
                out _), Is.True);
            Assert.That(payload.StartBinding, Is.EqualTo(
                fixture.Projection.CaptureCurrentBinding()));
        }

        [Test]
        public void HandleAsync_WhenBindingDoesNotMatch_ReturnsTypedErrorBeforeAdapterStart ()
        {
            var fixture = CreateFixture();
            var current = fixture.Projection.CaptureCurrentBinding();
            var mismatched = new IpcGameViewRecordingStartBinding(
                new ProcessIdentity(
                    current.Process.ProcessId,
                    current.Process.Generation + 1),
                current.Runtime,
                current.Generation);

            var response = InvokeAsync(
                    fixture,
                    mismatched,
                    DateTimeOffset.UtcNow.AddMinutes(1))
                .GetAwaiter()
                .GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(
                GameViewRecordingErrorCodes.BindingMismatch));
            Assert.That(fixture.Adapter.StartCallCount, Is.Zero);
        }

        [Test]
        public void HandleAsync_WhenDispatchDeadlineElapsed_ReturnsTypedErrorBeforeAdapterStart ()
        {
            var fixture = CreateFixture();

            var response = InvokeAsync(
                    fixture,
                    fixture.Projection.CaptureCurrentBinding(),
                    DateTimeOffset.UtcNow.AddSeconds(-1))
                .GetAwaiter()
                .GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(
                GameViewRecordingErrorCodes.DispatchDeadlineExceeded));
            Assert.That(fixture.Adapter.StartCallCount, Is.Zero);
        }

        [Test]
        public void HandleAsync_WhenMatchingStopPrecedesStart_ReturnsTerminalWithoutAdapterAdmission ()
        {
            var fixture = CreateFixture();
            var binding = fixture.Projection.CaptureCurrentBinding();
            var recordingId = Guid.NewGuid();
            var deadlineUtc = DateTimeOffset.UtcNow.AddMinutes(1);
            Assert.That(fixture.Registry.TryRegisterStopIntent(
                recordingId,
                RequestDigest,
                effectiveMaxDurationSeconds: 120,
                binding,
                deadlineUtc,
                DateTimeOffset.UtcNow,
                out _), Is.True);

            var response = InvokeAsync(fixture, binding, deadlineUtc, recordingId)
                .GetAwaiter()
                .GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcGameViewRecordingStartResponse start,
                out _), Is.True);
            Assert.That(start.Recording.State, Is.EqualTo(
                MackySoft.Ucli.Contracts.Recording.GameViewRecordingState.Indeterminate));
            Assert.That(fixture.Adapter.StartCallCount, Is.Zero);
            Assert.That(fixture.Registry.TryGetStopIntent(recordingId, out var intent), Is.True);
            Assert.That(intent.StartObserved, Is.True);
        }

        [Test]
        public void StatusAndStop_WhenMatchingStopPrecedesStart_ReturnConsistentRecoverySnapshots ()
        {
            var fixture = CreateFixture();
            var binding = fixture.Projection.CaptureCurrentBinding();
            var recordingId = Guid.NewGuid();
            var deadlineUtc = DateTimeOffset.UtcNow.AddMinutes(1);
            Assert.That(fixture.Registry.TryRegisterStopIntent(
                recordingId,
                RequestDigest,
                effectiveMaxDurationSeconds: 120,
                binding,
                deadlineUtc,
                DateTimeOffset.UtcNow,
                out _), Is.True);

            var statusResponse = InvokeStatusAsync(
                    fixture,
                    new IpcGameViewRecordingStatusRequest(
                        recordingId,
                        RequestDigest,
                        effectiveMaxDurationSeconds: 120,
                        binding,
                        deadlineUtc,
                        knownRecording: null))
                .GetAwaiter()
                .GetResult();
            var stopResponse = InvokeStopAsync(
                    fixture,
                    CreateStopRequest(recordingId, binding, deadlineUtc, known: null))
                .GetAwaiter()
                .GetResult();

            Assert.That(statusResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(stopResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserialize(
                statusResponse.Payload,
                out IpcGameViewRecordingStatusResponse status,
                out _), Is.True);
            Assert.That(IpcPayloadCodec.TryDeserialize(
                stopResponse.Payload,
                out IpcGameViewRecordingStopResponse stop,
                out _), Is.True);
            var statusRecovery = ((IpcSelectedGameViewRecordingSelection)status.RecordingSelection)
                .Recording as IpcGameViewRecordingRecoverySnapshot;
            var stopRecovery = stop.Recording as IpcGameViewRecordingRecoverySnapshot;
            Assert.That(statusRecovery, Is.Not.Null);
            Assert.That(stopRecovery, Is.Not.Null);
            Assert.That(stopRecovery!.RecordingId, Is.EqualTo(statusRecovery!.RecordingId));
            Assert.That(stopRecovery.RequestDigest, Is.EqualTo(statusRecovery.RequestDigest));
            Assert.That(stopRecovery.State, Is.EqualTo(statusRecovery.State));
            Assert.That(stopRecovery.StopReason, Is.EqualTo(statusRecovery.StopReason));
            Assert.That(fixture.Adapter.StartCallCount, Is.Zero);
            Assert.That(fixture.Adapter.StopCallCount, Is.Zero);
        }

        [Test]
        public void HandleAsync_WhenAdapterReportsAcceptedTerminal_ReturnsTheTerminalSnapshot ()
        {
            var fixture = CreateFixture();
            fixture.Adapter.ReturnAcceptedTerminal = true;

            var response = InvokeAsync(
                    fixture,
                    fixture.Projection.CaptureCurrentBinding(),
                    DateTimeOffset.UtcNow.AddMinutes(1))
                .GetAwaiter()
                .GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcGameViewRecordingStartResponse start,
                out _), Is.True);
            Assert.That(start.Recording.State, Is.EqualTo(
                MackySoft.Ucli.Contracts.Recording.GameViewRecordingState.Indeterminate));
            var terminal = start.Recording as IpcGameViewRecordingIndeterminateSnapshot;
            Assert.That(terminal, Is.Not.Null);
            Assert.That(terminal!.StartedAtUtc, Is.Not.Null);
            Assert.That(fixture.Adapter.StartCallCount, Is.EqualTo(1));
        }

        [Test]
        public void Status_WhenDispatchMayStillBePending_ReturnsPreparingForTheBoundRecording ()
        {
            var fixture = CreateFixture();
            var binding = fixture.Projection.CaptureCurrentBinding();
            var recordingId = Guid.NewGuid();
            var payload = new IpcGameViewRecordingStatusRequest(
                recordingId,
                RequestDigest,
                effectiveMaxDurationSeconds: 120,
                binding,
                DateTimeOffset.UtcNow.AddMinutes(1),
                knownRecording: null);
            var handler = new GameViewRecordingStatusUnityIpcMethodHandler(
                fixture.Registry,
                fixture.Projection);

            var response = UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRequest(UnityIpcMethod.RecordingStatus, payload),
                    CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcGameViewRecordingStatusResponse status,
                out _), Is.True);
            var selected = (IpcSelectedGameViewRecordingSelection)status.RecordingSelection;
            var preparing = selected.Recording as IpcGameViewRecordingActiveSnapshot;
            Assert.That(preparing, Is.Not.Null);
            Assert.That(preparing!.RecordingId, Is.EqualTo(recordingId));
            Assert.That(preparing.State, Is.EqualTo(
                MackySoft.Ucli.Contracts.Recording.GameViewRecordingState.Preparing));
            Assert.That(preparing.StartedAtUtc, Is.Null);
        }

        [Test]
        public void Status_WhenObservedRecordingIdentityDoesNotMatch_ReturnsBindingMismatch ()
        {
            var fixture = CreateFixture();
            var binding = fixture.Projection.CaptureCurrentBinding();
            var recordingId = Guid.NewGuid();
            fixture.Adapter.StatusResult = GameViewRecordingOperationResult.Observed(
                CreateObservedSnapshot(
                    recordingId,
                    Sha256Digest.Parse(new string('e', 64)),
                    binding));
            var payload = new IpcGameViewRecordingStatusRequest(
                recordingId,
                RequestDigest,
                effectiveMaxDurationSeconds: 120,
                binding,
                DateTimeOffset.UtcNow.AddMinutes(1),
                knownRecording: null);
            var handler = new GameViewRecordingStatusUnityIpcMethodHandler(
                fixture.Registry,
                fixture.Projection);

            var response = UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRequest(UnityIpcMethod.RecordingStatus, payload),
                    CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(
                GameViewRecordingErrorCodes.BindingMismatch));
        }

        [Test]
        public void Status_WhenStopIntentIdentityDoesNotMatch_ReturnsBindingMismatch ()
        {
            var fixture = CreateFixture();
            var binding = fixture.Projection.CaptureCurrentBinding();
            var recordingId = Guid.NewGuid();
            Assert.That(fixture.Registry.TryRegisterStopIntent(
                recordingId,
                Sha256Digest.Parse(new string('e', 64)),
                effectiveMaxDurationSeconds: 120,
                binding,
                DateTimeOffset.UtcNow.AddMinutes(1),
                DateTimeOffset.UtcNow,
                out _), Is.True);
            var payload = new IpcGameViewRecordingStatusRequest(
                recordingId,
                RequestDigest,
                effectiveMaxDurationSeconds: 120,
                binding,
                DateTimeOffset.UtcNow.AddMinutes(1),
                knownRecording: null);
            var handler = new GameViewRecordingStatusUnityIpcMethodHandler(
                fixture.Registry,
                fixture.Projection);

            var response = UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRequest(UnityIpcMethod.RecordingStatus, payload),
                    CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(
                GameViewRecordingErrorCodes.BindingMismatch));
        }

        [Test]
        public void Stop_WhenObservedRecordingIdentityDoesNotMatch_ReturnsBindingMismatchWithoutStopIntent ()
        {
            var fixture = CreateFixture();
            var binding = fixture.Projection.CaptureCurrentBinding();
            var recordingId = Guid.NewGuid();
            fixture.Adapter.StatusResult = GameViewRecordingOperationResult.Observed(
                CreateObservedSnapshot(
                    recordingId,
                    Sha256Digest.Parse(new string('e', 64)),
                    binding));
            var payload = CreateStopRequest(
                recordingId,
                binding,
                DateTimeOffset.UtcNow.AddMinutes(1),
                known: null);

            var response = InvokeStopAsync(fixture, payload).GetAwaiter().GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(
                GameViewRecordingErrorCodes.BindingMismatch));
            Assert.That(fixture.Adapter.StopCallCount, Is.Zero);
            Assert.That(fixture.Registry.TryGetStopIntent(
                recordingId,
                out _), Is.False);
        }

        [Test]
        public void Stop_WhenStopIntentIdentityDoesNotMatch_ReturnsBindingMismatch ()
        {
            var fixture = CreateFixture();
            var binding = fixture.Projection.CaptureCurrentBinding();
            var recordingId = Guid.NewGuid();
            Assert.That(fixture.Registry.TryRegisterStopIntent(
                recordingId,
                Sha256Digest.Parse(new string('e', 64)),
                effectiveMaxDurationSeconds: 120,
                binding,
                DateTimeOffset.UtcNow.AddMinutes(1),
                DateTimeOffset.UtcNow,
                out _), Is.True);
            var payload = CreateStopRequest(
                recordingId,
                binding,
                DateTimeOffset.UtcNow.AddMinutes(1),
                known: null);

            var response = InvokeStopAsync(fixture, payload).GetAwaiter().GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors[0].Code, Is.EqualTo(
                GameViewRecordingErrorCodes.BindingMismatch));
            Assert.That(fixture.Adapter.StopCallCount, Is.Zero);
        }

        [Test]
        public void Stop_WhenPreStartSafetyConditionsDoNotAllHold_DoesNotRegisterStopIntent ()
        {
            var fixture = CreateFixture();
            var current = fixture.Projection.CaptureCurrentBinding();
            var preparing = new IpcGameViewRecordingActiveSnapshot(
                Guid.NewGuid(),
                RequestDigest,
                MackySoft.Ucli.Contracts.Recording.GameViewRecordingState.Recording,
                current.Runtime,
                target: null,
                effectiveMaxDurationSeconds: 120,
                encodedFrameCount: null,
                startedAtUtc: DateTimeOffset.UtcNow.AddSeconds(-1),
                updatedAtUtc: DateTimeOffset.UtcNow,
                current.Generation,
                current.Generation);
            var requests = new[]
            {
                CreateStopRequest(
                    Guid.NewGuid(),
                    new IpcGameViewRecordingStartBinding(
                        current.Process,
                        current.Runtime,
                        new UnityEditorGenerationSnapshot(
                            current.Generation.DomainReloadGeneration + 1,
                            current.Generation.PlayModeGeneration,
                            current.Generation.CompileGeneration,
                            current.Generation.AssetRefreshGeneration)),
                    DateTimeOffset.UtcNow.AddMinutes(1),
                    known: null),
                CreateStopRequest(
                    Guid.NewGuid(),
                    current,
                    DateTimeOffset.UtcNow.AddSeconds(-1),
                    known: null),
                CreateStopRequest(
                    preparing.RecordingId,
                    current,
                    DateTimeOffset.UtcNow.AddMinutes(1),
                    preparing),
            };

            foreach (var request in requests)
            {
                var response = InvokeStopAsync(fixture, request).GetAwaiter().GetResult();

                Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
                Assert.That(fixture.Registry.TryGetStopIntent(
                    request.RecordingId,
                    out _), Is.False);
            }

            var unregisteredFixture = CreateFixture(registerAdapter: false);
            var unregisteredBinding = unregisteredFixture.Projection.CaptureCurrentBinding();
            var unregisteredRequest = CreateStopRequest(
                Guid.NewGuid(),
                unregisteredBinding,
                DateTimeOffset.UtcNow.AddMinutes(1),
                known: null);

            var unregisteredResponse = InvokeStopAsync(
                    unregisteredFixture,
                    unregisteredRequest)
                .GetAwaiter()
                .GetResult();

            Assert.That(unregisteredResponse.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(unregisteredFixture.Registry.TryGetStopIntent(
                unregisteredRequest.RecordingId,
                out _), Is.False);
        }

        [Test]
        public void Stop_WhenGenerationChanged_ReturnsIndeterminateWithoutStoppingAnotherGeneration ()
        {
            var fixture = CreateFixture();
            var current = fixture.Projection.CaptureCurrentBinding();
            var startGeneration = new UnityEditorGenerationSnapshot(1, 2, 3, 3);
            var binding = new IpcGameViewRecordingStartBinding(
                current.Process,
                current.Runtime,
                startGeneration);
            var recordingId = Guid.NewGuid();
            var startedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5);
            var known = new IpcGameViewRecordingActiveSnapshot(
                recordingId,
                RequestDigest,
                MackySoft.Ucli.Contracts.Recording.GameViewRecordingState.Recording,
                binding.Runtime,
                target: null,
                effectiveMaxDurationSeconds: 120,
                encodedFrameCount: 3,
                startedAtUtc,
                updatedAtUtc: startedAtUtc,
                binding.Generation,
                binding.Generation);
            var payload = new IpcGameViewRecordingStopRequest(
                recordingId,
                RequestDigest,
                effectiveMaxDurationSeconds: 120,
                binding,
                DateTimeOffset.UtcNow.AddMinutes(1),
                known);
            var handler = new GameViewRecordingStopUnityIpcMethodHandler(
                fixture.Registry,
                fixture.Projection,
                new ImmediateUnityMutationLaneControl());

            var response = UnityIpcMethodHandlerTestInvoker.HandleAsync(
                    handler,
                    CreateRequest(UnityIpcMethod.RecordingStop, payload),
                    CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Ok));
            Assert.That(IpcPayloadCodec.TryDeserialize(
                response.Payload,
                out IpcGameViewRecordingStopResponse stop,
                out _), Is.True);
            var indeterminate = stop.Recording as IpcGameViewRecordingIndeterminateSnapshot;
            Assert.That(indeterminate, Is.Not.Null);
            Assert.That(indeterminate!.State, Is.EqualTo(
                MackySoft.Ucli.Contracts.Recording.GameViewRecordingState.Indeterminate));
            Assert.That(indeterminate.StartedAtUtc, Is.EqualTo(startedAtUtc));
            Assert.That(indeterminate.EncodedFrameCount, Is.EqualTo(3));
            Assert.That(fixture.Adapter.StopCallCount, Is.Zero);
        }

        private static async Task<IpcResponse> InvokeAsync (
            Fixture fixture,
            IpcGameViewRecordingStartBinding binding,
            DateTimeOffset dispatchDeadlineUtc,
            Guid? recordingId = null)
        {
            var payload = new IpcGameViewRecordingStartRequest(
                recordingId ?? Guid.NewGuid(),
                RequestDigest,
                new GameViewRecordingRequest(
                    GameViewRecordingRequest.CurrentSchemaVersion,
                    new PixelDimensions(640, 480),
                    frameRate: 30,
                    maxDurationSeconds: 120),
                binding,
                dispatchDeadlineUtc);
            var request = CreateRequest(UnityIpcMethod.RecordingStart, payload);
            return await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                fixture.Handler,
                request,
                CancellationToken.None);
        }

        private static IpcGameViewRecordingStopRequest CreateStopRequest (
            Guid recordingId,
            IpcGameViewRecordingStartBinding binding,
            DateTimeOffset dispatchDeadlineUtc,
            IpcGameViewRecordingSnapshot? known)
        {
            return new IpcGameViewRecordingStopRequest(
                recordingId,
                RequestDigest,
                effectiveMaxDurationSeconds: 120,
                binding,
                dispatchDeadlineUtc,
                known);
        }

        private static async Task<IpcResponse> InvokeStopAsync (
            Fixture fixture,
            IpcGameViewRecordingStopRequest payload)
        {
            var handler = new GameViewRecordingStopUnityIpcMethodHandler(
                fixture.Registry,
                fixture.Projection,
                new ImmediateUnityMutationLaneControl());
            return await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                handler,
                CreateRequest(UnityIpcMethod.RecordingStop, payload),
                CancellationToken.None);
        }

        private static async Task<IpcResponse> InvokeStatusAsync (
            Fixture fixture,
            IpcGameViewRecordingStatusRequest payload)
        {
            var handler = new GameViewRecordingStatusUnityIpcMethodHandler(
                fixture.Registry,
                fixture.Projection);
            return await UnityIpcMethodHandlerTestInvoker.HandleAsync(
                handler,
                CreateRequest(UnityIpcMethod.RecordingStatus, payload),
                CancellationToken.None);
        }

        private static GameViewRecordingSnapshot CreateObservedSnapshot (
            Guid recordingId,
            Sha256Digest requestDigest,
            IpcGameViewRecordingStartBinding binding)
        {
            var observedAtUtc = DateTimeOffset.UtcNow;
            return new GameViewRecordingSnapshot(
                recordingId,
                requestDigest,
                120,
                MackySoft.Ucli.Unity.Recording.GameViewRecordingState.Recording,
                MackySoft.Ucli.Unity.Recording.GameViewRecordingStopReason.None,
                GameViewRecordingFailure.None,
                binding.Runtime,
                Cleanup: null,
                Target: null,
                Timing: null,
                StartedAtUtc: observedAtUtc,
                StopRequestedAtUtc: null,
                CompletedAtUtc: null,
                UpdatedAtUtc: observedAtUtc,
                Message: null,
                StartBinding: binding);
        }

        private static IpcRequestEnvelope CreateRequest<TPayload> (
            UnityIpcMethod method,
            TPayload payload)
        {
            return new IpcRequestEnvelope(
                IpcProtocol.CurrentVersion,
                Guid.NewGuid(),
                "session-token",
                TextVocabulary.GetText(method),
                IpcPayloadCodec.SerializeToElement(payload),
                TextVocabulary.GetText(IpcResponseMode.Single),
                DateTimeOffset.UtcNow.AddMinutes(1),
                requestDeadlineRemainingMilliseconds: 60_000);
        }

        private static Fixture CreateFixture (bool registerAdapter = true)
        {
            var generation = new UnityEditorGenerationSnapshot(1, 2, 3, 4);
            var host = new UnityLifecycleExecutionHostContext(
                new ProcessIdentity(123, 456),
                Guid.NewGuid(),
                Guid.NewGuid(),
                recoveryLease: null);
            var projection = new GameViewRecordingIpcProjection(
                host,
                new FixedAvailabilitySource(generation));
            var registry = new GameViewRecordingAdapterRegistry();
            var adapter = new StubAdapter(
                projection.CaptureCurrentBinding().Runtime);
            if (registerAdapter)
            {
                Assert.That(registry.TryRegister(adapter, out _), Is.True);
            }
            return new Fixture(
                registry,
                projection,
                adapter,
                new GameViewRecordingStartUnityIpcMethodHandler(
                    registry,
                    projection,
                    CreateBootstrapContext(),
                    new ImmediateUnityMutationLaneControl()));
        }

        private static UnityDaemonBootstrapContext CreateBootstrapContext ()
        {
            var root = AbsolutePath.Parse(Path.Combine(
                Path.GetTempPath(),
                "ucli-recording-start-handler-tests"));
            return new UnityDaemonBootstrapContext(
                root,
                ProjectFingerprintTestFactory.Create("recording-start-handler"),
                AbsolutePath.Parse(Path.Combine(root.Value, "session.json")),
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                UnityIpcEndpointBinding.Create(new IpcEndpoint(
                    IpcTransportKind.NamedPipe,
                    $"ucli-recording-test-{Guid.NewGuid():N}")));
        }

        private sealed class Fixture
        {
            public Fixture (
                GameViewRecordingAdapterRegistry registry,
                GameViewRecordingIpcProjection projection,
                StubAdapter adapter,
                GameViewRecordingStartUnityIpcMethodHandler handler)
            {
                Registry = registry;
                Projection = projection;
                Adapter = adapter;
                Handler = handler;
            }

            public GameViewRecordingAdapterRegistry Registry { get; }

            public GameViewRecordingIpcProjection Projection { get; }

            public StubAdapter Adapter { get; }

            public GameViewRecordingStartUnityIpcMethodHandler Handler { get; }
        }

        private sealed class StubAdapter : IGameViewRecordingAdapter
        {
            private readonly GameViewRecordingRuntimeIdentity runtime;

            public StubAdapter (GameViewRecordingRuntimeIdentity runtime)
            {
                this.runtime = runtime;
                Metadata = new GameViewRecordingAdapterMetadata(
                    GameViewRecorderCompatibilityMetadata.AdapterId,
                    GameViewRecorderCompatibilityMetadata.AdapterVersion,
                    GameViewRecorderCompatibilityMetadata.PackageId,
                    GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                    "[6000.3.11f1,6000.3.12)",
                    GameViewRecordingEditorPlatform.Windows,
                    new ContractCaptureProfile(
                        ContractContainer.Mp4,
                        ContractCodec.H264,
                        audio: false,
                        alpha: false,
                        encodingProfile: "coreEncoder",
                        encodingQuality: "high",
                        ContractTimingMode.ConstantFrameRateCapture),
                    new ContractLimits(10, 4096, 10, 4096, 2, 1, 120, 120, 600));
            }

            public int StartCallCount { get; private set; }

            public int StopCallCount { get; private set; }

            public bool ReturnAcceptedTerminal { get; set; }

            public GameViewRecordingOperationResult? StatusResult { get; set; }

            public GameViewRecordingAdapterMetadata Metadata { get; }

            public event Action<GameViewRecordingSnapshot> StateChanged
            {
                add { }
                remove { }
            }

            public MackySoft.Ucli.Unity.Recording.GameViewRecordingRuntimeAdmission GetRuntimeAdmission ()
            {
                return new GameViewRecordingRuntimeReadyAdmission();
            }

            public GameViewRecordingOperationResult Start (GameViewRecordingStartRequest request)
            {
                StartCallCount++;
                if (ReturnAcceptedTerminal)
                {
                    var completedAtUtc = DateTimeOffset.UtcNow;
                    return GameViewRecordingOperationResult.Observed(
                        new GameViewRecordingSnapshot(
                            request.RecordingId,
                            request.RequestDigest,
                            (int)request.MaximumDuration.TotalSeconds,
                            MackySoft.Ucli.Unity.Recording.GameViewRecordingState.Indeterminate,
                            MackySoft.Ucli.Unity.Recording.GameViewRecordingStopReason.InternalFailure,
                            GameViewRecordingFailure.InternalFailure,
                            runtime,
                            Cleanup: null,
                            Target: null,
                            Timing: null,
                            StartedAtUtc: completedAtUtc,
                            StopRequestedAtUtc: completedAtUtc,
                            CompletedAtUtc: completedAtUtc,
                            UpdatedAtUtc: completedAtUtc,
                            Message: "Recorder accepted the session before initialization failed.",
                            StartBinding: request.StartBinding));
                }

                throw new InvalidOperationException("Adapter start must not be reached by these tests.");
            }

            public GameViewRecordingOperationResult GetStatus (Guid? recordingId)
            {
                return StatusResult ?? GameViewRecordingOperationResult.Failed(
                    GameViewRecordingFailure.NotFound,
                    "not found");
            }

            public GameViewRecordingOperationResult Stop (Guid recordingId)
            {
                StopCallCount++;
                throw new InvalidOperationException("Stop must not cross a recording binding.");
            }
        }

        private sealed class FixedAvailabilitySource : IUnityEditorAvailabilityObservationSource
        {
            private readonly UnityEditorRuntimeObservation observation;

            public FixedAvailabilitySource (UnityEditorGenerationSnapshot generation)
            {
                observation = new UnityEditorRuntimeObservation(
                    new UnityEditorStateSnapshot(
                        UnityEditorMode.Gui,
                        UnityEditorLifecycleState.Ready,
                        UnityEditorCompileState.Ready,
                        generation,
                        new UnityEditorPlayModeSnapshot(
                            UnityEditorPlayModeState.Playing,
                            UnityEditorPlayModeTransition.None,
                            IsPlaying: true,
                            IsPlayingOrWillChangePlaymode: true)),
                    DateTimeOffset.UtcNow);
            }

            public UnityEditorRuntimeObservation CaptureAvailabilityObservation ()
            {
                return observation;
            }
        }
    }
}
