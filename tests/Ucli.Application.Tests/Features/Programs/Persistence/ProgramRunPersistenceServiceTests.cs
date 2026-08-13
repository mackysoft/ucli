using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Parsing;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Features.Programs.Resolution;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Features.Programs.Persistence;

public sealed class ProgramRunPersistenceServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task RegisterLoadAndCancel_PersistsOneGeneratedPendingRunWithoutExecution ()
    {
        var project = CreateProject();
        var store = new RecordingStore();
        var runId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var service = new ProgramRunPersistenceService(new RecordingFactory(store), new FixedGuidGenerator(runId), TimeProvider.System);
        var registration = await service.RegisterAsync(CreateRequest(project));

        Assert.True(registration.Created);
        Assert.Equal(runId, registration.Current.RunId);
        Assert.Equal(ProgramRunState.Created, registration.Current.State);
        Assert.Equal(ProgramStepState.Deferred, Assert.Single(registration.Current.Steps).State);
        Assert.Empty(registration.Current.ChildExecutionRefs);
        Assert.Null(registration.Current.Steps[0].ChildExecutionRef);
        Assert.Equal(["publish", "create"], store.Operations);
        Assert.Equal(runId, store.PublishedRunId);
        Assert.NotNull(store.PublishedSnapshot);
        Assert.NotNull(registration.Current.DefinitionSnapshotRef);
        Assert.NotEqual(Guid.Empty, registration.Current.RunId);
        Assert.All(registration.Current.Steps, static step => Assert.Equal(ProgramStepState.Deferred, step.State));

        var loaded = await service.LoadAsync(project, runId);
        Assert.NotNull(loaded);
        Assert.Equal(runId, loaded.Run.RunId);
        Assert.Equal(0, loaded.Run.Version);
        Assert.Equal(Sha256Digest.Parse("14c934ffaac9d7cfce1bcda1de4d74cfbc14d35d8f3eae8d119dfb2e84c5c629"), loaded.Run.DefinitionDigest);
        Assert.Equal(ProgramRunState.Created, loaded.Run.State);
        Assert.Equal(0, loaded.Run.Cursor);
        Assert.Equal(ProgramStepState.Deferred, Assert.Single(loaded.Run.Steps).State);
        Assert.Empty(loaded.Run.ChildExecutionRefs);
        Assert.False(loaded.Run.Cancellation.Requested);
        var cancelled = await service.RequestCancellationAsync(project, runId, "USER_CANCELLED");
        var duplicate = await service.RequestCancellationAsync(project, runId, "ignored");
        Assert.True(cancelled!.Cancellation.Requested);
        Assert.Equal(1, cancelled.Version);
        Assert.Equal("USER_CANCELLED", cancelled.Cancellation.ReasonCode);
        Assert.Equal(1, duplicate!.Version);
        Assert.Equal("USER_CANCELLED", duplicate!.Cancellation.ReasonCode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestCancellationAsync_PreservesPersistedLivenessObservations ()
    {
        var project = CreateProject();
        var store = new RecordingStore();
        var runId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var service = new ProgramRunPersistenceService(new RecordingFactory(store), new FixedGuidGenerator(runId), TimeProvider.System);
        await service.RegisterAsync(CreateRequest(project));
        store.Replace(store.CreatedRun! with
        {
            SupervisorObservation = new ProgramProcessLivenessObservation(ProcessIdentityStatus.Matching, DateTimeOffset.UtcNow),
            HostObservation = new ProgramProcessLivenessObservation(ProcessIdentityStatus.Unobservable, DateTimeOffset.UtcNow),
        });

        var cancelled = await service.RequestCancellationAsync(project, runId, "USER_CANCELLED");

        Assert.Equal(1, cancelled!.Version);
        Assert.Equal("USER_CANCELLED", cancelled.Cancellation.ReasonCode);
        Assert.Equal(ProcessIdentityStatus.Matching, cancelled.SupervisorObservation!.Status);
        Assert.Equal(ProcessIdentityStatus.Unobservable, cancelled.HostObservation!.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RegisterAsync_WhenDefinitionSnapshotPublicationFails_DoesNotCreateARun ()
    {
        var project = CreateProject();
        var store = new RecordingStore { PublishException = new InvalidDataException("snapshot rejected") };
        var service = new ProgramRunPersistenceService(
            new RecordingFactory(store),
            new FixedGuidGenerator(Guid.Parse("10000000-0000-0000-0000-000000000010")),
            TimeProvider.System);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.RegisterAsync(CreateRequest(project)).AsTask());

        Assert.Null(store.CreatedRun);
    }

    [Theory]
    [InlineData("refresh", 1000)]
    [InlineData("ready", 999)]
    [Trait("Size", "Small")]
    public async Task RegisterAsync_RejectsPendingStepThatDoesNotMatchTheFixedDefinition (string command, int timeoutMilliseconds)
    {
        var store = new RecordingStore();
        var service = new ProgramRunPersistenceService(new RecordingFactory(store), new FixedGuidGenerator(Guid.NewGuid()), TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(
            CreateRequest(CreateProject(), [new ProgramRunPendingStep(command, timeoutMilliseconds)])).AsTask());

        Assert.Null(store.CreatedRun);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RegisterAsync_RejectsPendingStepCountThatDoesNotMatchTheFixedDefinition ()
    {
        var store = new RecordingStore();
        var service = new ProgramRunPersistenceService(new RecordingFactory(store), new FixedGuidGenerator(Guid.NewGuid()), TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() => service.RegisterAsync(
            CreateRequest(CreateProject(), [new ProgramRunPendingStep("ready", 1000), new ProgramRunPendingStep("ready", 1000)])).AsTask());

        Assert.Null(store.CreatedRun);
    }

    [Theory]
    [InlineData(null, 1200, 1200, true)]
    [InlineData(null, 1200, 1000, false)]
    [InlineData(900, 1200, 900, true)]
    [InlineData(900, 1200, 1200, false)]
    [Trait("Size", "Small")]
    public void PendingStepValidation_UsesTheFixedExplicitTimeoutOrTheCapturedCommandTimeout (
        int? explicitTimeoutMilliseconds,
        int capturedCommandTimeoutMilliseconds,
        int pendingTimeoutMilliseconds,
        bool expected)
    {
        var definition = new ProgramDefinitionSnapshotFixedDefinition(
            [new ReadyProgramStep(explicitTimeoutMilliseconds)], [], null!, null!);
        var fixedContext = CreateFixedContext(new Dictionary<string, int> { ["ready"] = capturedCommandTimeoutMilliseconds });
        var request = CreateRequest(CreateProject(), [new ProgramRunPendingStep("ready", pendingTimeoutMilliseconds)], fixedContext);

        if (expected)
        {
            request.ValidatePendingSteps(definition);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => request.ValidatePendingSteps(definition));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PendingStepValidation_RejectsAnImplicitTimeoutWithoutACapturedCommandValue ()
    {
        var definition = new ProgramDefinitionSnapshotFixedDefinition([new ReadyProgramStep(null)], [], null!, null!);
        var request = CreateRequest(CreateProject(), [new ProgramRunPendingStep("ready", 1000)], CreateFixedContext(new Dictionary<string, int>()));

        Assert.Throws<ArgumentException>(() => request.ValidatePendingSteps(definition));
    }

    private static ProgramRunRegistrationRequest CreateRequest (
        ResolvedUnityProjectContext storageProject,
        IReadOnlyList<ProgramRunPendingStep>? pendingSteps = null,
        ProgramRunFixedContext? fixedContext = null) => new(
        storageProject,
        new UnityProjectIdentity("/project", storageProject.ProjectFingerprint, "6000.1.0f1"),
        CreateResolvedDefinition(),
        fixedContext ?? CreateFixedContext(new Dictionary<string, int>()),
        new LifecycleExecutionHostRegistration(new ProcessIdentity(1, 1), Guid.Parse("10000000-0000-0000-0000-000000000004"), Guid.Parse("10000000-0000-0000-0000-000000000005"), Guid.Parse("10000000-0000-0000-0000-000000000005")),
        new UnityEditorGenerationSnapshot(1, 2, 3, 4), null, DateTimeOffset.UtcNow.AddMinutes(1), pendingSteps ?? [new ProgramRunPendingStep("ready", 1000)]);

    private static ProgramRunFixedContext CreateFixedContext (IReadOnlyDictionary<string, int> commandTimeouts) => new(
        new ProgramEffectiveAuthorizationSnapshot(false, false, IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(false, false).ToString(), DateTimeOffset.UtcNow),
        new ProgramEffectiveConfigurationSnapshot(1, OperationPolicy.Safe, PlanTokenMode.Optional, ReadIndexMode.RequireFresh, [], 1000, commandTimeouts, false,
            IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(1, "safe", "optional", "requireFresh", [], 1000, commandTimeouts, false), DateTimeOffset.UtcNow),
        new ProgramExecutionModeSnapshot("auto", "daemon"),
        new ProgramAttachedSupervisorSnapshot(Guid.Parse("10000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000003"), new ProcessIdentity(2, 2), ProgramSupervisorConnection.Connected, ProgramSupervisorAvailability.Available, DateTimeOffset.UtcNow));

    private static ResolvedUnityProjectContext CreateProject () => ResolvedUnityProjectContext.Create(
        AbsolutePath.Parse(Path.Combine(Path.GetTempPath(), "ucli-project")), AbsolutePath.Parse(Path.Combine(Path.GetTempPath(), "ucli-repository")), new ProjectFingerprint(new string('e', 64)), UnityProjectPathSource.CurrentDirectory, null, "6000.1.0f1");

    private static ResolvedProgramDefinition CreateResolvedDefinition ()
    {
        using var document = JsonDocument.Parse("{\"steps\":[{\"command\":\"ready\",\"timeoutMilliseconds\":1000}]}");
        return new ResolvedProgramDefinition(new ProgramDefinition([new ReadyProgramStep(1000)], document.RootElement.Clone()), [],
            new ProgramSourceManifest(Sha256Digest.Parse("ad9deb8f7f2628012c4f15ffd29a79892ddceaa9237b530951f5b8aad33b60be"), ProgramRootSource.Stdin, null, null, Sha256Digest.Parse("7122109bf0b4b7b10dab6e76b8f9b57d7532d1f0e010ae0215366d78b3e23e28"), []), Sha256Digest.Parse("14c934ffaac9d7cfce1bcda1de4d74cfbc14d35d8f3eae8d119dfb2e84c5c629"));
    }

    private sealed class FixedGuidGenerator (Guid value) : IGuidGenerator
    {
        public Guid Generate () => value;
    }

    private sealed class RecordingFactory (RecordingStore store) : IProgramRunStoreFactory
    {
        public IProgramRunStore ForProject (ResolvedUnityProjectContext project) => store;
    }

    private sealed class RecordingStore : IProgramRunStore
    {
        private ProgramRunRecord? current;

        public Exception? PublishException { get; init; }

        public ProgramRunRecord? CreatedRun => current;

        public void Replace (ProgramRunRecord run) => current = run;

        public List<string> Operations { get; } = [];

        public Guid PublishedRunId { get; private set; }

        public ProgramDefinitionSnapshot? PublishedSnapshot { get; private set; }

        public ValueTask<ArtifactRef> PublishDefinitionSnapshotAsync (Guid runId, ProgramDefinitionSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Operations.Add("publish");
            PublishedRunId = runId;
            PublishedSnapshot = snapshot;
            if (PublishException is not null)
            {
                return ValueTask.FromException<ArtifactRef>(PublishException);
            }
            return ValueTask.FromResult<ArtifactRef>(new PathArtifactRef(new ArtifactKind("programDefinitionSnapshot"), new ArtifactMediaType("application/json"), new ArtifactPath("artifacts/definition.json"), Sha256Digest.Parse(new string('d', 64)), 1, DateTimeOffset.UtcNow));
        }

        public ValueTask<ProgramRunStoreCreateResult> CreateAsync (ProgramRunRecord run, CancellationToken cancellationToken = default)
        {
            Operations.Add("create");
            if (current is not null)
            {
                return ValueTask.FromResult(new ProgramRunStoreCreateResult(false, current));
            }
            current = run;
            return ValueTask.FromResult(new ProgramRunStoreCreateResult(true, run));
        }

        public ValueTask<ProgramRunRecord?> ReadAsync (Guid runId, CancellationToken cancellationToken = default) => ValueTask.FromResult<ProgramRunRecord?>(
            current is null ? null : CloneRun(current));

        public ValueTask<ProgramRunStoredDefinition?> ReadDefinitionAsync (Guid runId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<ProgramRunStoredDefinition?>(current is null ? null : new ProgramRunStoredDefinition(CloneRun(current), CreateStoredDefinition()));

        private static ProgramRunRecord CloneRun (ProgramRunRecord run) => new(
            run.SchemaVersion, run.Version, run.RunId, run.DefinitionDigest, run.DefinitionSnapshotRef,
            run.Project, run.FixedContext, run.Host, run.StartedGeneration, run.CurrentEditorGeneration,
            run.DeadlineUtc, run.StartedAtUtc, run.UpdatedAtUtc, run.State, run.Cursor,
            run.Steps, run.ChildExecutionRefs, run.Cancellation, run.TerminalRecordRef)
        {
            SupervisorObservation = run.SupervisorObservation,
            HostObservation = run.HostObservation,
            TerminalReasonCode = run.TerminalReasonCode,
        };

        private static ProgramDefinitionSnapshotFixedDefinition CreateStoredDefinition () => new(
            [new ReadyProgramStep(1000)], [], new ProgramSourceManifest(
                Sha256Digest.Parse("ad9deb8f7f2628012c4f15ffd29a79892ddceaa9237b530951f5b8aad33b60be"),
                ProgramRootSource.Stdin, null, null,
                Sha256Digest.Parse("7122109bf0b4b7b10dab6e76b8f9b57d7532d1f0e010ae0215366d78b3e23e28"), []),
            Sha256Digest.Parse("14c934ffaac9d7cfce1bcda1de4d74cfbc14d35d8f3eae8d119dfb2e84c5c629"));

        public ValueTask<ProgramRunStoreCompareExchangeResult> CompareExchangeAsync (ProgramRunRecord expected, ProgramRunRecord replacement, CancellationToken cancellationToken = default)
        {
            if (current is null)
            {
                throw new InvalidOperationException("Program Run must exist before it can be replaced.");
            }
            if (expected.Version != current.Version || replacement.Version != current.Version + 1)
            {
                return ValueTask.FromResult(new ProgramRunStoreCompareExchangeResult(false, current));
            }
            current = replacement;
            return ValueTask.FromResult(new ProgramRunStoreCompareExchangeResult(true, replacement));
        }

        public ValueTask<ProgramRunTerminalPublicationResult> PublishRunTerminalAsync (ProgramRunRecord expected, ProgramRunTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ProgramRunTerminalPublicationResult> PublishRunTimeoutTerminalAsync (ProgramRunRecord expected, int stepIndex, ProgramRunTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ProgramRunStepTerminalPublicationResult> PublishStepTerminalAsync (ProgramRunRecord expected, int stepIndex, ProgramStepTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
