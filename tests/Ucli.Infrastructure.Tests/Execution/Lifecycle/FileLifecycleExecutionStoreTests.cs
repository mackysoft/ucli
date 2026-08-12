using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;

namespace MackySoft.Ucli.Infrastructure.Tests.Execution.Lifecycle;

public sealed class FileLifecycleExecutionStoreTests
{
    private const int MaximumStoredRecordBytes = 4 * 1024 * 1024;

    private static readonly ProjectFingerprint ProjectFingerprint = new(
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    private static readonly DateTimeOffset StartedAtUtc = new(2026, 7, 31, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DeadlineUtc = StartedAtUtc.AddMinutes(5);
    private static readonly UnityEditorGenerationSnapshot StartedGeneration = new(10, 20, 30, 40);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task StartAsync_WhenExecutionIsNew_PersistsRegisteredBinding ()
    {
        using var scope = TestDirectories.CreateTempScope("lifecycle-execution-store", "register");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();

        var result = await store.StartAsync(
            definition,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            CreateProject(),
            CreateHost(),
            StartedGeneration,
            DeadlineUtc,
            StartedAtUtc,
            CancellationToken.None);

        Assert.Equal(LifecycleExecutionStartOutcome.Registered, result.Outcome);
        Assert.NotNull(result.Binding);
        Assert.Equal(executionId, result.Binding.LifecycleExecutionRef.Id);
        Assert.Equal("registered", result.Binding.LifecycleExecutionRef.State.Value);
        var stored = await store.ReadAsync(definition.Kind, executionId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(result.Binding, stored.Start);
        Assert.False(stored.IsTerminal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("windows")]
    public async Task StartAndListEntries_OnWindowsWithLongStorageRoot_PreservesRecoverableExecution ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "long-storage-root");
        var storageRoot = AbsolutePath.Parse(scope.GetPath(Path.Combine(
            new string('a', 80),
            new string('b', 80),
            new string('c', 80))));
        var store = new FileLifecycleExecutionStore(storageRoot, ProjectFingerprint);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        Assert.True(store.Paths.ResolveKindDirectory(definition.Kind).Value.Length >= 260);

        await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());

        var entry = Assert.Single(store.ListEntries(CancellationToken.None));
        Assert.Equal(definition.Kind, entry.Kind);
        Assert.Equal(executionId, entry.ExecutionId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task StartAsync_WhenNewHostAlreadyHasAdvancedEndpointGeneration_RejectsRegistration ()
    {
        using var scope = TestDirectories.CreateTempScope("lifecycle-execution-store", "advanced-start");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var host = CreateHost(
            currentEndpointRegistrationGenerationId:
                Guid.Parse("10000000-0000-0000-0000-000000000002"));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            StartAsync(store, definition, executionId, CreateProject(), host).AsTask());

        Assert.Equal("host", exception.ParamName);
        Assert.Null(await store.ReadAsync(definition.Kind, executionId, CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task StartAsync_WhenSameIdentityIsRetried_ReconnectsToEstablishedBinding ()
    {
        using var scope = TestDirectories.CreateTempScope("lifecycle-execution-store", "reconnect");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Compile);
        var executionId = Guid.NewGuid();
        var host = CreateHost();
        var registered = await StartAsync(store, definition, executionId, CreateProject(), host);

        var reconnected = await StartAsync(store, definition, executionId, CreateProject(), host);

        Assert.Equal(LifecycleExecutionStartOutcome.Reconnected, reconnected.Outcome);
        Assert.Equal(registered.Binding, reconnected.Binding);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task StartAsync_WhenSameIdentifierHasDifferentDefinitionDigest_RejectsReuse ()
    {
        using var scope = TestDirectories.CreateTempScope("lifecycle-execution-store", "definition-conflict");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Compile);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(store, definition, executionId, CreateProject(), CreateHost());

        var conflict = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost(),
            Sha256Digest.Parse(new string('f', 64)));

        Assert.Equal(LifecycleExecutionStartOutcome.DefinitionConflict, conflict.Outcome);
        Assert.Null(conflict.Binding);
        Assert.Equal(
            registered.Binding,
            (await store.ReadAsync(definition.Kind, executionId, CancellationToken.None))!.Start);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task StartAsync_WhenProjectOrHostDiffers_RejectsReuseWithoutReplacingEstablishedBinding ()
    {
        using var scope = TestDirectories.CreateTempScope("lifecycle-execution-store", "identity-mismatch");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.PlayEnter);
        var executionId = Guid.NewGuid();
        var project = CreateProject();
        var host = CreateHost();
        var registered = await StartAsync(store, definition, executionId, project, host);

        var projectMismatch = await StartAsync(
            store,
            definition,
            executionId,
            new UnityProjectIdentity("/different-project", ProjectFingerprint, "6000.1.5f1"),
            host);
        var hostMismatch = await StartAsync(
            store,
            definition,
            executionId,
            project,
            CreateHost(editorInstanceId: Guid.NewGuid()));

        Assert.Equal(LifecycleExecutionStartOutcome.ProjectMismatch, projectMismatch.Outcome);
        Assert.Null(projectMismatch.Binding);
        Assert.Equal(LifecycleExecutionStartOutcome.HostMismatch, hostMismatch.Outcome);
        Assert.Null(hostMismatch.Binding);
        Assert.Equal(
            registered.Binding,
            (await store.ReadAsync(definition.Kind, executionId, CancellationToken.None))!.Start);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryUpdateReferenceAsync_OnlyAcceptsTheEstablishedProjection ()
    {
        using var scope = TestDirectories.CreateTempScope("lifecycle-execution-store", "state-cas");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.PlayExit);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(store, definition, executionId, CreateProject(), CreateHost());
        var exiting = LifecycleExecutionReferenceFactory.CreateStateProjection(
            registered.Binding!.LifecycleExecutionRef,
            ExecutionLifecycle.Active,
            LifecycleExecutionState.Exiting);
        Assert.Equal(
            LifecycleExecutionSideEffectRightOutcome.Acquired,
            (await store.TryAcquireSideEffectRightAsync(
                registered.Binding.LifecycleExecutionRef,
                exiting,
                registered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                CancellationToken.None)).Outcome);
        var recovering = LifecycleExecutionReferenceFactory.CreateStateProjection(
            exiting,
            ExecutionLifecycle.Recovery,
            LifecycleExecutionState.Recovering);

        var updated = await store.TryUpdateReferenceAsync(
            exiting,
            recovering,
            CancellationToken.None);
        var staleUpdate = await store.TryUpdateReferenceAsync(
            exiting,
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                exiting,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Exiting),
            CancellationToken.None);

        Assert.Equal(LifecycleExecutionReferenceUpdateOutcome.Updated, updated);
        Assert.Equal(LifecycleExecutionReferenceUpdateOutcome.Conflict, staleUpdate);
        Assert.Equal(
            recovering,
            (await store.ReadAsync(definition.Kind, executionId, CancellationToken.None))!.CurrentReference);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryUpdateReferenceAsync_WhenRegistered_RejectsWithoutClaimingSideEffectRight ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "registered-update-is-admission-owned");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.PlayEnter);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var binding = Assert.IsType<LifecycleExecutionStartBinding>(
            registered.Binding);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            store.TryUpdateReferenceAsync(
                    binding.LifecycleExecutionRef,
                    LifecycleExecutionReferenceFactory.CreateStateProjection(
                        binding.LifecycleExecutionRef,
                        ExecutionLifecycle.Active,
                        LifecycleExecutionState.Entering),
                    CancellationToken.None)
                .AsTask());

