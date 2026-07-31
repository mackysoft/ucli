using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

namespace MackySoft.Ucli.Tests.Shared.Execution.Lifecycle;

public sealed class FileLifecycleExecutionHostExitTerminalizerTests
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 7, 31, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TerminalizeAsync_WhenPublicationSucceeds_ReturnsReverifiedTerminalContinuation ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-host-exit",
            "published");
        var context = CreateProjectContext(scope);
        var store = CreateStore(context);
        var start = await RegisterRefreshAsync(store, context);
        var observedFacts =
            LifecycleExecutionTerminalFactsPolicy.ResolveHostExit(
                start,
                start.LifecycleExecutionRef,
                lifecycleActionAdmitted: false,
                start.DeadlineUtc);
        var terminalRecord = CreateHostExitTerminalRecord(
            start,
            observedFacts);
        var terminalizer = new FileLifecycleExecutionHostExitTerminalizer();

        var result = await terminalizer.TerminalizeAsync(
            context,
            start,
            start.LifecycleExecutionRef,
            observedFacts,
            (authoritativeStart, resolvedFacts) =>
                CreateHostExitTerminalRecord(
                    authoritativeStart,
                    resolvedFacts));

        var published =
            Assert.IsType<LifecycleExecutionHostExitTerminalizationResult.Published>(
                result);
        Assert.Equal(terminalRecord, published.TerminalRecord);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Failed),
            published.ExecutionReference.State.Value);
        var reverified = await store.TryRecoverTerminalPublicationAsync(
            LifecycleExecutionKind.Refresh,
            start.LifecycleExecutionRef.Id,
            CancellationToken.None);
        Assert.True(reverified.IsSuccess);
        Assert.Equal(
            published.ExecutionReference,
            reverified.TerminalReference);
        Assert.Equal(published.TerminalRecord, reverified.TerminalRecord);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TerminalizeAsync_WhenConcurrentResendAcquiresSideEffectRightAfterRead_DoesNotPublishNotApplied ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-host-exit",
            "concurrent-side-effect-right");
        var context = CreateProjectContext(scope);
        var store = CreateStore(context);
        var start = await RegisterRefreshAsync(store, context);
        var callerReference = start.LifecycleExecutionRef;
        var refreshingReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                callerReference,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Refreshing);
        var observedFacts =
            LifecycleExecutionTerminalFactsPolicy.ResolveHostExit(
                start,
                callerReference,
                lifecycleActionAdmitted: false,
                StartedAtUtc.AddSeconds(1));
        var terminalizer = new FileLifecycleExecutionHostExitTerminalizer();
        var firstCandidateCreated =
            new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
        using var allowFirstPublication = new ManualResetEventSlim();
        var factoryInvocationCount = 0;

        var terminalizationTask = Task.Run(
            async () => await terminalizer.TerminalizeAsync(
                    context,
                    start,
                    callerReference,
                    observedFacts,
                    CreateSynchronizedTerminalRecord)
                .ConfigureAwait(false));
        await firstCandidateCreated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            var admission = await store.TryAcquireSideEffectRightAsync(
                callerReference,
                refreshingReference,
                start.Host.CurrentEndpointRegistrationGenerationId,
                CancellationToken.None);
            Assert.Equal(
                LifecycleExecutionSideEffectRightOutcome.Acquired,
                admission.Outcome);
        }
        finally
        {
            allowFirstPublication.Set();
        }

        var result = await terminalizationTask.WaitAsync(
            TimeSpan.FromSeconds(10));

        var published =
            Assert.IsType<LifecycleExecutionHostExitTerminalizationResult.Published>(
                result);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            published.TerminalRecord.ApplicationState);
        Assert.Equal(2, factoryInvocationCount);

        LifecycleExecutionTerminalRecord CreateSynchronizedTerminalRecord (
            LifecycleExecutionStartBinding authoritativeStart,
            LifecycleExecutionTerminalFacts resolvedFacts)
        {
            if (Interlocked.Increment(ref factoryInvocationCount) == 1)
            {
                firstCandidateCreated.SetResult(true);
                if (!allowFirstPublication.Wait(TimeSpan.FromSeconds(10)))
                {
                    throw new TimeoutException(
                        "The concurrent side-effect admission did not finish.");
                }
            }

            return CreateHostExitTerminalRecord(
                authoritativeStart,
                resolvedFacts);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TerminalizeAsync_WhenExistingIntentCannotBePublished_RetainsItsFixedApplicationState ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-host-exit",
            "fixed-intent-publication-failed");
        var context = CreateProjectContext(scope);
        var store = CreateStore(context);
        var start = await RegisterRefreshAsync(store, context);
        var refreshingReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                start.LifecycleExecutionRef,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Refreshing);
        var admission = await store.TryAcquireSideEffectRightAsync(
            start.LifecycleExecutionRef,
            refreshingReference,
            start.Host.CurrentEndpointRegistrationGenerationId,
            CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionSideEffectRightOutcome.Acquired,
            admission.Outcome);
        var admitted = (await store.ReadAsync(
            LifecycleExecutionKind.Refresh,
            start.LifecycleExecutionRef.Id,
            CancellationToken.None))!;
        var fixedFacts =
            LifecycleExecutionTerminalFactsPolicy.ResolveHostExit(
                admitted.Start,
                admitted.CurrentReference,
                lifecycleActionAdmitted: true,
                admitted.Start.DeadlineUtc);
        var selfReference = CreateSelfReferencingArtifact(store, start);
        var fixedRecord = CreateHostExitTerminalRecord(
            admitted.Start,
            fixedFacts,
            [selfReference]);
        var failedPublication = await store.PublishTerminalAsync(
            fixedRecord,
            CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.PublicationFailed,
            failedPublication.Outcome);
        var staleFacts =
            LifecycleExecutionTerminalFactsPolicy.ResolveHostExit(
                start,
                start.LifecycleExecutionRef,
                lifecycleActionAdmitted: false,
                StartedAtUtc.AddSeconds(1));
        var terminalizer = new FileLifecycleExecutionHostExitTerminalizer();

        var result = await terminalizer.TerminalizeAsync(
            context,
            start,
            start.LifecycleExecutionRef,
            staleFacts,
            (_, _) => throw new InvalidOperationException(
                "A fixed terminal publication intent must be recovered without replacing its record."));

        var failed =
            Assert.IsType<
                LifecycleExecutionHostExitTerminalizationResult.PublicationFailed>(
                result);
        Assert.Equal(
            ExecutionApplicationState.Indeterminate,
            failed.ApplicationState);
        var retainedRecord =
            Assert.IsType<RefreshLifecycleExecutionTerminalRecord>(
                failed.FixedTerminalRecord);
        Assert.Equal(fixedRecord.ExecutionId, retainedRecord.ExecutionId);
        Assert.Equal(fixedRecord.TerminalReason, retainedRecord.TerminalReason);
        Assert.Equal(fixedRecord.ApplicationState, retainedRecord.ApplicationState);
        Assert.Equal(fixedRecord.ArtifactRefs, retainedRecord.ArtifactRefs);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Publishing),
            failed.ExecutionReference.State.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TerminalizeAsync_WhenPublicationCannotBeReverified_RetainsLatestReconnectableReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-host-exit",
            "publication-failed");
        var context = CreateProjectContext(scope);
        var store = CreateStore(context);
        var start = await RegisterRefreshAsync(store, context);
        var selfReference = CreateSelfReferencingArtifact(store, start);
        var observedFacts =
            LifecycleExecutionTerminalFactsPolicy.ResolveHostExit(
                start,
                start.LifecycleExecutionRef,
                lifecycleActionAdmitted: false,
                start.DeadlineUtc);
        var terminalizer = new FileLifecycleExecutionHostExitTerminalizer();

        var result = await terminalizer.TerminalizeAsync(
            context,
            start,
            start.LifecycleExecutionRef,
            observedFacts,
            (authoritativeStart, resolvedFacts) =>
                CreateHostExitTerminalRecord(
                    authoritativeStart,
                    resolvedFacts,
                    [selfReference]));

        var failed =
            Assert.IsType<
                LifecycleExecutionHostExitTerminalizationResult.PublicationFailed>(
                result);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            failed.Failure.Code);
        Assert.IsAssignableFrom<IReconnectableExecutionRef>(
            failed.ExecutionReference);
        Assert.Equal(
            ExecutionLifecycle.Recovery,
            failed.ExecutionReference.Lifecycle);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Publishing),
            failed.ExecutionReference.State.Value);
        var fixedRecord =
            Assert.IsType<RefreshLifecycleExecutionTerminalRecord>(
                failed.FixedTerminalRecord);
        Assert.Equal(selfReference, Assert.Single(fixedRecord.ArtifactRefs));
        var stored = await store.ReadAsync(
            LifecycleExecutionKind.Refresh,
            start.LifecycleExecutionRef.Id,
            CancellationToken.None);
        Assert.Equal(failed.ExecutionReference, stored!.CurrentReference);
        Assert.Null(stored.TerminalReference);
    }

    private static PathArtifactRef CreateSelfReferencingArtifact (
        FileLifecycleExecutionStore store,
        LifecycleExecutionStartBinding start)
    {
        return new PathArtifactRef(
            LifecycleExecutionArtifactContract.TerminalRecordKind,
            LifecycleExecutionArtifactContract.TerminalRecordMediaType,
            store.Paths.CreateTerminalRecordArtifactPath(
                LifecycleExecutionKind.Refresh,
                start.LifecycleExecutionRef.Id),
            Sha256Digest.Compute(ReadOnlySpan<byte>.Empty),
            sizeBytes: 0,
            StartedAtUtc);
    }

    private static FileLifecycleExecutionStore CreateStore (
        ResolvedUnityProjectContext context)
    {
        return FileLifecycleExecutionStore.CreateForProject(
            context.UnityProjectRoot,
            context.ProjectFingerprint);
    }

    private static async ValueTask<LifecycleExecutionStartBinding>
        RegisterRefreshAsync (
            FileLifecycleExecutionStore store,
            ResolvedUnityProjectContext context)
    {
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var started = await store.StartAsync(
            definition,
            Guid.NewGuid(),
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new UnityProjectIdentity(
                context.UnityProjectRoot.Value,
                context.ProjectFingerprint,
                context.UnityVersion),
            CreateHost(),
            new UnityEditorGenerationSnapshot(10, 20, 30, 40),
            StartedAtUtc.AddMinutes(5),
            StartedAtUtc,
            CancellationToken.None);
        return started.Binding!;
    }

    private static LifecycleExecutionHostRegistration CreateHost ()
    {
        var endpointGeneration =
            Guid.Parse("11111111-2222-3333-4444-555555555555");
        return new LifecycleExecutionHostRegistration(
            new ProcessIdentity(42, 123456),
            Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            endpointGeneration,
            endpointGeneration);
    }

    private static RefreshLifecycleExecutionTerminalRecord
        CreateHostExitTerminalRecord (
            LifecycleExecutionStartBinding start,
            LifecycleExecutionTerminalFacts terminalFacts,
            IReadOnlyList<ArtifactRef>? artifactRefs = null)
    {
        return new RefreshLifecycleExecutionTerminalRecord(
            start.LifecycleExecutionRef.Id,
            start.LifecycleExecutionRef.DefinitionDigest,
            start.Project,
            start.Host,
            start.StartedGeneration,
            terminalGeneration: null,
            start.DeadlineUtc,
            start.StartedAtUtc,
            terminalFacts.CompletedAtUtc,
            terminalFacts.TerminalReason,
            terminalFacts.ApplicationState,
            result: null,
            verdict: null,
            artifactRefs ?? Array.Empty<ArtifactRef>());
    }

    private static ResolvedUnityProjectContext CreateProjectContext (
        TestDirectoryScope scope)
    {
        var root = AbsolutePath.Parse(scope.FullPath);
        return ResolvedUnityProjectContext.Create(
            root,
            root,
            new ProjectFingerprint(new string('a', 64)),
            UnityProjectPathSource.CommandOption,
            scope.FullPath,
            "6000.1.4f1");
    }
}
