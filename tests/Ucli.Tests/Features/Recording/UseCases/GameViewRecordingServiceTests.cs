using MackySoft.Ucli.Application.Features.Recording.Artifacts;
using MackySoft.Ucli.Application.Features.Recording.Capability;
using MackySoft.Ucli.Application.Features.Recording.Finalization;
using MackySoft.Ucli.Application.Features.Recording.Registry;
using MackySoft.Ucli.Application.Features.Recording.UseCases;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Recording;
using MackySoft.Ucli.Features.Recording.Artifacts;
using MackySoft.Ucli.Features.Recording.Artifacts.Mp4;
using MackySoft.Ucli.Features.Recording.Registry;
using MackySoft.Ucli.Infrastructure.Artifacts;

namespace MackySoft.Ucli.Tests.Features.Recording.UseCases;

public sealed class GameViewRecordingServiceTests
{
    private const string RequestJson =
        "{\"schemaVersion\":1,\"resolution\":{\"width\":1920,\"height\":1080},\"frameRate\":30,\"maxDurationSeconds\":120}";
    private const string DifferentRequestJson =
        "{\"schemaVersion\":1,\"resolution\":{\"width\":1920,\"height\":1080},\"frameRate\":24,\"maxDurationSeconds\":120}";

    private static readonly Guid RecordingId =
        Guid.Parse("2b5b01d2-00ed-40a1-b956-69de49a47629");
    private static readonly Guid OtherRecordingId =
        Guid.Parse("3f0f5790-8ccc-469e-b2ad-c7c0063fc66f");
    private static readonly Guid RuntimeId =
        Guid.Parse("ed74e94e-3607-461c-b95f-3c1799fcf8a8");
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 8, 5, 1, 0, 0, TimeSpan.Zero);
    private static readonly GameViewRecordingRuntimeIdentity Runtime = new(
        RuntimeId,
        "windows",
        "media-foundation",
        "1");
    private static readonly UnityEditorGenerationSnapshot StartGeneration =
        CreateGeneration(1);
    private static readonly IpcGameViewRecordingStartBinding StartBinding = new(
        new ProcessIdentity(ProcessId: 1234, Generation: 5678),
        Runtime,
        StartGeneration);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenCallerCancelsBeforeTheAcceptedSnapshotIsPersisted_CanRetryTheDurableStart ()
    {
        using var harness = new ServiceHarness();
        using var cancellation = new CancellationTokenSource();
        harness.Executor.CancelAfterStartResponse = cancellation;

        var result = await harness.Service.StartAsync(
            new GameViewRecordingStartInput(
                ProjectPath: null,
                RequestJson,
                RecordingId: null,
                Detach: false,
                TimeoutMilliseconds: 5_000),
            cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.Canceled, result.Error!.Kind);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.Error.Code);
        Assert.Equal(RecordingId, result.ExecutionCheckpoint!.ExecutionReference.Id);
        var stored = await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored.RuntimeSnapshot);

        var repeated = await harness.StartAsync(RecordingId, detach: true);

        Assert.True(repeated.IsSuccess);
        Assert.IsType<GameViewRecordingActivePayload>(repeated.Payload);
        Assert.Equal(2, harness.Executor.StartCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenTheInitialResponseIsLost_CanRetryTheSameDurableStart ()
    {
        using var harness = new ServiceHarness();
        harness.Executor.LoseNextStartResponse = true;

        var initial = await harness.StartAsync(RecordingId, detach: true);

        Assert.False(initial.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, initial.Error!.Code);
        Assert.Equal(RecordingId, initial.ExecutionCheckpoint!.ExecutionReference.Id);
        var stored = await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored.RuntimeSnapshot);

        var repeated = await harness.StartAsync(RecordingId, detach: true);

        Assert.True(repeated.IsSuccess);
        Assert.IsType<GameViewRecordingActivePayload>(repeated.Payload);
        Assert.Equal(2, harness.Executor.StartCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenDispatchDeadlineElapsedAndUnityDoesNotReportTheRecording_PublishesTheDurableTerminalResult ()
    {
        var timeProvider = new ManualTimeProvider(StartedAtUtc);
        using var harness = new ServiceHarness(timeProvider);
        harness.Executor.LoseNextStartResponse = true;

        var initial = await harness.StartAsync(RecordingId, detach: true);

        Assert.False(initial.IsSuccess);
        Assert.Null((await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None))!.RuntimeSnapshot);

        timeProvider.Advance(TimeSpan.FromSeconds(5));
        harness.Executor.ReturnNoRecordingStatus = true;

        var repeated = await harness.StartAsync(RecordingId, detach: true);

        var terminal = Assert.IsType<GameViewRecordingTerminalPayload>(repeated.Payload);
        Assert.Equal(GameViewRecordingState.Indeterminate, terminal.Progress.State);
        Assert.Contains(
            terminal.Diagnostics,
            diagnostic => diagnostic.Code == GameViewRecordingErrorCodes.DispatchDeadlineExceeded);
        Assert.Equal(1, harness.Executor.StartCount);
        var durable = await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None);
        Assert.NotNull(durable);
        Assert.True(durable.Payload.TryGetTerminal(out var durableTerminal));
        Assert.Contains(
            durableTerminal.Diagnostics,
            diagnostic => diagnostic.Code == GameViewRecordingErrorCodes.DispatchDeadlineExceeded);

        var other = await harness.StartAsync(OtherRecordingId, detach: true);

        Assert.True(other.IsSuccess);
        Assert.Equal(2, harness.Executor.StartCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenTheInitialDispatchDeadlineExpiresAfterDurableRegistration_ReturnsTheRecoveryCheckpointWithoutDispatching ()
    {
        var timeProvider = new ManualTimeProvider(StartedAtUtc);
        using var harness = new ServiceHarness(
            timeProvider,
            executionStore => new AdvancingRegistrationExecutionStore(
                executionStore,
                timeProvider,
                TimeSpan.FromSeconds(5)));

        var result = await harness.StartAsync(RecordingId, detach: true);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.MonitoringTimeout, result.Error!.Code);
        var recovery = Assert.IsType<GameViewRecordingRecoveryPayload>(result.ExecutionCheckpoint);
        Assert.Equal(GameViewRecordingState.Finalizing, recovery.Progress.State);
        Assert.Contains(
            recovery.Diagnostics,
            diagnostic => diagnostic.Code == GameViewRecordingErrorCodes.DispatchDeadlineExceeded);
        Assert.Equal(0, harness.Executor.StartCount);
        var durable = await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None);
        Assert.NotNull(durable);
        var durableRecovery = Assert.IsType<GameViewRecordingRecoveryPayload>(durable.Payload);
        Assert.Contains(
            durableRecovery.Diagnostics,
            diagnostic => diagnostic.Code == GameViewRecordingErrorCodes.DispatchDeadlineExceeded);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenARepeatedDispatchReportsDeadlineExceeded_ObservesThePossiblyAcceptedStart ()
    {
        using var harness = new ServiceHarness();
        harness.Executor.LoseNextStartResponse = true;

        var initial = await harness.StartAsync(RecordingId, detach: true);

        Assert.False(initial.IsSuccess);
        harness.Executor.StartResponseError = new OperationExecutionError(
            GameViewRecordingErrorCodes.DispatchDeadlineExceeded,
            "Unity observed the dispatch after its deadline.",
            InstancePath: null);
        harness.Executor.StatusSnapshotFactory = (request, _) =>
            ValueTask.FromResult<IpcGameViewRecordingSnapshot>(CreatePreparingSnapshot(request));

        var repeated = await harness.StartAsync(RecordingId, detach: true);

        Assert.True(repeated.IsSuccess);
        Assert.IsType<GameViewRecordingActivePayload>(repeated.Payload);
        Assert.Equal(2, harness.Executor.StartCount);
        Assert.Single(harness.Executor.StatusRequests);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WithTheSameIdConcurrently_ConvergesOnOneDurableExecution ()
    {
        using var harness = new ServiceHarness();
        var bothCapabilitiesObserved = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCapabilities = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var capabilityCount = 0;
        harness.Executor.BeforeCapabilityResponseAsync = async cancellationToken =>
        {
            if (Interlocked.Increment(ref capabilityCount) == 2)
            {
                bothCapabilitiesObserved.SetResult();
            }

            await releaseCapabilities.Task.WaitAsync(cancellationToken);
        };

        var first = harness.StartAsync(RecordingId, detach: true).AsTask();
        var second = harness.StartAsync(RecordingId, detach: true).AsTask();
        await bothCapabilitiesObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
        releaseCapabilities.SetResult();
        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        var firstPayload = Assert.IsAssignableFrom<GameViewRecordingExecutionPayload>(
            results[0].Payload);
        var secondPayload = Assert.IsAssignableFrom<GameViewRecordingExecutionPayload>(
            results[1].Payload);
        Assert.Equal(firstPayload.ExecutionReference.Id, secondPayload.ExecutionReference.Id);
        Assert.Equal(firstPayload.RequestDigest, secondPayload.RequestDigest);
        Assert.Equal(firstPayload.RequestRef, secondPayload.RequestRef);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenCapabilityTurnsBlockedAfterTheSameIdWasRegistered_ConvergesOnTheExecution ()
    {
        using var harness = new ServiceHarness();
        var staleCapabilityStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseStaleCapability = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var capabilityCount = 0;
        harness.Executor.BeforeCapabilityResponseAsync = async cancellationToken =>
        {
            if (Interlocked.Increment(ref capabilityCount) == 1)
            {
                staleCapabilityStarted.SetResult();
                await releaseStaleCapability.Task.WaitAsync(cancellationToken);
            }
        };

        var staleStart = harness.StartAsync(RecordingId, detach: true).AsTask();
        await staleCapabilityStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var registered = await harness.StartAsync(RecordingId, detach: true);
        harness.Executor.RuntimeAdmission = new GameViewRecordingRuntimeAdmission(
            GameViewRecordingRuntimeAdmissionState.Blocked,
            [GameViewRecordingErrorCodes.RequiresPlayMode]);
        releaseStaleCapability.SetResult();
        var repeated = await staleStart.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(registered.IsSuccess);
        Assert.True(repeated.IsSuccess);
        Assert.Equal(
            Assert.IsAssignableFrom<GameViewRecordingExecutionPayload>(registered.Payload)
                .ExecutionReference,
            Assert.IsAssignableFrom<GameViewRecordingExecutionPayload>(repeated.Payload)
                .ExecutionReference);
        Assert.Equal(1, harness.Executor.StartCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenTheDeadlineExpiresBeforeRegistration_ReturnsAGeneralTimeout ()
    {
        var timeProvider = new ManualTimeProvider(StartedAtUtc);
        using var harness = new ServiceHarness(timeProvider);
        harness.Executor.BeforeCapabilityResponseAsync = _ =>
        {
            timeProvider.Advance(TimeSpan.FromSeconds(2));
            return ValueTask.CompletedTask;
        };

        var result = await harness.Service.StartAsync(
            new GameViewRecordingStartInput(
                ProjectPath: null,
                RequestJson,
                RecordingId,
                Detach: true,
                TimeoutMilliseconds: 1_000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        Assert.Null(result.ExecutionCheckpoint);
        Assert.Null(await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenProjectResolutionConsumesTheDeadline_DoesNotStartRecordingIpc ()
    {
        var timeProvider = new ManualTimeProvider(StartedAtUtc);
        using var harness = new ServiceHarness(
            timeProvider,
            projectContextResolverFactory: context => new AdvancingProjectContextResolver(
                context,
                timeProvider,
                TimeSpan.FromSeconds(2)));

        var result = await harness.Service.StartAsync(
            new GameViewRecordingStartInput(
                ProjectPath: null,
                RequestJson,
                RecordingId,
                Detach: true,
                TimeoutMilliseconds: 1_000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        Assert.Equal(0, harness.Executor.CapabilityCount);
        Assert.Equal(0, harness.Executor.StartCount);
        Assert.Null(await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenARegisteredStartIsRejected_ReturnsTheDurableExecutionCheckpoint ()
    {
        using var harness = new ServiceHarness();
        harness.Executor.StartResponseError = new OperationExecutionError(
            GameViewRecordingErrorCodes.RequiresPlayMode,
            "Play Mode ended before the start response was returned.",
            InstancePath: null);

        var result = await harness.StartAsync(RecordingId, detach: true);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.RequiresPlayMode, result.Error!.Code);
        Assert.Equal(
            RecordingId,
            Assert.IsAssignableFrom<GameViewRecordingExecutionPayload>(result.ExecutionCheckpoint)
                .ExecutionReference.Id);
        Assert.NotNull(await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenMonitoringDeadlineExpires_ReturnsTheContinuingRecordingReference ()
    {
        var timeProvider = new ManualTimeProvider(StartedAtUtc);
        using var harness = new ServiceHarness(timeProvider);

        var execution = harness.Service.StartAsync(
            new GameViewRecordingStartInput(
                ProjectPath: null,
                RequestJson,
                RecordingId,
                Detach: false,
                TimeoutMilliseconds: 1_000),
            CancellationToken.None).AsTask();
        await timeProvider.WaitForTimerDueWithinAsync(TimeSpan.FromSeconds(1));
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        var result = await execution.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.MonitoringTimeout, result.Error!.Code);
        Assert.Equal(RecordingId, result.ExecutionCheckpoint!.ExecutionReference.Id);
        Assert.NotNull(await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WithTheSameIdAndRequest_ObservesTheExistingExecutionWithoutStartingAgain ()
    {
        using var harness = new ServiceHarness();
        var first = await harness.StartAsync(RecordingId, detach: true);
        var active = Assert.IsType<GameViewRecordingActivePayload>(first.Payload);
        harness.Executor.ReturnTerminalStatus = true;

        var repeated = await harness.StartAsync(RecordingId, detach: false);

        var terminal = Assert.IsType<GameViewRecordingTerminalPayload>(repeated.Payload);
        Assert.Equal(GameViewRecordingState.Indeterminate, terminal.Progress.State);
        Assert.Equal(1, harness.Executor.StartCount);
        var statusRequest = Assert.Single(harness.Executor.StatusRequests);
        Assert.Equal(active.ExecutionRef.Id, statusRequest.RecordingId);
        Assert.Equal(GameViewRecordingState.Recording, statusRequest.KnownRecording!.State);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_RetryWhenProjectResolutionConsumesDeadline_ReturnsDurableCheckpointWithoutOpeningArtifactsOrIpc ()
    {
        var timeProvider = new ManualTimeProvider(StartedAtUtc);
        AdvancingProjectContextResolver? resolver = null;
        CountingArtifactStore? artifactStore = null;
        using var harness = new ServiceHarness(
            timeProvider,
            projectContextResolverFactory: context => resolver = new AdvancingProjectContextResolver(
                context,
                timeProvider,
                TimeSpan.FromSeconds(2))
            {
                IsAdvancing = false,
            },
            decorateArtifactStore: store => artifactStore = new CountingArtifactStore(store));
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        var durable = Assert.IsType<GameViewRecordingStoredExecution>(
            await harness.ExecutionStore.ReadAsync(harness.Project, RecordingId, CancellationToken.None));
        var capabilityCount = harness.Executor.CapabilityCount;
        resolver!.IsAdvancing = true;

        var result = await harness.Service.StartAsync(
            new GameViewRecordingStartInput(
                ProjectPath: null,
                RequestJson,
                RecordingId,
                Detach: true,
                TimeoutMilliseconds: 1_000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(GameViewRecordingErrorCodes.MonitoringTimeout, result.Error!.Code);
        Assert.Equal(durable.Payload.ExecutionReference, result.ExecutionCheckpoint!.ExecutionReference);
        Assert.Equal(capabilityCount, harness.Executor.CapabilityCount);
        Assert.Equal(0, artifactStore!.OpenCount);
        Assert.Equal(1, harness.Executor.StartCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_AdmissionDistinguishesIdReuseFromAnotherActiveRecording ()
    {
        using var harness = new ServiceHarness();
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);

        var idReuse = await harness.Service.StartAsync(
            new GameViewRecordingStartInput(
                ProjectPath: null,
                DifferentRequestJson,
                RecordingId,
                Detach: true,
                TimeoutMilliseconds: 5_000),
            CancellationToken.None);
        var concurrent = await harness.StartAsync(OtherRecordingId, detach: true);

        Assert.Equal(GameViewRecordingErrorCodes.IdConflict, idReuse.Error!.Code);
        Assert.Equal(GameViewRecordingErrorCodes.Conflict, concurrent.Error!.Code);
        Assert.Equal(1, harness.Executor.StartCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenRuntimeObservationTimesOut_ReturnsIpcTimeoutAndDurableCheckpoint ()
    {
        using var harness = new ServiceHarness();
        var empty = await harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId: null, TimeoutMilliseconds: 5_000),
            CancellationToken.None);
        Assert.IsType<NoGameViewRecordingSelection>(
            Assert.IsType<GameViewRecordingStatusPayload>(empty.Payload).RecordingSelection);

        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        harness.Executor.StatusFailure = UnityRequestExecutionResult.Failure(
            new UnityRequestFailure(
                UnityRequestFailureKind.TransportInterrupted,
                ExecutionErrorCodes.IpcTimeout,
                "Runtime status could not be observed."));

        var result = await harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId: null, TimeoutMilliseconds: 5_000),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.IpcTimeout, result.Error!.Code);
        Assert.IsType<GameViewRecordingActivePayload>(result.ExecutionCheckpoint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenTheRuntimeCannotBeIdentified_DoesNotSelectARecordingWithoutItsId ()
    {
        using var harness = new ServiceHarness();
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        harness.Executor.CapabilityFailure = UnityRequestExecutionResult.Failure(
            new UnityRequestFailure(
                UnityRequestFailureKind.TransportInterrupted,
                ExecutionErrorCodes.IpcTimeout,
                "The Unity runtime could not be identified."));

        var current = await harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId: null, TimeoutMilliseconds: 5_000),
            CancellationToken.None);
        var byId = await harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            CancellationToken.None);

        var currentPayload = Assert.IsType<GameViewRecordingStatusPayload>(current.Payload);
        Assert.Equal(
            GameViewRecordingRuntimeAdmissionState.Unobserved,
            currentPayload.Capability.RuntimeAdmission.State);
        Assert.IsType<NoGameViewRecordingSelection>(currentPayload.RecordingSelection);
        var selected = Assert.IsType<SelectedGameViewRecordingSelection>(
            Assert.IsType<GameViewRecordingStatusPayload>(byId.Payload).RecordingSelection);
        Assert.Equal(RecordingId, selected.Recording.ExecutionReference.Id);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenANewEditorReportsNoRecordingAfterTheBoundProcessExited_PublishesAnIndeterminateTerminalExecution ()
    {
        using var harness = new ServiceHarness();
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        harness.Executor.ReturnNoRecordingStatus = true;
        harness.ProcessIdentityObserver.Status = ProcessIdentityStatus.ExitedOrReplaced;

        var result = await harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            CancellationToken.None);

        var status = Assert.IsType<GameViewRecordingStatusPayload>(result.Payload);
        var selection = Assert.IsType<SelectedGameViewRecordingSelection>(status.RecordingSelection);
        var terminal = Assert.IsType<GameViewRecordingTerminalPayload>(selection.Recording);
        Assert.Equal(GameViewRecordingState.Indeterminate, terminal.Progress.State);
        Assert.Equal(
            GameViewRecordingStopReason.UnityExited,
            terminal.TerminalSummary.StopReason);
        Assert.Equal(StartBinding.Process, harness.ProcessIdentityObserver.LastObservedProcess);
        Assert.Equal(1, harness.TerminalFinalizer.FinalizationCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenCapabilityObservationIsCanceled_ReturnsTheDurableExecution ()
    {
        using var harness = new ServiceHarness();
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        var capabilityObservationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Executor.BeforeCapabilityResponseAsync = async cancellationToken =>
        {
            capabilityObservationStarted.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };
        using var cancellation = new CancellationTokenSource();

        var status = harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            cancellation.Token).AsTask();
        await capabilityObservationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        var result = await status.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(ExecutionErrorCodes.Canceled, result.Error!.Code);
        Assert.IsType<GameViewRecordingActivePayload>(result.ExecutionCheckpoint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenAnOlderObservationCompletesAfterTerminalPublication_ReturnsTheTerminalCheckpoint ()
    {
        using var harness = new ServiceHarness();
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        var olderObservationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseOlderObservation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observationIndex = 0;
        harness.Executor.StatusSnapshotFactory = async (request, _) =>
        {
            if (Interlocked.Increment(ref observationIndex) == 1)
            {
                olderObservationStarted.SetResult();
                await releaseOlderObservation.Task;
                return request.KnownRecording!;
            }

            return CreateTerminalSnapshot(request.KnownRecording as IpcGameViewRecordingActiveSnapshot
                ?? throw new InvalidOperationException(
                    "A durable active runtime observation was expected."));
        };

        var olderStatus = harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            CancellationToken.None).AsTask();
        await olderObservationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var terminalStatus = await harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            CancellationToken.None);
        releaseOlderObservation.SetResult();
        var completedOlderStatus = await olderStatus.WaitAsync(TimeSpan.FromSeconds(5));

        AssertTerminalStatus(terminalStatus);
        AssertTerminalStatus(completedOlderStatus);
        Assert.IsType<GameViewRecordingTerminalPayload>((await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None))!.Payload);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhileTerminalPublicationIsOwned_ReturnsRecoveryWithoutFinalizingAgain ()
    {
        using var harness = new ServiceHarness();
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        harness.Executor.ReturnTerminalStatus = true;
        var firstFinalizationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstFinalization = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        harness.TerminalFinalizer.BeforeFinalizeAsync = async (attempt, cancellationToken) =>
        {
            if (attempt == 1)
            {
                firstFinalizationStarted.SetResult();
                await releaseFirstFinalization.Task.WaitAsync(cancellationToken);
            }
        };

        var first = harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            CancellationToken.None).AsTask();
        await firstFinalizationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            var concurrent = await harness.Service.StopAsync(
                new GameViewRecordingStopInput(null, RecordingId, TimeoutMilliseconds: 250),
                CancellationToken.None);

            Assert.IsType<GameViewRecordingRecoveryPayload>(concurrent.Payload);
            Assert.Equal(1, harness.TerminalFinalizer.FinalizationCount);
            Assert.Equal(0, harness.Executor.StopCount);
        }
        finally
        {
            releaseFirstFinalization.SetResult();
        }

        AssertTerminalStatus(await first.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, harness.TerminalFinalizer.FinalizationCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhileTerminalPublicationLeaseIsOwned_ReturnsTheDurableRecoveryCheckpoint ()
    {
        DelayedTerminalPublicationLeaseStore? delayedStore = null;
        using var harness = new ServiceHarness(decorateExecutionStore: inner =>
        {
            delayedStore = new DelayedTerminalPublicationLeaseStore(inner);
            return delayedStore;
        });
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        harness.Executor.ReturnTerminalStatus = true;

        var first = harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            CancellationToken.None).AsTask();
        await delayedStore!.FirstLeaseAcquired.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<GameViewRecordingRecoveryPayload>((await harness.ExecutionStore.ReadAsync(
            harness.Project,
            RecordingId,
            CancellationToken.None))!.Payload);
        try
        {
            var concurrent = await harness.Service.StopAsync(
                new GameViewRecordingStopInput(null, RecordingId, TimeoutMilliseconds: 250),
                CancellationToken.None);

            Assert.True(concurrent.IsSuccess);
            Assert.IsType<GameViewRecordingRecoveryPayload>(concurrent.Payload);
            Assert.Equal(0, harness.TerminalFinalizer.FinalizationCount);
            Assert.Equal(0, harness.Executor.StopCount);
        }
        finally
        {
            delayedStore.ReleaseFirstLease();
        }

        AssertTerminalStatus(await first.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(1, harness.TerminalFinalizer.FinalizationCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenCanceledAfterAnotherCallerPublishesTerminal_ReturnsTheLatestDurableCheckpoint ()
    {
        using var harness = new ServiceHarness();
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        var canceledObservationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observationIndex = 0;
        harness.Executor.StatusSnapshotFactory = async (request, cancellationToken) =>
        {
            if (Interlocked.Increment(ref observationIndex) == 1)
            {
                canceledObservationStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return CreateTerminalSnapshot(request.KnownRecording as IpcGameViewRecordingActiveSnapshot
                ?? throw new InvalidOperationException(
                    "A durable active runtime observation was expected."));
        };
        using var cancellation = new CancellationTokenSource();

        var canceledStatus = harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            cancellation.Token).AsTask();
        await canceledObservationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var terminalStatus = await harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            CancellationToken.None);
        cancellation.Cancel();
        var result = await canceledStatus.WaitAsync(TimeSpan.FromSeconds(5));

        AssertTerminalStatus(terminalStatus);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.Error!.Code);
        Assert.IsType<GameViewRecordingTerminalPayload>(result.ExecutionCheckpoint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Status_WhenCallerCancelsDuringTerminalFinalization_PassesANonCancelableTokenToTheFinalizer ()
    {
        using var harness = new ServiceHarness();
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        harness.Executor.ReturnTerminalStatus = true;
        var finalizationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFinalization = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        harness.TerminalFinalizer.BeforeFinalizeAsync = async (_, cancellationToken) =>
        {
            Assert.False(cancellationToken.CanBeCanceled);
            finalizationStarted.SetResult();
            await releaseFinalization.Task.WaitAsync(cancellationToken);
        };
        using var callerCancellation = new CancellationTokenSource();

        var status = harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            callerCancellation.Token).AsTask();
        await finalizationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        callerCancellation.Cancel();
        releaseFinalization.SetResult();
        var result = await status.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.Error!.Code);
        Assert.IsType<GameViewRecordingTerminalPayload>(result.ExecutionCheckpoint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenMonitoringIsCanceledAfterTerminalPublication_ReturnsTheLatestDurableCheckpoint ()
    {
        using var harness = new ServiceHarness();
        var canceledObservationStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var observationIndex = 0;
        harness.Executor.StatusSnapshotFactory = async (request, cancellationToken) =>
        {
            if (Interlocked.Increment(ref observationIndex) == 1)
            {
                canceledObservationStarted.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            return CreateTerminalSnapshot(request.KnownRecording as IpcGameViewRecordingActiveSnapshot
                ?? throw new InvalidOperationException(
                    "A durable active runtime observation was expected."));
        };
        using var cancellation = new CancellationTokenSource();

        var monitoredStart = harness.Service.StartAsync(
            new GameViewRecordingStartInput(
                ProjectPath: null,
                RequestJson,
                RecordingId,
                Detach: false,
                TimeoutMilliseconds: 5_000),
            cancellation.Token).AsTask();
        await canceledObservationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var terminalStatus = await harness.Service.GetStatusAsync(
            new GameViewRecordingStatusInput(null, RecordingId, TimeoutMilliseconds: 5_000),
            CancellationToken.None);
        cancellation.Cancel();
        var result = await monitoredStart.WaitAsync(TimeSpan.FromSeconds(5));

        AssertTerminalStatus(terminalStatus);
        Assert.Equal(ExecutionErrorCodes.Canceled, result.Error!.Code);
        Assert.IsType<GameViewRecordingTerminalPayload>(result.ExecutionCheckpoint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Stop_WhenStatusRecoversATerminalExecution_DoesNotIssueARecorderStop ()
    {
        using var harness = new ServiceHarness();
        Assert.True((await harness.StartAsync(RecordingId, detach: true)).IsSuccess);
        harness.Executor.ReturnTerminalStatus = true;

        var first = await harness.StopAsync(RecordingId);
        var repeated = await harness.StopAsync(RecordingId);

        Assert.IsType<GameViewRecordingTerminalPayload>(first.Payload);
        Assert.IsType<GameViewRecordingTerminalPayload>(repeated.Payload);
        Assert.Single(harness.Executor.StatusRequests);
        Assert.Equal(0, harness.Executor.StopCount);
    }

    private static void AssertTerminalStatus (
        GameViewRecordingServiceResult<GameViewRecordingStatusPayload> result)
    {
        var payload = Assert.IsType<GameViewRecordingStatusPayload>(result.Payload);
        var selection = Assert.IsType<SelectedGameViewRecordingSelection>(payload.RecordingSelection);
        Assert.IsType<GameViewRecordingTerminalPayload>(selection.Recording);
    }

    private sealed class ServiceHarness : IDisposable
    {
        private readonly TestDirectoryScope scope;

        public ServiceHarness (
            TimeProvider? timeProvider = null,
            Func<IGameViewRecordingExecutionStore, IGameViewRecordingExecutionStore>? decorateExecutionStore = null,
            Func<ProjectContext, IProjectContextResolver>? projectContextResolverFactory = null,
            Func<IGameViewRecordingArtifactStore, IGameViewRecordingArtifactStore>? decorateArtifactStore = null)
        {
            scope = TestDirectories.CreateTempScope(
                "game-view-recording-service",
                Guid.NewGuid().ToString("N"));
            Project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(
                scope,
                ProjectFingerprintTestFactory.Create("game-view-recording-service"),
                unityVersion: "6000.3.11f1");
            var context = new ProjectContext(
                Project,
                UcliConfig.CreateDefault(),
                ConfigSource.Default);
            Executor = new RecordingRequestExecutor();
            ExecutionStore = new FileGameViewRecordingExecutionStore();
            var serviceExecutionStore = decorateExecutionStore?.Invoke(ExecutionStore)
                ?? ExecutionStore;
            var artifactStore = new FileGameViewRecordingArtifactStore(
                new ImmutableArtifactFilePublisher(static () => StartedAtUtc),
                new GameViewRecordingMp4Validator());
            TerminalFinalizer = new SuccessfulTerminalFinalizer();
            ProcessIdentityObserver = new RecordingProcessIdentityObserver();
            Service = new GameViewRecordingService(
                projectContextResolverFactory?.Invoke(context) ?? new FixedProjectContextResolver(context),
                new GameViewRecordingCapabilityResolver(
                    new ResolvedRecorderPackageResolver(),
                    Executor),
                Executor,
                decorateArtifactStore?.Invoke(artifactStore) ?? artifactStore,
                serviceExecutionStore,
                TerminalFinalizer,
                ProcessIdentityObserver,
                new FixedGuidGenerator(RecordingId),
                timeProvider ?? TimeProvider.System);
        }

        public ResolvedUnityProjectContext Project { get; }

        public FileGameViewRecordingExecutionStore ExecutionStore { get; }

        public RecordingRequestExecutor Executor { get; }

        public SuccessfulTerminalFinalizer TerminalFinalizer { get; }

        public RecordingProcessIdentityObserver ProcessIdentityObserver { get; }

        public GameViewRecordingService Service { get; }

        public ValueTask<GameViewRecordingServiceResult<GameViewRecordingExecutionPayload>> StartAsync (
            Guid recordingId,
            bool detach) =>
            Service.StartAsync(
                new GameViewRecordingStartInput(
                    ProjectPath: null,
                    RequestJson,
                    recordingId,
                    detach,
                    TimeoutMilliseconds: 5_000),
                CancellationToken.None);

        public ValueTask<GameViewRecordingServiceResult<GameViewRecordingStopResultPayload>> StopAsync (
            Guid recordingId) =>
            Service.StopAsync(
                new GameViewRecordingStopInput(
                    ProjectPath: null,
                    recordingId,
                    TimeoutMilliseconds: 5_000),
                CancellationToken.None);

        public void Dispose () => scope.Dispose();
    }

    private sealed class AdvancingRegistrationExecutionStore : IGameViewRecordingExecutionStore
    {
        private readonly IGameViewRecordingExecutionStore inner;
        private readonly ManualTimeProvider timeProvider;
        private readonly TimeSpan elapsed;
        private int hasAdvanced;

        public AdvancingRegistrationExecutionStore (
            IGameViewRecordingExecutionStore inner,
            ManualTimeProvider timeProvider,
            TimeSpan elapsed)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
            this.elapsed = elapsed;
        }

        public ValueTask<GameViewRecordingStoredExecution?> ReadAsync (
            ResolvedUnityProjectContext project,
            Guid recordingId,
            CancellationToken cancellationToken = default) =>
            inner.ReadAsync(project, recordingId, cancellationToken);

        public ValueTask<GameViewRecordingStoredExecution?> ReadCurrentAsync (
            ResolvedUnityProjectContext project,
            Guid runtimeId,
            CancellationToken cancellationToken = default) =>
            inner.ReadCurrentAsync(project, runtimeId, cancellationToken);

        public ValueTask WriteAsync (
            ResolvedUnityProjectContext project,
            AbsolutePath executionStatePath,
            GameViewRecordingStoredExecution execution,
            CancellationToken cancellationToken = default) =>
            inner.WriteAsync(project, executionStatePath, execution, cancellationToken);

        public ValueTask<GameViewRecordingCheckpointExchangeResult> CompareExchangeAsync (
            ResolvedUnityProjectContext project,
            AbsolutePath executionStatePath,
            GameViewRecordingStoredExecution expected,
            GameViewRecordingStoredExecution replacement,
            CancellationToken cancellationToken = default) =>
            inner.CompareExchangeAsync(
                project,
                executionStatePath,
                expected,
                replacement,
                cancellationToken);

        public async ValueTask<IGameViewRecordingAdmissionLease?> TryAcquireAdmissionLeaseAsync (
            ResolvedUnityProjectContext project,
            Guid recordingId,
            IpcGameViewRecordingStartBinding startBinding,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var lease = await inner.TryAcquireAdmissionLeaseAsync(
                    project,
                    recordingId,
                    startBinding,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            return lease is null
                ? null
                : new AdvancingRegistrationAdmissionLease(
                    lease,
                    () =>
                    {
                        if (Interlocked.Exchange(ref hasAdvanced, 1) == 0)
                        {
                            timeProvider.Advance(elapsed);
                        }
                    });
        }

        public ValueTask<IGameViewRecordingTerminalPublicationLease?> TryAcquireTerminalPublicationLeaseAsync (
            ResolvedUnityProjectContext project,
            Guid recordingId,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            inner.TryAcquireTerminalPublicationLeaseAsync(
                project,
                recordingId,
                timeout,
                cancellationToken);
    }

    private sealed class AdvancingRegistrationAdmissionLease : IGameViewRecordingAdmissionLease
    {
        private readonly IGameViewRecordingAdmissionLease inner;
        private readonly Action afterRegistration;

        public AdvancingRegistrationAdmissionLease (
            IGameViewRecordingAdmissionLease inner,
            Action afterRegistration)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.afterRegistration = afterRegistration ?? throw new ArgumentNullException(nameof(afterRegistration));
        }

        public ResolvedUnityProjectContext Project => inner.Project;

        public Guid RecordingId => inner.RecordingId;

        public IpcGameViewRecordingStartBinding StartBinding => inner.StartBinding;

        public async ValueTask<GameViewRecordingRegistrationResult> TryRegisterAsync (
            AbsolutePath executionStatePath,
            GameViewRecordingStoredExecution execution,
            CancellationToken cancellationToken = default)
        {
            var registration = await inner.TryRegisterAsync(
                    executionStatePath,
                    execution,
                    cancellationToken)
                .ConfigureAwait(false);
            if (registration.Registered)
            {
                afterRegistration();
            }

            return registration;
        }

        public void Dispose () => inner.Dispose();
    }

    private sealed class DelayedTerminalPublicationLeaseStore : IGameViewRecordingExecutionStore
    {
        private readonly IGameViewRecordingExecutionStore inner;
        private readonly TaskCompletionSource firstLeaseAcquired = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource releaseFirstLease = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int leaseCount;

        public DelayedTerminalPublicationLeaseStore (IGameViewRecordingExecutionStore inner)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public Task FirstLeaseAcquired => firstLeaseAcquired.Task;

        public void ReleaseFirstLease () => releaseFirstLease.TrySetResult();

        public ValueTask<GameViewRecordingStoredExecution?> ReadAsync (
            ResolvedUnityProjectContext project,
            Guid recordingId,
            CancellationToken cancellationToken = default) =>
            inner.ReadAsync(project, recordingId, cancellationToken);

        public ValueTask<GameViewRecordingStoredExecution?> ReadCurrentAsync (
            ResolvedUnityProjectContext project,
            Guid runtimeId,
            CancellationToken cancellationToken = default) =>
            inner.ReadCurrentAsync(project, runtimeId, cancellationToken);

        public ValueTask WriteAsync (
            ResolvedUnityProjectContext project,
            AbsolutePath executionStatePath,
            GameViewRecordingStoredExecution execution,
            CancellationToken cancellationToken = default) =>
            inner.WriteAsync(project, executionStatePath, execution, cancellationToken);

        public ValueTask<GameViewRecordingCheckpointExchangeResult> CompareExchangeAsync (
            ResolvedUnityProjectContext project,
            AbsolutePath executionStatePath,
            GameViewRecordingStoredExecution expected,
            GameViewRecordingStoredExecution replacement,
            CancellationToken cancellationToken = default) =>
            inner.CompareExchangeAsync(
                project,
                executionStatePath,
                expected,
                replacement,
                cancellationToken);

        public ValueTask<IGameViewRecordingAdmissionLease?> TryAcquireAdmissionLeaseAsync (
            ResolvedUnityProjectContext project,
            Guid recordingId,
            IpcGameViewRecordingStartBinding startBinding,
            TimeSpan timeout,
            CancellationToken cancellationToken = default) =>
            inner.TryAcquireAdmissionLeaseAsync(
                project,
                recordingId,
                startBinding,
                timeout,
                cancellationToken);

        public async ValueTask<IGameViewRecordingTerminalPublicationLease?> TryAcquireTerminalPublicationLeaseAsync (
            ResolvedUnityProjectContext project,
            Guid recordingId,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var lease = await inner.TryAcquireTerminalPublicationLeaseAsync(
                    project,
                    recordingId,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
            if (lease is not null && Interlocked.Increment(ref leaseCount) == 1)
            {
                firstLeaseAcquired.TrySetResult();
                await releaseFirstLease.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            return lease;
        }

    }

    private sealed class FixedProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContext context;

        public FixedProjectContextResolver (ProjectContext context)
        {
            this.context = context;
        }

        public ValueTask<ProjectContextResolutionResult> ResolveAsync (
            AbsolutePath? projectPath,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ProjectContextResolutionResult.Success(context));
    }

    private sealed class AdvancingProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContext context;
        private readonly ManualTimeProvider timeProvider;
        private readonly TimeSpan elapsed;

        public AdvancingProjectContextResolver (
            ProjectContext context,
            ManualTimeProvider timeProvider,
            TimeSpan elapsed)
        {
            this.context = context;
            this.timeProvider = timeProvider;
            this.elapsed = elapsed;
        }

        public bool IsAdvancing { get; set; } = true;

        public ValueTask<ProjectContextResolutionResult> ResolveAsync (
            AbsolutePath? projectPath,
            CancellationToken cancellationToken = default)
        {
            if (IsAdvancing)
            {
                timeProvider.Advance(elapsed);
            }
            return ValueTask.FromResult(ProjectContextResolutionResult.Success(context));
        }
    }

    private sealed class CountingArtifactStore : IGameViewRecordingArtifactStore
    {
        private readonly IGameViewRecordingArtifactStore inner;

        public CountingArtifactStore (IGameViewRecordingArtifactStore inner)
        {
            this.inner = inner;
        }

        public int OpenCount { get; private set; }

        public GameViewRecordingArtifactPreparationResult Prepare (
            ResolvedUnityProjectContext unityProject,
            Guid recordingId,
            IGameViewRecordingAdmissionLease admissionLease) =>
            inner.Prepare(unityProject, recordingId, admissionLease);

        public GameViewRecordingArtifactOpenResult Open (
            ResolvedUnityProjectContext unityProject,
            Guid recordingId)
        {
            OpenCount++;
            return inner.Open(unityProject, recordingId);
        }
    }

    private sealed class ResolvedRecorderPackageResolver : IGameViewRecorderPackageResolver
    {
        public ValueTask<GameViewRecorderPackageResolution> ResolveAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(GameViewRecorderPackageResolution.Resolved("5.1.5"));
    }

    private sealed class FixedGuidGenerator : IGuidGenerator
    {
        private readonly Guid value;

        public FixedGuidGenerator (Guid value)
        {
            this.value = value;
        }

        public Guid Generate () => value;
    }

    private sealed class RecordingProcessIdentityObserver : IProcessIdentityObserver
    {
        public ProcessIdentityStatus Status { get; set; } = ProcessIdentityStatus.Unobservable;

        public ProcessIdentity? LastObservedProcess { get; private set; }

        public ProcessIdentityStatus Observe (ProcessIdentity process)
        {
            LastObservedProcess = process;
            return Status;
        }
    }

    private sealed class RecordingRequestExecutor : IUnityRequestExecutor
    {
        private static readonly GameViewRecordingAdapterCapability Adapter = new(
            GameViewRecordingAdapterState.Registered,
            GameViewRecorderCompatibilityMetadata.AdapterId,
            GameViewRecorderCompatibilityMetadata.AdapterVersion);
        private static readonly GameViewRecordingRuntimeAdmission ReadyRuntimeAdmission = new(
            GameViewRecordingRuntimeAdmissionState.Ready,
            Array.Empty<UcliCode>());
        private static readonly GameViewRecordingLimits Limits = new(
            minimumWidth: 2,
            maximumWidth: 3840,
            minimumHeight: 2,
            maximumHeight: 2160,
            dimensionMultiple: 2,
            minimumFrameRate: 1,
            maximumFrameRate: 60,
            defaultMaxDurationSeconds: 120,
            maximumMaxDurationSeconds: 600);
        private static readonly GameViewRecordingCaptureProfile CaptureProfile = new(
            GameViewRecordingContainer.Mp4,
            GameViewRecordingCodec.H264,
            audio: false,
            alpha: false,
            encodingProfile: "h264-main",
            encodingQuality: "high",
            GameViewRecordingTimingMode.ConstantFrameRateCapture);

        public CancellationTokenSource? CancelAfterStartResponse { get; set; }

        public Func<CancellationToken, ValueTask>? BeforeCapabilityResponseAsync { get; set; }

        public Func<CancellationToken, ValueTask>? BeforeStartResponseAsync { get; set; }

        public OperationExecutionError? StartResponseError { get; set; }

        public UnityRequestExecutionResult? CapabilityFailure { get; set; }

        public GameViewRecordingRuntimeAdmission RuntimeAdmission { get; set; } =
            ReadyRuntimeAdmission;

        public bool ReturnTerminalStatus { get; set; }

        public bool ReturnNoRecordingStatus { get; set; }

        public bool LoseNextStartResponse { get; set; }

        public UnityRequestExecutionResult? StatusFailure { get; set; }

        public Func<
            IpcGameViewRecordingStatusRequest,
            CancellationToken,
            ValueTask<IpcGameViewRecordingSnapshot>>? StatusSnapshotFactory
        { get; set; }

        public int StartCount { get; private set; }

        public int CapabilityCount { get; private set; }

        public int StopCount { get; private set; }

        public List<IpcGameViewRecordingStatusRequest> StatusRequests { get; } = [];

        public ValueTask<UnityRequestExecutionResult> ExecuteAsync (
            UcliCommand command,
            UnityExecutionMode mode,
            TimeSpan timeout,
            UcliConfig config,
            ResolvedUnityProjectContext unityProject,
            UnityRequestPayload payload,
            CancellationToken cancellationToken = default)
        {
            if (payload is UnityRequestPayload.RecordingCapability)
            {
                CapabilityCount++;
            }

            if (payload is UnityRequestPayload.RecordingCapability
                && BeforeCapabilityResponseAsync is not null)
            {
                return CreateCapabilityResponseAsync(cancellationToken);
            }

            if (payload is UnityRequestPayload.RecordingCapability
                && CapabilityFailure is { } capabilityFailure)
            {
                return ValueTask.FromResult(capabilityFailure);
            }

            if (payload is UnityRequestPayload.RecordingStatus status)
            {
                return CreateStatusResponseAsync(status.Request, cancellationToken);
            }

            if (payload is UnityRequestPayload.RecordingStart start)
            {
                return CreateStartResponseAsync(start.Request, cancellationToken);
            }

            var result = payload switch
            {
                UnityRequestPayload.RecordingCapability => Success(
                    new IpcGameViewRecordingCapabilityResponse(
                        Adapter,
                        RuntimeAdmission,
                        Limits,
                        CaptureProfile,
                        RuntimeAdmission.State == GameViewRecordingRuntimeAdmissionState.Ready
                            ? StartBinding
                            : null,
                        StartBinding.Runtime)),
                UnityRequestPayload.RecordingStop stop => CreateStopResponse(stop.Request),
                _ => throw new InvalidOperationException(
                    $"Unexpected request payload {payload.GetType().Name}."),
            };
            return ValueTask.FromResult(result);
        }

        private async ValueTask<UnityRequestExecutionResult> CreateCapabilityResponseAsync (
            CancellationToken cancellationToken)
        {
            await BeforeCapabilityResponseAsync!(cancellationToken);
            return Success(new IpcGameViewRecordingCapabilityResponse(
                Adapter,
                RuntimeAdmission,
                Limits,
                CaptureProfile,
                RuntimeAdmission.State == GameViewRecordingRuntimeAdmissionState.Ready
                    ? StartBinding
                    : null,
                StartBinding.Runtime));
        }

        private async ValueTask<UnityRequestExecutionResult> CreateStartResponseAsync (
            IpcGameViewRecordingStartRequest request,
            CancellationToken cancellationToken)
        {
            StartCount++;
            if (BeforeStartResponseAsync is not null)
            {
                await BeforeStartResponseAsync(cancellationToken);
            }

            if (LoseNextStartResponse)
            {
                LoseNextStartResponse = false;
                return UnityRequestExecutionResult.Failure(
                    new UnityRequestFailure(
                        UnityRequestFailureKind.TransportInterrupted,
                        ExecutionErrorCodes.IpcTimeout,
                        "The start response was lost after dispatch."));
            }

            var response = new IpcGameViewRecordingStartResponse(CreateActiveSnapshot(request));
            if (StartResponseError is not null)
            {
                return UnityRequestExecutionResult.Success(
                    new UnityRequestResponse(
                        IpcPayloadCodec.SerializeToElement(response),
                        [StartResponseError]));
            }

            var result = Success(response);
            CancelAfterStartResponse?.Cancel();
            return result;
        }

        private async ValueTask<UnityRequestExecutionResult> CreateStatusResponseAsync (
            IpcGameViewRecordingStatusRequest request,
            CancellationToken cancellationToken)
        {
            StatusRequests.Add(request);
            if (StatusFailure is not null)
            {
                return StatusFailure;
            }
            if (ReturnNoRecordingStatus)
            {
                return Success(new IpcGameViewRecordingStatusResponse(
                    new IpcNoGameViewRecordingSelection()));
            }

            var snapshot = StatusSnapshotFactory is not null
                ? await StatusSnapshotFactory(request, cancellationToken)
                : ReturnTerminalStatus
                    ? CreateTerminalSnapshot(request.KnownRecording as IpcGameViewRecordingActiveSnapshot
                        ?? throw new InvalidOperationException(
                            "A durable active runtime observation was expected."))
                    : request.KnownRecording
                        ?? throw new InvalidOperationException(
                            "A durable runtime observation was expected.");
            return Success(new IpcGameViewRecordingStatusResponse(
                new IpcSelectedGameViewRecordingSelection(snapshot)));
        }

        private UnityRequestExecutionResult CreateStopResponse (
            IpcGameViewRecordingStopRequest request)
        {
            StopCount++;
            throw new InvalidOperationException(
                $"Recording stop was not expected for {request.RecordingId:D}.");
        }

        private static UnityRequestExecutionResult Success<T> (T payload) =>
            UnityRequestExecutionResult.Success(
                new UnityRequestResponse(
                    IpcPayloadCodec.SerializeToElement(payload),
                    Errors: []));
    }

    private sealed class SuccessfulTerminalFinalizer : IGameViewRecordingTerminalFinalizer
    {
        private int finalizationCount;

        public Func<int, CancellationToken, ValueTask>? BeforeFinalizeAsync { get; set; }

        public int FinalizationCount => Volatile.Read(ref finalizationCount);

        public async ValueTask<GameViewRecordingTerminalFinalizationResult> FinalizeAsync (
            ProjectContext context,
            IGameViewRecordingArtifactLease artifactLease,
            GameViewRecordingStoredExecution stored,
            IpcGameViewRecordingTerminalSnapshot terminalSnapshot,
            Func<bool> canStartNextStage,
            CancellationToken cancellationToken = default)
        {
            var attempt = Interlocked.Increment(ref finalizationCount);
            if (BeforeFinalizeAsync is not null)
            {
                await BeforeFinalizeAsync(attempt, cancellationToken);
            }

            var completedAtUtc = terminalSnapshot.CompletedAtUtc;
            var terminalRef = new PathArtifactRef(
                GameViewRecordingArtifactKinds.TerminalRecord,
                GameViewRecordingArtifactMediaTypes.Json,
                new ArtifactPath($"recordings/{stored.RecordingId:N}/terminal.json"),
                Sha256Digest.Compute([9, 8, 7]),
                sizeBytes: 10,
                completedAtUtc);
            var progress = new GameViewRecordingTerminalProgress(
                terminalSnapshot.State,
                terminalSnapshot.EffectiveMaxDurationSeconds,
                terminalSnapshot.EncodedFrameCount,
                GetStartedAtUtc(terminalSnapshot),
                GetStopRequestedAtUtc(terminalSnapshot),
                terminalSnapshot.UpdatedAtUtc);
            var payload = new GameViewRecordingTerminalPayload(
                stored.Payload.Project,
                new TerminalExecutionRef(
                    GameViewRecordingExecutionContract.Kind,
                    stored.RecordingId,
                    stored.RequestDigest,
                    GameViewRecordingExecutionContract.ToExecutionState(terminalSnapshot.State),
                    statusLocator: null,
                    terminalRef),
                stored.RequestDigest,
                stored.RequestRef,
                progress,
                [stored.RequestRef, terminalRef],
                stored.Payload.Diagnostics,
                new GameViewRecordingTerminalSummary(
                    terminalSnapshot.State,
                    terminalSnapshot.StopReason,
                    GameViewRecordingVideoDisposition.Unconfirmed,
                    GameViewRecordingCleanupDisposition.Unconfirmed,
                    GetStartedAtUtc(terminalSnapshot),
                    completedAtUtc));
            return GameViewRecordingTerminalFinalizationResult.Success(payload);
        }

        private static DateTimeOffset? GetStartedAtUtc (
            IpcGameViewRecordingTerminalSnapshot snapshot) =>
            snapshot switch
            {
                IpcGameViewRecordingCompletedSnapshot completed => completed.StartedAtUtc,
                IpcGameViewRecordingFailedSnapshot failed => failed.StartedAtUtc,
                IpcGameViewRecordingIndeterminateSnapshot indeterminate => indeterminate.StartedAtUtc,
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
            };

        private static DateTimeOffset? GetStopRequestedAtUtc (
            IpcGameViewRecordingTerminalSnapshot snapshot) =>
            snapshot switch
            {
                IpcGameViewRecordingCompletedSnapshot completed => completed.StopRequestedAtUtc,
                IpcGameViewRecordingFailedSnapshot failed => failed.StopRequestedAtUtc,
                IpcGameViewRecordingIndeterminateSnapshot indeterminate => indeterminate.StopRequestedAtUtc,
                _ => throw new ArgumentOutOfRangeException(nameof(snapshot)),
            };
    }

    private static IpcGameViewRecordingActiveSnapshot CreatePreparingSnapshot (
        IpcGameViewRecordingStatusRequest request) =>
        new IpcGameViewRecordingActiveSnapshot(
            request.RecordingId,
            request.RequestDigest,
            GameViewRecordingState.Preparing,
            request.StartBinding.Runtime,
            target: null,
            request.EffectiveMaxDurationSeconds,
            encodedFrameCount: null,
            startedAtUtc: null,
            StartedAtUtc,
            request.StartBinding.Generation,
            request.StartBinding.Generation);

    private static IpcGameViewRecordingActiveSnapshot CreateActiveSnapshot (
        IpcGameViewRecordingStartRequest request)
    {
        var resolution = request.Request.Resolution;
        return new IpcGameViewRecordingActiveSnapshot(
            request.RecordingId,
            request.RequestDigest,
            GameViewRecordingState.Recording,
            request.StartBinding.Runtime,
            new GameViewRecordingTargetObservation(
                playModeViewId: "play-mode-view-1",
                gameViewId: "game-view-1",
                display: 0,
                resolution,
                resolution,
                orientation: "upright",
                projectColorSpace: UnityProjectColorSpace.Linear),
            request.Request.MaxDurationSeconds,
            encodedFrameCount: 1,
            StartedAtUtc,
            StartedAtUtc.AddSeconds(1),
            request.StartBinding.Generation,
            CreateGeneration(1));
    }

    private static IpcGameViewRecordingIndeterminateSnapshot CreateTerminalSnapshot (
        IpcGameViewRecordingActiveSnapshot known)
    {
        var completedAtUtc = known.UpdatedAtUtc.AddSeconds(2);
        return new IpcGameViewRecordingIndeterminateSnapshot(
            known.RecordingId,
            known.RequestDigest,
            GameViewRecordingState.Indeterminate,
            GameViewRecordingStopReason.UnityExited,
            failure: null,
            known.Runtime,
            cleanup: null,
            known.Target,
            timing: null,
            known.EffectiveMaxDurationSeconds,
            known.EncodedFrameCount,
            known.StartedAtUtc,
            stopRequestedAtUtc: null,
            completedAtUtc,
            completedAtUtc,
            known.StartGeneration,
            CreateGeneration(2));
    }

    private static UnityEditorGenerationSnapshot CreateGeneration (long generation) =>
        new(generation, generation, generation, generation);
}