        Assert.Equal("expectedReference", exception.ParamName);
        var authoritative = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        Assert.Equal(
            binding.LifecycleExecutionRef,
            authoritative!.CurrentReference);
        Assert.Null(
            authoritative
                .SideEffectRightOwnerEndpointRegistrationGenerationId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryUpdateReferenceAsync_WhenPublishingIsRequested_RejectsWithoutChangingReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "publishing-is-publication-owned");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.PlayExit);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var exiting = LifecycleExecutionReferenceFactory.CreateStateProjection(
            registered.Binding!.LifecycleExecutionRef,
            ExecutionLifecycle.Active,
            LifecycleExecutionState.Exiting);
        Assert.Equal(
            LifecycleExecutionSideEffectRightOutcome.Acquired,
            (await store.TryAcquireSideEffectRightAsync(
                registered.Binding.LifecycleExecutionRef,
                exiting,
                registered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                CancellationToken.None)).Outcome);
        var publishing = LifecycleExecutionReferenceFactory.CreateStateProjection(
            exiting,
            ExecutionLifecycle.Recovery,
            LifecycleExecutionState.Publishing);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            store.TryUpdateReferenceAsync(
                    exiting,
                    publishing,
                    CancellationToken.None)
                .AsTask());

        Assert.Equal("nextReference", exception.ParamName);
        Assert.Equal(
            exiting,
            (await store.ReadAsync(
                definition.Kind,
                executionId,
                CancellationToken.None))!.CurrentReference);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryEnterRecoveryAsync_WhenRegistrationHasNoSideEffectRight_RejectsRecoveryWithoutChangingReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "recovery-before-side-effect-admission");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.PlayEnter);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());

        var outcome = await store.TryEnterRecoveryAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionRecoveryTransitionOutcome
                .SideEffectAdmissionRequired,
            outcome);
        Assert.Equal(
            registered.Binding!.LifecycleExecutionRef,
            (await store.ReadAsync(
                definition.Kind,
                executionId,
                CancellationToken.None))!.CurrentReference);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryEnterRecoveryAsync_WhenSameAdmittedExecutionRaces_ConvergesOnOneRecoveringProjection ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "recovery-transition-race");
        var firstStore = CreateStore(scope);
        var secondStore = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.PlayExit);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            firstStore,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var exitingReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                registered.Binding!.LifecycleExecutionRef,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Exiting);
        Assert.Equal(
            LifecycleExecutionSideEffectRightOutcome.Acquired,
            (await firstStore.TryAcquireSideEffectRightAsync(
                registered.Binding.LifecycleExecutionRef,
                exitingReference,
                registered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                CancellationToken.None)).Outcome);

        var outcomes = await Task.WhenAll(
            firstStore.TryEnterRecoveryAsync(
                    definition.Kind,
                    executionId,
                    CancellationToken.None)
                .AsTask(),
            secondStore.TryEnterRecoveryAsync(
                    definition.Kind,
                    executionId,
                    CancellationToken.None)
                .AsTask());

        Assert.Single(
            outcomes,
            outcome =>
                outcome
                    == LifecycleExecutionRecoveryTransitionOutcome.Entered);
        Assert.Single(
            outcomes,
            outcome =>
                outcome
                    == LifecycleExecutionRecoveryTransitionOutcome
                        .AlreadyRecovering);
        var stored = await firstStore.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        Assert.Equal(
            ExecutionLifecycle.Recovery,
            stored!.CurrentReference.Lifecycle);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Recovering),
            stored.CurrentReference.State.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryAcquireSideEffectRightAsync_WhenSameRegistrationRaces_GrantsExactlyOneRight ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "side-effect-right-race");
        var firstStore = CreateStore(scope);
        var secondStore = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.PlayEnter);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            firstStore,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var expectedReference = registered.Binding!.LifecycleExecutionRef;
        var enteringReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                expectedReference,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Entering);

        var results = await Task.WhenAll(
            firstStore.TryAcquireSideEffectRightAsync(
                    expectedReference,
                    enteringReference,
                    registered.Binding.Host
                        .CurrentEndpointRegistrationGenerationId,
                    CancellationToken.None)
                .AsTask(),
            secondStore.TryAcquireSideEffectRightAsync(
                    expectedReference,
                    enteringReference,
                    registered.Binding.Host
                        .CurrentEndpointRegistrationGenerationId,
                    CancellationToken.None)
                .AsTask());

        Assert.Single(
            results,
            result =>
                result.Outcome
                    == LifecycleExecutionSideEffectRightOutcome.Acquired);
        Assert.Single(
            results,
            result =>
                result.Outcome
                    == LifecycleExecutionSideEffectRightOutcome.Contended);
        Assert.All(
            results,
            result => Assert.Equal(
                enteringReference,
                result.AuthoritativeExecution!.CurrentReference));
        Assert.Equal(
            enteringReference,
            (await firstStore.ReadAsync(
                definition.Kind,
                executionId,
                CancellationToken.None))!.CurrentReference);
        Assert.All(
            results,
            result => Assert.Equal(
                registered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                result.AuthoritativeExecution!
                    .SideEffectRightOwnerEndpointRegistrationGenerationId));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryAcquireSideEffectRightAsync_WhenOldEndpointClaimsAfterAdvance_DoesNotBorrowSuccessorIdentity ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "side-effect-right-stale-claimant");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.PlayEnter);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var oldEndpoint = registered.Binding!.Host
            .CurrentEndpointRegistrationGenerationId;
        var successorEndpoint = Guid.NewGuid();
        var nowUtc = StartedAtUtc.AddMinutes(1);
        Assert.Equal(
            LifecycleExecutionEndpointAdvanceOutcome.Advanced,
            await store.TryAdvanceEndpointRegistrationAsync(
                definition.Kind,
                executionId,
                registered.Binding.Project,
                registered.Binding.Host.Process,
                registered.Binding.Host.EditorInstanceId,
                successorEndpoint,
                new DaemonLifecycleRecoveryLease(
                    oldEndpoint,
                    nowUtc.AddMinutes(1)),
                nowUtc,
                CancellationToken.None));
        var enteringReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                registered.Binding.LifecycleExecutionRef,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Entering);

        var stale = await store.TryAcquireSideEffectRightAsync(
            registered.Binding.LifecycleExecutionRef,
            enteringReference,
            oldEndpoint,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionSideEffectRightOutcome.Contended,
            stale.Outcome);
        Assert.Equal(
            successorEndpoint,
            stale.AuthoritativeExecution!.Start.Host
                .CurrentEndpointRegistrationGenerationId);
        Assert.Null(
            stale.AuthoritativeExecution
                .SideEffectRightOwnerEndpointRegistrationGenerationId);
        var successor = await store.TryAcquireSideEffectRightAsync(
            registered.Binding.LifecycleExecutionRef,
            enteringReference,
            successorEndpoint,
            CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionSideEffectRightOutcome.Acquired,
            successor.Outcome);
        Assert.Equal(
            successorEndpoint,
            successor.AuthoritativeExecution!
                .SideEffectRightOwnerEndpointRegistrationGenerationId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Medium")]
    public async Task TryAcquireSideEffectRightAsync_WhenExecutionIsTerminalOrPublishing_ReturnsAuthoritativeExecution (
        bool terminal)
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            terminal
                ? "side-effect-right-terminal"
                : "side-effect-right-publishing");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var expectedReference = registered.Binding!.LifecycleExecutionRef;
        if (terminal)
        {
            _ = await store.PublishTerminalAsync(
                CreateDeadlineTerminalRecord(registered.Binding),
                CancellationToken.None);
        }
        else
        {
            await PersistPublishingStateAsync(
                store,
                definition.Kind,
                registered.Binding);
        }

        var result = await store.TryAcquireSideEffectRightAsync(
            expectedReference,
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                expectedReference,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Refreshing),
            registered.Binding.Host
                .CurrentEndpointRegistrationGenerationId,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionSideEffectRightOutcome.TerminalOrPublishing,
            result.Outcome);
        Assert.Equal(terminal, result.AuthoritativeExecution!.IsTerminal);
        Assert.Equal(!terminal, result.AuthoritativeExecution.IsPublishing);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SideEffectAdmissionCoordinator_WhenSameIdentifierContends_WaitsForWinningMarkerAndReturnsRecovery ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "side-effect-admission-contention");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.PlayEnter);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var expectedExecution = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var enteringReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                registered.Binding!.LifecycleExecutionRef,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Entering);
        var checkpointStore =
            new ControlledSideEffectAdmissionCheckpointStore(
                delayMarkerPersistence: true);
        var firstCoordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);
        var secondCoordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);

        var ownerTask = firstCoordinator.AcquireAsync(
                definition.Kind,
                expectedExecution!,
                enteringReference,
                registered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                checkpointStore,
                checkpointStore.InitialCheckpoint,
                CancellationToken.None)
            .AsTask();
        await checkpointStore.MarkerPersistenceStarted.WaitAsync(
            TimeSpan.FromSeconds(5));
        var resendTask = secondCoordinator.AcquireAsync(
                definition.Kind,
                expectedExecution!,
                enteringReference,
                registered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                checkpointStore,
                checkpointStore.InitialCheckpoint,
                CancellationToken.None)
            .AsTask();

        Assert.False(resendTask.IsCompleted);
        Assert.Equal(1, checkpointStore.MarkCount);
        checkpointStore.ReleaseMarkerPersistence();
        var owner = await ownerTask;
        var resend = await resendTask;

        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Acquired,
            owner.State);
        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Recover,
            resend.State);
        Assert.True(owner.Checkpoint.Admitted);
        Assert.True(resend.Checkpoint.Admitted);
        Assert.Equal(1, checkpointStore.MarkCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SideEffectAdmissionCoordinator_WhenSameIdentifierBelongsToDifferentProjects_DoesNotSerializeAdmissions ()
    {
        using var firstScope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "side-effect-admission-first-project");
        using var secondScope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "side-effect-admission-second-project");
        var secondProjectFingerprint = new ProjectFingerprint(
            "1123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
        var firstStore = CreateStore(firstScope);
        var secondStore = new FileLifecycleExecutionStore(
            AbsolutePath.Parse(secondScope.FullPath),
            secondProjectFingerprint);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var firstRegistered = await StartAsync(
            firstStore,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var secondRegistered = await StartAsync(
            secondStore,
            definition,
            executionId,
            new UnityProjectIdentity(
                "/workspace/SecondUnityProject",
                secondProjectFingerprint,
                "6000.1.4f1"),
            CreateHost());
        var firstExpected = await firstStore.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var secondExpected = await secondStore.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var firstCheckpointStore =
            new ControlledSideEffectAdmissionCheckpointStore(
                delayMarkerPersistence: true);
        var secondCheckpointStore =
            new ControlledSideEffectAdmissionCheckpointStore(
                delayMarkerPersistence: false);
        var firstCoordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(firstStore);
        var secondCoordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(secondStore);

        var firstAdmission = firstCoordinator.AcquireAsync(
                definition.Kind,
                firstExpected!,
                LifecycleExecutionReferenceFactory.CreateStateProjection(
                    firstRegistered.Binding!.LifecycleExecutionRef,
                    ExecutionLifecycle.Active,
                    LifecycleExecutionState.Refreshing),
                firstRegistered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                firstCheckpointStore,
                firstCheckpointStore.InitialCheckpoint,
                CancellationToken.None)
            .AsTask();
        await firstCheckpointStore.MarkerPersistenceStarted.WaitAsync(
            TimeSpan.FromSeconds(5));

        LifecycleExecutionSideEffectAdmissionCoordinator.Resolution<
            SideEffectAdmissionCheckpoint> secondAdmission;
        try
        {
            secondAdmission = await secondCoordinator.AcquireAsync(
                    definition.Kind,
                    secondExpected!,
                    LifecycleExecutionReferenceFactory
                        .CreateStateProjection(
                            secondRegistered.Binding!
                                .LifecycleExecutionRef,
                            ExecutionLifecycle.Active,
                            LifecycleExecutionState.Refreshing),
                    secondRegistered.Binding.Host
                        .CurrentEndpointRegistrationGenerationId,
                    secondCheckpointStore,
                    secondCheckpointStore.InitialCheckpoint,
                    CancellationToken.None)
                .AsTask()
                .WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            firstCheckpointStore.ReleaseMarkerPersistence();
        }

        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Acquired,
            secondAdmission.State);
        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Acquired,
            (await firstAdmission).State);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AcquireAsync_WhenTerminalWinsDuringMarkerPersistence_DoesNotReturnIssueRightAndFastPathReadsTerminal ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "side-effect-marker-terminal-race");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var expectedExecution = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var refreshingReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                registered.Binding!.LifecycleExecutionRef,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Refreshing);
        var checkpointStore =
            new ControlledSideEffectAdmissionCheckpointStore(
                delayMarkerPersistence: true);
        var coordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);
        var claimant = registered.Binding.Host
            .CurrentEndpointRegistrationGenerationId;

        var acquisitionTask = coordinator.AcquireAsync(
                definition.Kind,
                expectedExecution!,
                refreshingReference,
                claimant,
                checkpointStore,
                checkpointStore.InitialCheckpoint,
                CancellationToken.None)
            .AsTask();
        await checkpointStore.MarkerPersistenceStarted.WaitAsync(
            TimeSpan.FromSeconds(5));
        Assert.True(
            (await store.PublishTerminalAsync(
                CreateDeadlineTerminalRecord(
                    registered.Binding,
                    ExecutionApplicationState.Indeterminate),
                CancellationToken.None)).IsSuccess);
        checkpointStore.ReleaseMarkerPersistence();

        var acquisition = await acquisitionTask;

        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Terminal,
            acquisition.State);
        Assert.True(acquisition.AuthoritativeExecution.IsTerminal);
        var fastPath = await coordinator.AcquireAsync(
            definition.Kind,
            expectedExecution!,
            refreshingReference,
            claimant,
            checkpointStore,
            acquisition.Checkpoint,
            CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Terminal,
            fastPath.State);
        Assert.True(fastPath.AuthoritativeExecution.IsTerminal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AcquireAsync_WhenEndpointAdvancesDuringMarkerPersistence_DoesNotReturnOldIssueRight ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "side-effect-marker-endpoint-race");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.PlayExit);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var expectedExecution = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var exitingReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                registered.Binding!.LifecycleExecutionRef,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Exiting);
        var checkpointStore =
            new ControlledSideEffectAdmissionCheckpointStore(
                delayMarkerPersistence: true);
        var coordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);
        var oldEndpoint = registered.Binding.Host
            .CurrentEndpointRegistrationGenerationId;
        var successorEndpoint = Guid.NewGuid();

        var acquisitionTask = coordinator.AcquireAsync(
                definition.Kind,
                expectedExecution!,
                exitingReference,
                oldEndpoint,
                checkpointStore,
                checkpointStore.InitialCheckpoint,
                CancellationToken.None)
            .AsTask();
        await checkpointStore.MarkerPersistenceStarted.WaitAsync(
            TimeSpan.FromSeconds(5));
        var nowUtc = StartedAtUtc.AddMinutes(1);
        Assert.Equal(
            LifecycleExecutionEndpointAdvanceOutcome.Advanced,
            await store.TryAdvanceEndpointRegistrationAsync(
                definition.Kind,
                executionId,
                registered.Binding.Project,
                registered.Binding.Host.Process,
                registered.Binding.Host.EditorInstanceId,
                successorEndpoint,
                new DaemonLifecycleRecoveryLease(
                    oldEndpoint,
                    nowUtc.AddMinutes(1)),
                nowUtc,
                CancellationToken.None));
        checkpointStore.ReleaseMarkerPersistence();

        var acquisition = await acquisitionTask;

        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Recover,
            acquisition.State);
        Assert.Equal(
            successorEndpoint,
            acquisition.AuthoritativeExecution.Start.Host
                .CurrentEndpointRegistrationGenerationId);
        Assert.Equal(
            oldEndpoint,
            acquisition.AuthoritativeExecution
                .SideEffectRightOwnerEndpointRegistrationGenerationId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReconnectAsync_WhenAdmissionMarkerAttemptIsInProgress_StopsOnSuppliedCancellationWithoutReportingRecovery ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "side-effect-admission-cancellation");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var refreshingReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                registered.Binding!.LifecycleExecutionRef,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Refreshing);
        var expectedExecution = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var checkpointStore =
            new ControlledSideEffectAdmissionCheckpointStore(
                delayMarkerPersistence: true);
        var ownerCoordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);
        var reconnectCoordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);

        var ownerTask = ownerCoordinator.AcquireAsync(
                definition.Kind,
                expectedExecution!,
                refreshingReference,
                registered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                checkpointStore,
                checkpointStore.InitialCheckpoint,
                CancellationToken.None)
            .AsTask();
        await checkpointStore.MarkerPersistenceStarted.WaitAsync(
            TimeSpan.FromSeconds(5));
        var authoritativeExecution = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        using var cancellationSource = new CancellationTokenSource();

        var reconnectTask = reconnectCoordinator.ReconnectAsync(
                definition.Kind,
                authoritativeExecution!,
                registered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                checkpointStore,
                checkpointStore.InitialCheckpoint,
                cancellationSource.Token)
            .AsTask();
        cancellationSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => reconnectTask);
        Assert.False(ownerTask.IsCompleted);
        Assert.False(
            checkpointStore.IsAdmitted(
                checkpointStore.InitialCheckpoint));
        Assert.Equal(
            refreshingReference,
            (await store.ReadAsync(
                definition.Kind,
                executionId,
                CancellationToken.None))!.CurrentReference);

        checkpointStore.ReleaseMarkerPersistence();
        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Acquired,
            (await ownerTask).State);
        Assert.Equal(1, checkpointStore.MarkCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AcquireAsync_WhenOwnerMarkerAttemptFails_ParallelSameClaimantResendRetriesAfterAttemptCompletes ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "side-effect-admission-same-claimant-marker-retry");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var expectedExecution = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var refreshingReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                registered.Binding!.LifecycleExecutionRef,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Refreshing);
        var checkpointStore =
            new ControlledSideEffectAdmissionCheckpointStore(
                delayMarkerPersistence: true,
                failMarkerPersistence: true);
        var firstCoordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);
        var resendCoordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);

        var firstAttempt = firstCoordinator.AcquireAsync(
                definition.Kind,
                expectedExecution!,
                refreshingReference,
                registered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                checkpointStore,
                checkpointStore.InitialCheckpoint,
                CancellationToken.None)
            .AsTask();
        await checkpointStore.MarkerPersistenceStarted.WaitAsync(
            TimeSpan.FromSeconds(5));
        using var resendCancellation = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));
        var resend = resendCoordinator.AcquireAsync(
                definition.Kind,
                expectedExecution!,
                refreshingReference,
                registered.Binding.Host
                    .CurrentEndpointRegistrationGenerationId,
                checkpointStore,
                checkpointStore.InitialCheckpoint,
                resendCancellation.Token)
            .AsTask();

        Assert.Equal(1, checkpointStore.MarkCount);
        Assert.False(resend.IsCompleted);
        checkpointStore.ReleaseMarkerPersistence();
        await Assert.ThrowsAsync<IOException>(() => firstAttempt);

        var recoveredAdmission = await resend;

        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Acquired,
            recoveredAdmission.State);
        Assert.True(recoveredAdmission.Checkpoint.Admitted);
        Assert.Equal(2, checkpointStore.MarkCount);
        Assert.Equal(
            registered.Binding.Host
                .CurrentEndpointRegistrationGenerationId,
            recoveredAdmission.AuthoritativeExecution
                .SideEffectRightOwnerEndpointRegistrationGenerationId);
        Assert.Equal(
            refreshingReference,
            recoveredAdmission.AuthoritativeExecution.CurrentReference);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task AcquireAsync_WhenMarkerPersistenceFails_StaleClaimantWaitDoesNotBlockProvenSuccessorTakeover ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "side-effect-admission-marker-failure");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var expectedExecution = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var refreshingReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                registered.Binding!.LifecycleExecutionRef,
                ExecutionLifecycle.Active,
                LifecycleExecutionState.Refreshing);
        var checkpointStore =
            new ControlledSideEffectAdmissionCheckpointStore(
                delayMarkerPersistence: false,
                failMarkerPersistence: true);
        var coordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);

        await Assert.ThrowsAsync<IOException>(() =>
            coordinator.AcquireAsync(
                    definition.Kind,
                    expectedExecution!,
                    refreshingReference,
                    registered.Binding.Host
                        .CurrentEndpointRegistrationGenerationId,
                    checkpointStore,
                    checkpointStore.InitialCheckpoint,
                    CancellationToken.None)
                .AsTask());
        var advanced = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        Assert.Equal(refreshingReference, advanced!.CurrentReference);
        var firstOwner =
            advanced.Start.Host.CurrentEndpointRegistrationGenerationId;
        Assert.Equal(
            firstOwner,
            advanced
                .SideEffectRightOwnerEndpointRegistrationGenerationId);
        Assert.False(
            checkpointStore.IsAdmitted(
                checkpointStore.InitialCheckpoint));

        var successor = Guid.NewGuid();
        var advancedEndpoint =
            await store.TryAdvanceEndpointRegistrationAsync(
                definition.Kind,
                executionId,
                advanced.Start.Project,
                advanced.Start.Host.Process,
                advanced.Start.Host.EditorInstanceId,
                successor,
                new DaemonLifecycleRecoveryLease(
                    firstOwner,
                    DeadlineUtc),
                StartedAtUtc.AddMinutes(1),
                CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionEndpointAdvanceOutcome.Advanced,
            advancedEndpoint);
        var successorExecution = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var staleTakeover = await store.TryTakeOverSideEffectRightAsync(
            successorExecution!,
            firstOwner,
            CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionSideEffectRightOutcome.Contended,
            staleTakeover.Outcome);
        Assert.Equal(
            firstOwner,
            staleTakeover.AuthoritativeExecution!
                .SideEffectRightOwnerEndpointRegistrationGenerationId);
        var staleCoordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);
        var successorCoordinator =
            new LifecycleExecutionSideEffectAdmissionCoordinator(store);
        using var staleCancellation = new CancellationTokenSource();
        var staleReconnect = staleCoordinator.ReconnectAsync(
                definition.Kind,
                successorExecution!,
                firstOwner,
                checkpointStore,
                checkpointStore.InitialCheckpoint,
                staleCancellation.Token)
            .AsTask();
        await checkpointStore.UnadmittedReadObserved.WaitAsync(
            TimeSpan.FromSeconds(5));

        using var successorCancellation = new CancellationTokenSource(
            TimeSpan.FromSeconds(5));
        LifecycleExecutionSideEffectAdmissionCoordinator.Resolution<
            SideEffectAdmissionCheckpoint> takeover;
        try
        {
            takeover = await successorCoordinator.ReconnectAsync(
                definition.Kind,
                successorExecution!,
                successor,
                checkpointStore,
                checkpointStore.InitialCheckpoint,
                successorCancellation.Token);
        }
        catch
        {
            staleCancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => staleReconnect);
            throw;
        }
        var staleResolution = await staleReconnect.WaitAsync(
            TimeSpan.FromSeconds(5));

        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Acquired,
            takeover.State);
        Assert.True(takeover.Checkpoint.Admitted);
        Assert.Equal(
            successor,
            takeover.AuthoritativeExecution
                .SideEffectRightOwnerEndpointRegistrationGenerationId);
        Assert.Equal(
            refreshingReference,
            takeover.AuthoritativeExecution.CurrentReference);
        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Recover,
            staleResolution.State);
        Assert.Equal(
            successor,
            staleResolution.AuthoritativeExecution
                .SideEffectRightOwnerEndpointRegistrationGenerationId);

        var publication = await store.PublishTerminalAsync(
            CreateDeadlineTerminalRecord(
                takeover.AuthoritativeExecution.Start,
                ExecutionApplicationState.Indeterminate),
            CancellationToken.None);
        Assert.True(publication.IsSuccess);
        var terminal = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var reconnected = await coordinator.ReconnectAsync(
            definition.Kind,
            terminal!,
            successor,
            checkpointStore,
            checkpointStore.InitialCheckpoint,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionSideEffectAdmissionCoordinator.Outcome.Terminal,
            reconnected.State);
        Assert.Equal(2, checkpointStore.MarkCount);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryAdvanceEndpointRegistrationAsync_RequiresCurrentUnexpiredLeaseAndConsumesItsGeneration ()
    {
        using var scope = TestDirectories.CreateTempScope("lifecycle-execution-store", "endpoint-cas");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var project = CreateProject();
        var host = CreateHost();
        await StartAsync(store, definition, executionId, project, host);
        var successor = Guid.NewGuid();
        var nowUtc = StartedAtUtc.AddMinutes(1);
        var lease = new DaemonLifecycleRecoveryLease(
            host.CurrentEndpointRegistrationGenerationId,
            nowUtc.AddMinutes(1));

        var advanced = await store.TryAdvanceEndpointRegistrationAsync(
            definition.Kind,
            executionId,
            project,
            host.Process,
            host.EditorInstanceId,
            successor,
            lease,
            nowUtc,
            CancellationToken.None);
        var reused = await store.TryAdvanceEndpointRegistrationAsync(
            definition.Kind,
            executionId,
            project,
            host.Process,
            host.EditorInstanceId,
            Guid.NewGuid(),
            lease,
            nowUtc,
            CancellationToken.None);
        var expired = await store.TryAdvanceEndpointRegistrationAsync(
            definition.Kind,
            executionId,
            project,
            host.Process,
            host.EditorInstanceId,
            Guid.NewGuid(),
            new DaemonLifecycleRecoveryLease(successor, nowUtc),
            nowUtc,
            CancellationToken.None);

        Assert.Equal(LifecycleExecutionEndpointAdvanceOutcome.Advanced, advanced);
        Assert.Equal(LifecycleExecutionEndpointAdvanceOutcome.GenerationMismatch, reused);
        Assert.Equal(LifecycleExecutionEndpointAdvanceOutcome.RecoveryLeaseExpired, expired);
        Assert.Equal(
            successor,
            (await store.ReadAsync(definition.Kind, executionId, CancellationToken.None))!
                .Start.Host.CurrentEndpointRegistrationGenerationId);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryAdvanceEndpointRegistrationAsync_RejectsAnyPreviouslyAcceptedGenerationAfterReload ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "endpoint-generation-history");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var project = CreateProject();
        var host = CreateHost();
        await StartAsync(store, definition, executionId, project, host);
        var first = host.FirstEndpointRegistrationGenerationId;
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        var nowUtc = StartedAtUtc.AddMinutes(1);

        var firstAdvance = await store.TryAdvanceEndpointRegistrationAsync(
            definition.Kind,
            executionId,
            project,
            host.Process,
            host.EditorInstanceId,
            second,
            new DaemonLifecycleRecoveryLease(first, nowUtc.AddMinutes(1)),
            nowUtc,
            CancellationToken.None);
        var firstReplay = await store.TryAdvanceEndpointRegistrationAsync(
            definition.Kind,
            executionId,
            project,
            host.Process,
            host.EditorInstanceId,
            first,
            new DaemonLifecycleRecoveryLease(second, nowUtc.AddMinutes(1)),
            nowUtc,
            CancellationToken.None);
        var secondAdvance = await store.TryAdvanceEndpointRegistrationAsync(
            definition.Kind,
            executionId,
            project,
            host.Process,
            host.EditorInstanceId,
            third,
            new DaemonLifecycleRecoveryLease(second, nowUtc.AddMinutes(1)),
            nowUtc,
            CancellationToken.None);

        var reloadedStore = CreateStore(scope);
        var secondReplay = await reloadedStore.TryAdvanceEndpointRegistrationAsync(
            definition.Kind,
            executionId,
            project,
            host.Process,
            host.EditorInstanceId,
            second,
            new DaemonLifecycleRecoveryLease(third, nowUtc.AddMinutes(1)),
            nowUtc,
            CancellationToken.None);

        Assert.Equal(LifecycleExecutionEndpointAdvanceOutcome.Advanced, firstAdvance);
        Assert.Equal(
            LifecycleExecutionEndpointAdvanceOutcome.GenerationMismatch,
            firstReplay);
        Assert.Equal(LifecycleExecutionEndpointAdvanceOutcome.Advanced, secondAdvance);
        Assert.Equal(
            LifecycleExecutionEndpointAdvanceOutcome.GenerationMismatch,
            secondReplay);
        Assert.Equal(
            third,
            (await reloadedStore.ReadAsync(
                definition.Kind,
                executionId,
                CancellationToken.None))!
                .Start.Host.CurrentEndpointRegistrationGenerationId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WhenAcceptedGenerationHistoryDoesNotMatchStartHost_RejectsRecord (
        bool replaceFirst)
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            replaceFirst
                ? "generation-history-first-mismatch"
                : "generation-history-current-mismatch");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var project = CreateProject();
        var host = CreateHost();
        await StartAsync(store, definition, executionId, project, host);
        var successor = Guid.NewGuid();
        var nowUtc = StartedAtUtc.AddMinutes(1);
        Assert.Equal(
            LifecycleExecutionEndpointAdvanceOutcome.Advanced,
            await store.TryAdvanceEndpointRegistrationAsync(
                definition.Kind,
                executionId,
                project,
                host.Process,
                host.EditorInstanceId,
                successor,
                new DaemonLifecycleRecoveryLease(
                    host.CurrentEndpointRegistrationGenerationId,
                    nowUtc.AddMinutes(1)),
                nowUtc,
                CancellationToken.None));
        await MutateStoreRecordAsync(
            store,
            definition.Kind,
            executionId,
            root =>
            {
                var history =
                    root["acceptedEndpointRegistrationGenerationIds"]!.AsArray();
                history[replaceFirst ? 0 : history.Count - 1] =
                    Guid.NewGuid().ToString("D");
            });

        await Assert.ThrowsAsync<IOException>(() =>
            store.ReadAsync(
                    definition.Kind,
                    executionId,
                    CancellationToken.None)
                .AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WhenActionActiveReferenceHasNoAcceptedOwner_RejectsRecord ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "action-active-owner-missing");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        await MutateStoreRecordAsync(
            store,
            definition.Kind,
            executionId,
            root =>
                root["start"]!["lifecycleExecutionRef"]!["state"] =
                    TextVocabulary.GetText(
                        LifecycleExecutionState.Refreshing));

        await Assert.ThrowsAsync<IOException>(() =>
            store.ReadAsync(
                    definition.Kind,
                    executionId,
                    CancellationToken.None)
                .AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishTerminalAsync_WhenRecordIsNonTerminal_FixesIntentAndPublishesImmutableTerminalReference ()
    {
        using var scope = TestDirectories.CreateTempScope("lifecycle-execution-store", "publish-terminal");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(store, definition, executionId, CreateProject(), CreateHost());
        var evidenceBytes = Encoding.UTF8.GetBytes("""{"refreshed":true}""");
        var evidenceReference = await WriteArtifactAsync(
            scope,
            "lifecycle-evidence/refresh.json",
            evidenceBytes);
        var terminalRecord = CreateDeadlineTerminalRecord(
            registered.Binding!,
            artifactRefs: new ArtifactRef[] { evidenceReference });

        var published = await store.PublishTerminalAsync(terminalRecord, CancellationToken.None);
        var reconnected = await store.PublishTerminalAsync(terminalRecord, CancellationToken.None);

        Assert.Equal(LifecycleExecutionTerminalPublicationOutcome.Published, published.Outcome);
        Assert.NotNull(published.TerminalReference);
        Assert.Equal(LifecycleExecutionTerminalPublicationOutcome.Reconnected, reconnected.Outcome);
        Assert.Equal(published.TerminalReference, reconnected.TerminalReference);
        var stored = await store.ReadAsync(definition.Kind, executionId, CancellationToken.None);
        Assert.Equal(published.TerminalReference, stored!.TerminalReference);
        Assert.True(File.Exists(store.Paths.ResolveTerminalRecordPath(definition.Kind, executionId).Target.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishTerminalAsync_WhenDistinctCandidatesRace_ReturnsOneFixedRecordToBothPublishers ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "concurrent-terminal-candidates");
        var firstStore = CreateStore(scope);
        var secondStore = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            firstStore,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var binding = Assert.IsType<LifecycleExecutionStartBinding>(
            registered.Binding);
        var candidates = new LifecycleExecutionTerminalRecord[]
        {
            CreateDeadlineTerminalRecord(
                binding,
                ExecutionApplicationState.NotApplied),
            CreateDeadlineTerminalRecord(
                binding,
                ExecutionApplicationState.Indeterminate),
        };

        var publications = await Task.WhenAll(
            firstStore.PublishTerminalAsync(
                    candidates[0],
                    CancellationToken.None)
                .AsTask(),
            secondStore.PublishTerminalAsync(
                    candidates[1],
                    CancellationToken.None)
                .AsTask());

        Assert.All(publications, publication => Assert.True(publication.IsSuccess));
        Assert.Equal(
            publications[0].TerminalReference,
            publications[1].TerminalReference);
        Assert.Equal(
            publications[0].TerminalRecord,
            publications[1].TerminalRecord);
        Assert.Contains(publications[0].TerminalRecord, candidates);
        Assert.Contains(
            publications,
            publication =>
                publication.Outcome
                    == LifecycleExecutionTerminalPublicationOutcome.Published);
        Assert.Contains(
            publications,
            publication =>
                publication.Outcome
                    == LifecycleExecutionTerminalPublicationOutcome.Reconnected);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryRecoverTerminalPublicationAsync_WhenIntentWasDurablyFixed_CompletesWithoutCallerCandidate ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "recover-fixed-intent");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var publishing = LifecycleExecutionReferenceFactory.CreateStateProjection(
            registered.Binding!.LifecycleExecutionRef,
            ExecutionLifecycle.Recovery,
            LifecycleExecutionState.Publishing);
        var publishingStart = new LifecycleExecutionStartBinding(
            publishing,
            registered.Binding.Project,
            registered.Binding.Host,
            registered.Binding.StartedGeneration,
            registered.Binding.DeadlineUtc,
            registered.Binding.StartedAtUtc);
        var terminalRecord = CreateDeadlineTerminalRecord(publishingStart);
        var terminalBytes =
            JsonSerializer.SerializeToUtf8Bytes<LifecycleExecutionTerminalRecord>(
                terminalRecord,
                IpcJsonSerializerOptions.Default);
        var intent = new LifecycleExecutionTerminalPublicationIntent(
            publishingStart.Host.CurrentEndpointRegistrationGenerationId,
            terminalBytes);
        var interruptedRecord = new LifecycleExecutionStoreRecord(
            LifecycleExecutionStoreRecord.CurrentSchemaVersion,
            publishingStart,
            terminalReference: null,
            intent,
            sideEffectRightOwnerEndpointRegistrationGenerationId: null,
            new[]
            {
                publishingStart.Host.FirstEndpointRegistrationGenerationId,
            });
        await File.WriteAllTextAsync(
            store.Paths.ResolveRecordPath(definition.Kind, executionId).Value,
            JsonSerializer.Serialize(interruptedRecord, IpcJsonSerializerOptions.Default)
                + Environment.NewLine,
            CancellationToken.None);

        var recovered = await store.TryRecoverTerminalPublicationAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Published,
            recovered.Outcome);
        Assert.NotNull(recovered.TerminalReference);
        Assert.Equal(
            recovered.TerminalReference,
            (await store.ReadAsync(
                definition.Kind,
                executionId,
                CancellationToken.None))!.TerminalReference);
        Assert.Equal(
            terminalBytes,
            await File.ReadAllBytesAsync(
                store.Paths.ResolveTerminalRecordPath(definition.Kind, executionId).Target.Value,
                CancellationToken.None));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishTerminalAsync_WhenPrecedingArtifactIsUriOnly_PublishesWithoutFetchingUri ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "uri-only-preceding-artifact");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var binding = Assert.IsType<LifecycleExecutionStartBinding>(
            registered.Binding);
        ArtifactRef uriOnlyArtifact = new UriArtifactRef(
            new ArtifactKind("lifecycle.remoteEvidence"),
            new ArtifactMediaType("application/json"),
            new ArtifactUri("https://artifacts.example.invalid/lifecycle/evidence.json"),
            Sha256Digest.Parse(new string('a', 64)),
            sizeBytes: 42,
            StartedAtUtc);
        var terminalRecord = CreateDeadlineTerminalRecord(
            binding,
            artifactRefs: new[] { uriOnlyArtifact });

        var published = await store.PublishTerminalAsync(
            terminalRecord,
            CancellationToken.None);
        var reconnected = await store.PublishTerminalAsync(
            terminalRecord,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Published,
            published.Outcome);
        Assert.NotNull(published.TerminalReference);
        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Reconnected,
            reconnected.Outcome);
        Assert.Equal(published.TerminalReference, reconnected.TerminalReference);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishTerminalAsync_WhenStoredReferencePointsToDifferentValidArtifact_RejectsReconnect ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "terminal-reference-path");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var terminalRecord = await PublishDeadlineTerminalAsync(
            store,
            definition,
            executionId);
        var otherBytes = Encoding.UTF8.GetBytes("""{"not":"a terminal record"}""");
        const string otherPath = "other-valid-artifact.json";
        await File.WriteAllBytesAsync(
            Path.Combine(scope.FullPath, otherPath),
            otherBytes,
            CancellationToken.None);
        await MutateStoreRecordAsync(
            store,
            definition.Kind,
            executionId,
            root =>
            {
                var artifact = root["terminalReference"]!["terminalRecordRef"]!;
                artifact["path"] = otherPath;
                artifact["digest"] = Sha256Digest.Compute(otherBytes).ToString();
                artifact["sizeBytes"] = otherBytes.Length;
            });

        await Assert.ThrowsAsync<IOException>(() =>
            store.PublishTerminalAsync(terminalRecord, CancellationToken.None).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishTerminalAsync_WhenTerminalRecordReferencesItself_ReturnsRecoverablePublicationFailure ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "terminal-self-reference");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var publishing = LifecycleExecutionReferenceFactory.CreateStateProjection(
            registered.Binding!.LifecycleExecutionRef,
            ExecutionLifecycle.Recovery,
            LifecycleExecutionState.Publishing);
        var selfReference = new PathArtifactRef(
            LifecycleExecutionArtifactContract.TerminalRecordKind,
            LifecycleExecutionArtifactContract.TerminalRecordMediaType,
            store.Paths.CreateTerminalRecordArtifactPath(
                definition.Kind,
                executionId),
            Sha256Digest.Compute(ReadOnlySpan<byte>.Empty),
            sizeBytes: 0,
            StartedAtUtc);

        var terminalRecord = CreateDeadlineTerminalRecord(
            registered.Binding,
            artifactRefs: new ArtifactRef[] { selfReference });

        var publication = await store.PublishTerminalAsync(
            terminalRecord,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.PublicationFailed,
            publication.Outcome);
        Assert.Null(publication.TerminalReference);
        Assert.Equal(terminalRecord, publication.TerminalRecord);
        Assert.IsType<IOException>(publication.Failure);
        var stored = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored.TerminalReference);
        Assert.Equal(publishing, stored.CurrentReference);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Medium")]
    public async Task PublishTerminalAsync_WhenPrecedingArtifactCannotBeReverified_ReturnsFixedFailureAndRecovers (
        bool modifyAfterReference)
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            modifyAfterReference
                ? "modified-preceding-artifact"
                : "missing-preceding-artifact");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var publishing = LifecycleExecutionReferenceFactory.CreateStateProjection(
            registered.Binding!.LifecycleExecutionRef,
            ExecutionLifecycle.Recovery,
            LifecycleExecutionState.Publishing);
        const string artifactPath = "lifecycle-evidence/preceding.json";
        var originalBytes = Encoding.UTF8.GetBytes("""{"state":"published"}""");
        var artifactReference = CreateArtifactReference(
            artifactPath,
            originalBytes);
        if (modifyAfterReference)
        {
            var absoluteArtifactPath = Path.Combine(
                scope.FullPath,
                artifactPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteArtifactPath)!);
            await File.WriteAllBytesAsync(
                absoluteArtifactPath,
                Encoding.UTF8.GetBytes("""{"state":"modified"}"""),
                CancellationToken.None);
        }

        var terminalRecord = CreateDeadlineTerminalRecord(
            registered.Binding,
            artifactRefs: new ArtifactRef[] { artifactReference });

        var publication = await store.PublishTerminalAsync(
            terminalRecord,
            CancellationToken.None);
        var failedRecovery = await store.TryRecoverTerminalPublicationAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.PublicationFailed,
            publication.Outcome);
        Assert.Null(publication.TerminalReference);
        Assert.Equal(terminalRecord, publication.TerminalRecord);
        Assert.Equal(publishing, publication.ReconnectableReference);
        Assert.IsType<IOException>(publication.Failure);
        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.PublicationFailed,
            failedRecovery.Outcome);
        Assert.Null(failedRecovery.TerminalReference);
        AssertTerminalRecordJsonEqual(
            terminalRecord,
            failedRecovery.TerminalRecord);
        Assert.Equal(publishing, failedRecovery.ReconnectableReference);
        Assert.IsType<IOException>(failedRecovery.Failure);
        var stored = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored.TerminalReference);
        Assert.Equal(publishing, stored.CurrentReference);

        var repairedArtifactPath = Path.Combine(
            scope.FullPath,
            artifactPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(repairedArtifactPath)!);
        await File.WriteAllBytesAsync(
            repairedArtifactPath,
            originalBytes,
            CancellationToken.None);
        var recovered = await store.TryRecoverTerminalPublicationAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Published,
            recovered.Outcome);
        Assert.NotNull(recovered.TerminalReference);
        AssertTerminalRecordJsonEqual(terminalRecord, recovered.TerminalRecord);
        Assert.Null(recovered.Failure);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishTerminalAsync_WhenExistingTerminalArtifactCannotBeReverified_ReturnsFixedFailureAndRecovers ()
    {
        using var scope = TestDirectories.CreateTempScope("lifecycle-execution-store", "publish-failure");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(store, definition, executionId, CreateProject(), CreateHost());
        var publishing = LifecycleExecutionReferenceFactory.CreateStateProjection(
            registered.Binding!.LifecycleExecutionRef,
            ExecutionLifecycle.Recovery,
            LifecycleExecutionState.Publishing);
        var terminalPath = store.Paths.ResolveTerminalRecordPath(definition.Kind, executionId).Target.Value;
        Directory.CreateDirectory(Path.GetDirectoryName(terminalPath)!);
        await File.WriteAllTextAsync(terminalPath, "not-the-terminal-record", CancellationToken.None);

        var terminalRecord = CreateDeadlineTerminalRecord(registered.Binding);

        var publication = await store.PublishTerminalAsync(
            terminalRecord,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.PublicationFailed,
            publication.Outcome);
        Assert.Null(publication.TerminalReference);
        Assert.Equal(terminalRecord, publication.TerminalRecord);
        Assert.IsType<IOException>(publication.Failure);
        var stored = await store.ReadAsync(definition.Kind, executionId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Null(stored.TerminalReference);
        Assert.Equal(publishing, stored.CurrentReference);

        File.Delete(terminalPath);
        var recovered = await store.TryRecoverTerminalPublicationAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Published,
            recovered.Outcome);
        Assert.NotNull(recovered.TerminalReference);
        AssertTerminalRecordJsonEqual(terminalRecord, recovered.TerminalRecord);
        Assert.Null(recovered.Failure);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [Trait("Size", "Medium")]
    public async Task TryAdvanceEndpointRegistrationAsync_WhenTerminalPublicationWasFixed_RecoversAndReverifiesFixedRecord (
        bool artifactWasPublished)
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            artifactWasPublished
                ? "intent-after-artifact"
                : "intent-before-artifact");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var publishing = LifecycleExecutionReferenceFactory.CreateStateProjection(
            registered.Binding!.LifecycleExecutionRef,
            ExecutionLifecycle.Recovery,
            LifecycleExecutionState.Publishing);
        var publishingStart = new LifecycleExecutionStartBinding(
            publishing,
            registered.Binding.Project,
            registered.Binding.Host,
            registered.Binding.StartedGeneration,
            registered.Binding.DeadlineUtc,
            registered.Binding.StartedAtUtc);
        var fixedTerminalRecord = CreateDeadlineTerminalRecord(
            publishingStart);
        var fixedTerminalBytes =
            JsonSerializer.SerializeToUtf8Bytes<LifecycleExecutionTerminalRecord>(
                fixedTerminalRecord,
                IpcJsonSerializerOptions.Default);
        var intent = new LifecycleExecutionTerminalPublicationIntent(
            publishingStart.Host.CurrentEndpointRegistrationGenerationId,
            fixedTerminalBytes);
        var crashRecord = new LifecycleExecutionStoreRecord(
            LifecycleExecutionStoreRecord.CurrentSchemaVersion,
            publishingStart,
            terminalReference: null,
            intent,
            sideEffectRightOwnerEndpointRegistrationGenerationId: null,
            new[]
            {
                publishingStart.Host.FirstEndpointRegistrationGenerationId,
            });
        await File.WriteAllTextAsync(
            store.Paths.ResolveRecordPath(definition.Kind, executionId).Value,
            JsonSerializer.Serialize(crashRecord, IpcJsonSerializerOptions.Default)
                + Environment.NewLine,
            CancellationToken.None);
        if (artifactWasPublished)
        {
            var terminalPath = store.Paths.ResolveTerminalRecordPath(
                definition.Kind,
                executionId).Target.Value;
            Directory.CreateDirectory(Path.GetDirectoryName(terminalPath)!);
            await File.WriteAllBytesAsync(
                terminalPath,
                fixedTerminalBytes,
                CancellationToken.None);
        }

        var successorEndpointRegistrationGenerationId = Guid.NewGuid();
        var nowUtc = StartedAtUtc.AddMinutes(2);
        Assert.Equal(
            LifecycleExecutionEndpointAdvanceOutcome.TerminalPublicationFixed,
            await store.TryAdvanceEndpointRegistrationAsync(
                definition.Kind,
                executionId,
                CreateProject(),
                publishingStart.Host.Process,
                publishingStart.Host.EditorInstanceId,
                successorEndpointRegistrationGenerationId,
                new DaemonLifecycleRecoveryLease(
                    publishingStart.Host.CurrentEndpointRegistrationGenerationId,
                    nowUtc.AddMinutes(1)),
                nowUtc,
                CancellationToken.None));
        var publishingExecution = (await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None))!;
        Assert.Equal(
            publishingStart.Host.CurrentEndpointRegistrationGenerationId,
            publishingExecution.Start.Host.CurrentEndpointRegistrationGenerationId);
        var publicationStartedAtUtc = DateTimeOffset.UtcNow;
        var recovered = await store.TryRecoverTerminalPublicationAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var publicationCompletedAtUtc = DateTimeOffset.UtcNow;
        var firstReverification = await store.TryRecoverTerminalPublicationAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        var secondReverification = await store.TryRecoverTerminalPublicationAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Published,
            recovered.Outcome);
        Assert.NotNull(recovered.TerminalReference);
        Assert.Equal(fixedTerminalRecord, recovered.TerminalRecord);
        Assert.InRange(
            recovered.TerminalReference.TerminalRecordRef.CreatedAtUtc,
            publicationStartedAtUtc,
            publicationCompletedAtUtc);
        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Reconnected,
            firstReverification.Outcome);
        Assert.Equal(
            recovered.TerminalReference,
            firstReverification.TerminalReference);
        Assert.Equal(
            fixedTerminalRecord,
            firstReverification.TerminalRecord);
        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Reconnected,
            secondReverification.Outcome);
        Assert.Equal(
            recovered.TerminalReference,
            secondReverification.TerminalReference);
        Assert.Equal(
            fixedTerminalRecord,
            secondReverification.TerminalRecord);
        var terminalPathAfterRecovery = store.Paths.ResolveTerminalRecordPath(
            definition.Kind,
            executionId).Target.Value;
        Assert.Equal(
            fixedTerminalBytes,
            await File.ReadAllBytesAsync(
                terminalPathAfterRecovery,
                CancellationToken.None));
        var persistedTerminal =
            JsonSerializer.Deserialize<LifecycleExecutionTerminalRecord>(
                fixedTerminalBytes,
                IpcJsonSerializerOptions.Default)!;
        Assert.Equal(
            publishingStart.Host.CurrentEndpointRegistrationGenerationId,
            persistedTerminal.Host.CurrentEndpointRegistrationGenerationId);
        var terminalExecution = (await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None))!;
        Assert.Equal(
            publishingStart.Host.CurrentEndpointRegistrationGenerationId,
            terminalExecution.Start.Host.CurrentEndpointRegistrationGenerationId);
        Assert.Equal(
            recovered.TerminalReference,
            terminalExecution.TerminalReference);
    }

    [Theory]
    [InlineData("kind", "unexpected-terminal-record")]
    [InlineData("mediaType", "application/octet-stream")]
    [Trait("Size", "Medium")]
    public async Task ReadAsync_WhenStoredTerminalArtifactContractIsInvalid_RejectsRecord (
        string propertyName,
        string invalidValue)
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            $"invalid-terminal-{propertyName}");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        await PublishDeadlineTerminalAsync(store, definition, executionId);
        await MutateStoreRecordAsync(
            store,
            definition.Kind,
            executionId,
            root => root["terminalReference"]!["terminalRecordRef"]![propertyName] =
                invalidValue);

        await Assert.ThrowsAsync<IOException>(() =>
            store.ReadAsync(definition.Kind, executionId, CancellationToken.None).AsTask());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryRecoverTerminalPublicationAsync_WhenStoredEndpointNoLongerMatchesPublishedRecord_RejectsReconnect ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "terminal-endpoint-mismatch");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var project = CreateProject();
        var host = CreateHost();
        await StartAsync(
            store,
            definition,
            executionId,
            project,
            host);
        var successor = Guid.NewGuid();
        var nowUtc = StartedAtUtc.AddMinutes(1);
        Assert.Equal(
            LifecycleExecutionEndpointAdvanceOutcome.Advanced,
            await store.TryAdvanceEndpointRegistrationAsync(
                definition.Kind,
                executionId,
                project,
                host.Process,
                host.EditorInstanceId,
                successor,
                new DaemonLifecycleRecoveryLease(
                    host.CurrentEndpointRegistrationGenerationId,
                    nowUtc.AddMinutes(1)),
                nowUtc,
                CancellationToken.None));
        var advanced = (await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None))!;
        var terminalRecord = CreateDeadlineTerminalRecord(
            advanced.Start);
        var published = await store.PublishTerminalAsync(
            terminalRecord,
            CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Published,
            published.Outcome);
        var mismatchedEndpoint = Guid.NewGuid().ToString("D");
        await MutateStoreRecordAsync(
            store,
            definition.Kind,
            executionId,
            root =>
            {
                root["start"]!["host"]![
                    "currentEndpointRegistrationGenerationId"] =
                    mismatchedEndpoint;
                var history =
                    root["acceptedEndpointRegistrationGenerationIds"]!.AsArray();
                history[history.Count - 1] = mismatchedEndpoint;
            });

        var recovered = await store.TryRecoverTerminalPublicationAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);

        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Conflict,
            recovered.Outcome);
        Assert.Null(recovered.TerminalReference);
        Assert.Null(recovered.TerminalRecord);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task PublishTerminalAsync_WhenStoredTerminalStateDisagreesWithTerminalReason_RejectsReconnect ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            "terminal-state-mismatch");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var terminalRecord = await PublishDeadlineTerminalAsync(
            store,
            definition,
            executionId);
        await MutateStoreRecordAsync(
            store,
            definition.Kind,
            executionId,
            root => root["terminalReference"]!["state"] =
                TextVocabulary.GetText(LifecycleExecutionState.Completed));

        var reconnect = await store.PublishTerminalAsync(
            terminalRecord,
            CancellationToken.None);

        Assert.Equal(LifecycleExecutionTerminalPublicationOutcome.Conflict, reconnect.Outcome);
        Assert.Null(reconnect.TerminalReference);
    }

    [Theory]
    [InlineData(3_200_000, false)]
    [InlineData(4_200_000, true)]
    [Trait("Size", "Medium")]
    public async Task PublishTerminalAsync_WhenDurablePublicationWouldExceedReadLimit_LeavesStartRecordReadable (
        int artifactPayloadLength,
        bool terminalRecordExceedsLimit)
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-store",
            terminalRecordExceedsLimit
                ? "oversized-terminal-record"
                : "oversized-publication-intent");
        var store = CreateStore(scope);
        var definition = new LifecycleExecutionDefinition(LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var binding = Assert.IsType<LifecycleExecutionStartBinding>(
            registered.Binding);
        var terminalRecord = CreateDeadlineTerminalRecord(
            binding,
            artifactRefs: CreateLargeArtifactReferenceSet(
                artifactPayloadLength));
        var terminalBytes =
            JsonSerializer.SerializeToUtf8Bytes<LifecycleExecutionTerminalRecord>(
                terminalRecord,
                IpcJsonSerializerOptions.Default);
        Assert.Equal(
            terminalRecordExceedsLimit,
            terminalBytes.Length > MaximumStoredRecordBytes);
        if (!terminalRecordExceedsLimit)
        {
            var publishing = LifecycleExecutionReferenceFactory.CreateStateProjection(
                binding.LifecycleExecutionRef,
                ExecutionLifecycle.Recovery,
                LifecycleExecutionState.Publishing);
            var publishingStart = new LifecycleExecutionStartBinding(
                publishing,
                binding.Project,
                binding.Host,
                binding.StartedGeneration,
                binding.DeadlineUtc,
                binding.StartedAtUtc);
            var publicationState = new LifecycleExecutionStoreRecord(
                LifecycleExecutionStoreRecord.CurrentSchemaVersion,
                publishingStart,
                terminalReference: null,
                new LifecycleExecutionTerminalPublicationIntent(
                    publishingStart.Host.CurrentEndpointRegistrationGenerationId,
                    terminalBytes),
                sideEffectRightOwnerEndpointRegistrationGenerationId: null,
                new[]
                {
                    publishingStart.Host.FirstEndpointRegistrationGenerationId,
                });
            Assert.True(
                JsonSerializer.SerializeToUtf8Bytes(
                    publicationState,
                    IpcJsonSerializerOptions.Default).Length
                > MaximumStoredRecordBytes);
        }

        await Assert.ThrowsAsync<IOException>(() =>
            store.PublishTerminalAsync(terminalRecord, CancellationToken.None).AsTask());

        var stored = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(
            binding.LifecycleExecutionRef,
            stored.CurrentReference);
        Assert.Null(stored.TerminalReference);
    }

    private static IReadOnlyList<ArtifactRef>
        CreateLargeArtifactReferenceSet (int payloadLength)
    {
        const int MaximumPayloadPerUri = 8_000;
        var references = new List<ArtifactRef>();
        var remainingLength = payloadLength;
        var index = 0;
        while (remainingLength > 0)
        {
            var chunkLength = Math.Min(
                MaximumPayloadPerUri,
                remainingLength);
            references.Add(new UriArtifactRef(
                new ArtifactKind("lifecycle.remoteEvidence"),
                new ArtifactMediaType("application/json"),
                new ArtifactUri(
                    $"https://artifacts.example.invalid/{index}/"
                    + new string('a', chunkLength)),
                Sha256Digest.Compute(ReadOnlySpan<byte>.Empty),
                sizeBytes: 0,
                StartedAtUtc));
            remainingLength -= chunkLength;
            index++;
        }

        return references;
    }

    private static async ValueTask PersistPublishingStateAsync (
        FileLifecycleExecutionStore store,
        LifecycleExecutionKind kind,
        LifecycleExecutionStartBinding start)
    {
        var publishingReference =
            LifecycleExecutionReferenceFactory.CreateStateProjection(
                start.LifecycleExecutionRef,
                ExecutionLifecycle.Recovery,
                LifecycleExecutionState.Publishing);
        var publishingStart = new LifecycleExecutionStartBinding(
            publishingReference,
            start.Project,
            start.Host,
            start.StartedGeneration,
            start.DeadlineUtc,
            start.StartedAtUtc);
        var terminalRecord = CreateDeadlineTerminalRecord(publishingStart);
        var terminalBytes =
            JsonSerializer.SerializeToUtf8Bytes<LifecycleExecutionTerminalRecord>(
                terminalRecord,
                IpcJsonSerializerOptions.Default);
        var record = new LifecycleExecutionStoreRecord(
            LifecycleExecutionStoreRecord.CurrentSchemaVersion,
            publishingStart,
            terminalReference: null,
            new LifecycleExecutionTerminalPublicationIntent(
                publishingStart.Host.CurrentEndpointRegistrationGenerationId,
                terminalBytes),
            sideEffectRightOwnerEndpointRegistrationGenerationId: null,
            new[]
            {
                publishingStart.Host.FirstEndpointRegistrationGenerationId,
            });
        await File.WriteAllTextAsync(
            store.Paths.ResolveRecordPath(
                kind,
                start.LifecycleExecutionRef.Id).Value,
            JsonSerializer.Serialize(record, IpcJsonSerializerOptions.Default)
                + Environment.NewLine,
            CancellationToken.None);
    }

    private static FileLifecycleExecutionStore CreateStore (TestDirectoryScope scope)
    {
        return new FileLifecycleExecutionStore(AbsolutePath.Parse(scope.FullPath), ProjectFingerprint);
    }

    private static void AssertTerminalRecordJsonEqual (
        LifecycleExecutionTerminalRecord expected,
        LifecycleExecutionTerminalRecord? actual)
    {
        Assert.NotNull(actual);
        Assert.True(JsonNode.DeepEquals(
            JsonSerializer.SerializeToNode(expected, IpcJsonSerializerOptions.Default),
            JsonSerializer.SerializeToNode(actual, IpcJsonSerializerOptions.Default)));
    }

    private static ValueTask<LifecycleExecutionStartResult> StartAsync (
        FileLifecycleExecutionStore store,
        LifecycleExecutionDefinition definition,
        Guid executionId,
        UnityProjectIdentity project,
        LifecycleExecutionHostRegistration host,
        Sha256Digest? requestedDefinitionDigest = null)
    {
        return store.StartAsync(
            definition,
            executionId,
            requestedDefinitionDigest ?? LifecycleExecutionDefinitionDigest.Calculate(definition),
            project,
            host,
            StartedGeneration,
            DeadlineUtc,
            StartedAtUtc,
            CancellationToken.None);
    }

    private static UnityProjectIdentity CreateProject ()
    {
        return new UnityProjectIdentity("/workspace/UnityProject", ProjectFingerprint, "6000.1.4f1");
    }

    private static LifecycleExecutionHostRegistration CreateHost (
        Guid? editorInstanceId = null,
        Guid? currentEndpointRegistrationGenerationId = null)
    {
        var firstEndpointRegistrationGenerationId =
            Guid.Parse("10000000-0000-0000-0000-000000000001");
        return new LifecycleExecutionHostRegistration(
            new ProcessIdentity(4200, 123456),
            editorInstanceId ?? Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            firstEndpointRegistrationGenerationId,
            currentEndpointRegistrationGenerationId
                ?? firstEndpointRegistrationGenerationId);
    }

    private static RefreshLifecycleExecutionTerminalRecord CreateDeadlineTerminalRecord (
        LifecycleExecutionStartBinding binding,
        ExecutionApplicationState applicationState = ExecutionApplicationState.Unknown,
        IReadOnlyList<ArtifactRef>? artifactRefs = null)
    {
        return new RefreshLifecycleExecutionTerminalRecord(
            binding.LifecycleExecutionRef.Id,
            binding.LifecycleExecutionRef.DefinitionDigest,
            binding.Project,
            binding.Host,
            binding.StartedGeneration,
            terminalGeneration: null,
            binding.DeadlineUtc,
            binding.StartedAtUtc,
            binding.DeadlineUtc,
            LifecycleExecutionTerminalReason.DeadlineExceeded,
            applicationState,
            result: null,
            verdict: null,
            artifactRefs ?? Array.Empty<ArtifactRef>());
    }

    private static async Task<PathArtifactRef> WriteArtifactAsync (
        TestDirectoryScope scope,
        string artifactPath,
        byte[] contents)
    {
        var absoluteArtifactPath = Path.Combine(
            scope.FullPath,
            artifactPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteArtifactPath)!);
        await File.WriteAllBytesAsync(
            absoluteArtifactPath,
            contents,
            CancellationToken.None);
        return CreateArtifactReference(artifactPath, contents);
    }

    private static PathArtifactRef CreateArtifactReference (
        string artifactPath,
        byte[] contents)
    {
        return new PathArtifactRef(
            new ArtifactKind("lifecycle.testEvidence"),
            new ArtifactMediaType("application/json"),
            new ArtifactPath(artifactPath),
            Sha256Digest.Compute(contents),
            (ulong)contents.LongLength,
            StartedAtUtc);
    }

    private static async Task<RefreshLifecycleExecutionTerminalRecord>
        PublishDeadlineTerminalAsync (
            FileLifecycleExecutionStore store,
            LifecycleExecutionDefinition definition,
            Guid executionId)
    {
        var registered = await StartAsync(
            store,
            definition,
            executionId,
            CreateProject(),
            CreateHost());
        var binding = Assert.IsType<LifecycleExecutionStartBinding>(
            registered.Binding);
        var terminalRecord = CreateDeadlineTerminalRecord(binding);
        var published = await store.PublishTerminalAsync(
            terminalRecord,
            CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Published,
            published.Outcome);
        return terminalRecord;
    }

    private static async Task MutateStoreRecordAsync (
        FileLifecycleExecutionStore store,
        LifecycleExecutionKind kind,
        Guid executionId,
        Action<JsonObject> mutate)
    {
        var recordPath = store.Paths.ResolveRecordPath(kind, executionId).Value;
        var root = JsonNode.Parse(
            await File.ReadAllTextAsync(recordPath, CancellationToken.None))!
            .AsObject();
        mutate(root);
        await File.WriteAllTextAsync(
            recordPath,
            root.ToJsonString(),
            CancellationToken.None);
    }

    private sealed record SideEffectAdmissionCheckpoint (bool Admitted);

    private sealed class ControlledSideEffectAdmissionCheckpointStore :
        ILifecycleExecutionSideEffectAdmissionCheckpointStore<
            SideEffectAdmissionCheckpoint>
    {
        private readonly bool delayMarkerPersistence;
        private int remainingMarkerPersistenceFailures;
        private readonly TaskCompletionSource markerPersistenceRelease =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource markerPersistenceStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource unadmittedReadObserved =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int admitted;
        private int markCount;

        public ControlledSideEffectAdmissionCheckpointStore (
            bool delayMarkerPersistence,
            bool failMarkerPersistence = false)
        {
            this.delayMarkerPersistence = delayMarkerPersistence;
            remainingMarkerPersistenceFailures =
                failMarkerPersistence ? 1 : 0;
        }

        public SideEffectAdmissionCheckpoint InitialCheckpoint { get; } =
            new(Admitted: false);

        public Task MarkerPersistenceStarted =>
            markerPersistenceStarted.Task;

        public Task UnadmittedReadObserved =>
            unadmittedReadObserved.Task;

        public int MarkCount => Volatile.Read(ref markCount);

        public bool IsAdmitted (
            SideEffectAdmissionCheckpoint checkpoint)
        {
            return checkpoint.Admitted;
        }

        public ValueTask<SideEffectAdmissionCheckpoint?> ReadAsync (
            Guid executionId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isAdmitted = Volatile.Read(ref admitted) != 0;
            if (!isAdmitted)
            {
                unadmittedReadObserved.TrySetResult();
            }

            return ValueTask.FromResult<SideEffectAdmissionCheckpoint?>(
                new SideEffectAdmissionCheckpoint(isAdmitted));
        }

        public async ValueTask<SideEffectAdmissionCheckpoint>
            MarkAdmittedAsync (
            SideEffectAdmissionCheckpoint checkpoint,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref markCount);
            markerPersistenceStarted.TrySetResult();
            if (delayMarkerPersistence)
            {
                await markerPersistenceRelease.Task.WaitAsync(
                    cancellationToken);
            }
            if (Interlocked.CompareExchange(
                    ref remainingMarkerPersistenceFailures,
                    value: 0,
                    comparand: 1)
                == 1)
            {
                throw new IOException(
                    "Synthetic side-effect admission marker failure.");
            }

            Volatile.Write(ref admitted, 1);
            return new SideEffectAdmissionCheckpoint(Admitted: true);
        }

        public void ReleaseMarkerPersistence ()
        {
            markerPersistenceRelease.TrySetResult();
        }
    }
}
