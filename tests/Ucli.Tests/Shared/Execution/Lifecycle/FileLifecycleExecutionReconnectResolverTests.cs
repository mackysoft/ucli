using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

namespace MackySoft.Ucli.Tests.Shared.Execution.Lifecycle;

public sealed class FileLifecycleExecutionReconnectResolverTests
{
    private static readonly DateTimeOffset StartedAtUtc =
        new(2026, 7, 31, 1, 2, 3, TimeSpan.Zero);
    private static readonly DateTimeOffset DeadlineUtc =
        StartedAtUtc.AddMinutes(5);

    public static TheoryData<LifecycleExecutionKind> Kinds =>
        new()
        {
            LifecycleExecutionKind.Refresh,
            LifecycleExecutionKind.Compile,
            LifecycleExecutionKind.PlayEnter,
            LifecycleExecutionKind.PlayExit,
        };

    [Theory]
    [MemberData(nameof(Kinds))]
    [Trait("Size", "Medium")]
    public async Task ResolveAsync_ForPublishedActionReference_RestoresOriginalRegistration (
        LifecycleExecutionKind kind)
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-reconnect",
            TextVocabulary.GetText(kind).Replace('.', '-'));
        var context = CreateProjectContext(scope);
        var definition = new LifecycleExecutionDefinition(kind);
        var executionId = Guid.NewGuid();
        var store = CreateStore(context);
        var start = await RegisterAsync(
            store,
            context,
            definition,
            executionId);
        var resolver = new FileLifecycleExecutionReconnectResolver();

        var result = await resolver.ResolveAsync(
            context,
            definition,
            start.LifecycleExecutionRef,
            CancellationToken.None);

        var open = Assert.IsType<LifecycleExecutionReconnectResolution.Open>(
            result);
        Assert.Equal(definition, open.Registration.Definition);
        Assert.Equal(executionId, open.Registration.ExecutionId);
        Assert.Equal(StartedAtUtc, open.Registration.StartedAtUtc);
        Assert.Equal(DeadlineUtc, open.Registration.DeadlineUtc);
        Assert.Equal(
            start,
            (await store.ReadAsync(kind, executionId, CancellationToken.None))!.Start);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ResolveAsync_ForTerminalOrStaleActiveReference_ReturnsTheSameTerminalContinuation ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-reconnect",
            "terminal-reference");
        var context = CreateProjectContext(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var store = CreateStore(context);
        var start = await RegisterAsync(
            store,
            context,
            definition,
            executionId);
        var publication = await store.PublishTerminalAsync(
            CreateDeadlineTerminalRecord(start),
            CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.Published,
            publication.Outcome);
        var terminalReference = publication.TerminalReference!;
        var resolver = new FileLifecycleExecutionReconnectResolver();

        var result = await resolver.ResolveAsync(
            context,
            definition,
            terminalReference,
            CancellationToken.None);

        var terminal =
            Assert.IsType<LifecycleExecutionReconnectResolution.Terminal>(
                result);
        Assert.Equal(terminalReference, terminal.ExecutionReference);

        var staleActiveResult = await resolver.ResolveAsync(
            context,
            definition,
            start.LifecycleExecutionRef,
            CancellationToken.None);

        var staleTerminal =
            Assert.IsType<LifecycleExecutionReconnectResolution.Terminal>(
                staleActiveResult);
        Assert.Equal(
            terminal.ExecutionReference,
            staleTerminal.ExecutionReference);
        Assert.Equal(
            terminal.TerminalRecord,
            staleTerminal.TerminalRecord);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ResolveAsync_WhenTerminalPublicationWasInterrupted_CompletesFixedIntentBeforeReconnect ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-reconnect",
            "recover-terminal-publication");
        var context = CreateProjectContext(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var store = CreateStore(context);
        var start = await RegisterAsync(
            store,
            context,
            definition,
            executionId);
        var publishing = LifecycleExecutionReferenceFactory.CreateStateProjection(
            start.LifecycleExecutionRef,
            ExecutionLifecycle.Recovery,
            LifecycleExecutionState.Publishing);
        var publishingStart = new LifecycleExecutionStartBinding(
            publishing,
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
        var interruptedRecord = new LifecycleExecutionStoreRecord(
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
            store.Paths.ResolveRecordPath(definition.Kind, executionId).Value,
            JsonSerializer.Serialize(interruptedRecord, IpcJsonSerializerOptions.Default)
                + Environment.NewLine,
            CancellationToken.None);
        var resolver = new FileLifecycleExecutionReconnectResolver();

        var result = await resolver.ResolveAsync(
            context,
            definition,
            start.LifecycleExecutionRef,
            CancellationToken.None);

        var terminal =
            Assert.IsType<LifecycleExecutionReconnectResolution.Terminal>(
                result);
        var terminalReference = terminal.ExecutionReference;
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Failed),
            terminalReference.State.Value);
        Assert.Equal(
            terminalReference,
            (await store.ReadAsync(
                definition.Kind,
                executionId,
                CancellationToken.None))!.TerminalReference);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ResolveAsync_WhenTerminalPublicationCannotComplete_RetainsRecoveryReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-reconnect",
            "failed-terminal-publication");
        var context = CreateProjectContext(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var store = CreateStore(context);
        var start = await RegisterAsync(
            store,
            context,
            definition,
            executionId);
        var terminalPath = store.Paths.ResolveTerminalRecordPath(
            definition.Kind,
            executionId).Target.Value;
        Directory.CreateDirectory(Path.GetDirectoryName(terminalPath)!);
        await File.WriteAllTextAsync(
            terminalPath,
            "invalid-terminal-record",
            CancellationToken.None);
        var publication = await store.PublishTerminalAsync(
            CreateDeadlineTerminalRecord(start),
            CancellationToken.None);
        Assert.Equal(
            LifecycleExecutionTerminalPublicationOutcome.PublicationFailed,
            publication.Outcome);
        var publishing = Assert.IsType<RecoveryExecutionRef>(
            publication.ReconnectableReference);
        var resolver = new FileLifecycleExecutionReconnectResolver();

        var result = await resolver.ResolveAsync(
            context,
            definition,
            start.LifecycleExecutionRef,
            CancellationToken.None);

        var publicationFailed =
            Assert.IsType<
                LifecycleExecutionReconnectResolution.PublicationFailed>(
                result);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            publicationFailed.Failure.Code);
        Assert.Equal(
            publication.ReconnectableReference,
            publicationFailed.CurrentReference);
        var stored = await store.ReadAsync(
            definition.Kind,
            executionId,
            CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(publishing, stored.CurrentReference);
        Assert.Null(stored.TerminalReference);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ResolveAsync_WhenPublishedTerminalCannotBeReverified_RetainsRecoveryReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-reconnect",
            "invalid-terminal-publication");
        var context = CreateProjectContext(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var store = CreateStore(context);
        var start = await RegisterAsync(
            store,
            context,
            definition,
            executionId);
        var publication = await store.PublishTerminalAsync(
            CreateDeadlineTerminalRecord(start),
            CancellationToken.None);
        Assert.True(publication.IsSuccess);
        await File.WriteAllTextAsync(
            store.Paths.ResolveTerminalRecordPath(
                definition.Kind,
                executionId).Target.Value,
            """{"tampered":true}""",
            CancellationToken.None);
        var resolver = new FileLifecycleExecutionReconnectResolver();

        var result = await resolver.ResolveAsync(
            context,
            definition,
            publication.TerminalReference!,
            CancellationToken.None);

        var publicationFailed =
            Assert.IsType<
                LifecycleExecutionReconnectResolution.PublicationFailed>(
                result);
        Assert.Equal(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            publicationFailed.Failure.Code);
        Assert.Equal(
            ExecutionLifecycle.Recovery,
            publicationFailed.CurrentReference.Lifecycle);
        Assert.Equal(
            TextVocabulary.GetText(LifecycleExecutionState.Publishing),
            publicationFailed.CurrentReference.State.Value);
        Assert.Equal(
            publication.TerminalReference!.Kind,
            publicationFailed.CurrentReference.Kind);
        Assert.Equal(
            publication.TerminalReference.Id,
            publicationFailed.CurrentReference.Id);
        Assert.Equal(
            publication.TerminalReference.DefinitionDigest,
            publicationFailed.CurrentReference.DefinitionDigest);
        Assert.Equal(
            publication.TerminalReference.StatusLocator,
            publicationFailed.CurrentReference.StatusLocator);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ResolveAsync_ForFabricatedTerminalReference_RejectsReference ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-reconnect",
            "fabricated-terminal-reference");
        var context = CreateProjectContext(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Compile);
        var executionId = Guid.NewGuid();
        var store = CreateStore(context);
        var start = await RegisterAsync(
            store,
            context,
            definition,
            executionId);
        var fabricatedReference = new TerminalExecutionRef(
            start.LifecycleExecutionRef.Kind,
            executionId,
            start.LifecycleExecutionRef.DefinitionDigest,
            new ExecutionState(
                TextVocabulary.GetText(LifecycleExecutionState.Completed)),
            start.LifecycleExecutionRef.StatusLocator,
            new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                new ArtifactPath(
                    $".ucli/local/artifacts/lifecycle-execution/compile/{executionId:N}/terminal.json"),
                Sha256Digest.Parse(new string('a', 64)),
                sizeBytes: 1,
                StartedAtUtc.AddMinutes(1)));
        var resolver = new FileLifecycleExecutionReconnectResolver();

        var result = await resolver.ResolveAsync(
            context,
            definition,
            fabricatedReference,
            CancellationToken.None);

        var rejected =
            Assert.IsType<LifecycleExecutionReconnectResolution.Rejected>(
                result);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, rejected.Failure.Code);
        Assert.False(
            (await store.ReadAsync(
                definition.Kind,
                executionId,
                CancellationToken.None))!.IsTerminal);
    }

    [Theory]
    [InlineData(ReconnectRejectionCase.Kind)]
    [InlineData(ReconnectRejectionCase.Digest)]
    [InlineData(ReconnectRejectionCase.State)]
    [InlineData(ReconnectRejectionCase.Project)]
    [InlineData(ReconnectRejectionCase.Missing)]
    [Trait("Size", "Medium")]
    public async Task ResolveAsync_WhenIdentityCannotResolve_RejectsBeforeMutatingStore (
        ReconnectRejectionCase rejectionCase)
    {
        using var scope = TestDirectories.CreateTempScope(
            "lifecycle-execution-reconnect",
            rejectionCase.ToString().ToLowerInvariant());
        var context = CreateProjectContext(scope);
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Refresh);
        var executionId = Guid.NewGuid();
        var store = CreateStore(context);
        ExecutionRef executionRef;
        ResolvedUnityProjectContext requestedProject = context;
        UcliCode expectedCode;
        if (rejectionCase == ReconnectRejectionCase.Missing)
        {
            executionRef = CreateReference(
                definition,
                executionId,
                store.Paths.CreateStatusLocator(definition.Kind, executionId));
            expectedCode = UcliCoreErrorCodes.InvalidArgument;
        }
        else
        {
            var start = await RegisterAsync(
                store,
                context,
                definition,
                executionId);
            executionRef = rejectionCase switch
            {
                ReconnectRejectionCase.Kind => CreateReference(
                    new LifecycleExecutionDefinition(
                        LifecycleExecutionKind.Compile),
                    executionId,
                    start.LifecycleExecutionRef.StatusLocator!),
                ReconnectRejectionCase.Digest => new ActiveExecutionRef(
                    definition.ExecutionKind,
                    executionId,
                    Sha256Digest.Parse(new string('f', 64)),
                    start.LifecycleExecutionRef.State,
                    start.LifecycleExecutionRef.StatusLocator),
                ReconnectRejectionCase.State => new ActiveExecutionRef(
                    definition.ExecutionKind,
                    executionId,
                    start.LifecycleExecutionRef.DefinitionDigest,
                    new ExecutionState(
                        TextVocabulary.GetText(
                            LifecycleExecutionState.Completed)),
                    start.LifecycleExecutionRef.StatusLocator),
                _ => start.LifecycleExecutionRef,
            };
            requestedProject = rejectionCase == ReconnectRejectionCase.Project
                ? CreateProjectContext(
                    scope,
                    new ProjectFingerprint(new string('b', 64)))
                : context;
            expectedCode = rejectionCase == ReconnectRejectionCase.Project
                ? LifecycleExecutionErrorCodes.ProjectMismatch
                : rejectionCase == ReconnectRejectionCase.State
                    ? UcliCoreErrorCodes.InvalidArgument
                    : LifecycleExecutionErrorCodes.DefinitionConflict;
        }

        var filesBefore = Directory
            .EnumerateFiles(scope.FullPath, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var resolver = new FileLifecycleExecutionReconnectResolver();

        var result = await resolver.ResolveAsync(
            requestedProject,
            definition,
            executionRef,
            CancellationToken.None);

        var rejected =
            Assert.IsType<LifecycleExecutionReconnectResolution.Rejected>(
                result);
        Assert.Equal(expectedCode, rejected.Failure.Code);
        Assert.Equal(
            filesBefore,
            Directory
                .EnumerateFiles(scope.FullPath, "*", SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    public enum ReconnectRejectionCase
    {
        Kind = 1,
        Digest,
        State,
        Project,
        Missing,
    }

    private static FileLifecycleExecutionStore CreateStore (
        ResolvedUnityProjectContext context)
    {
        return FileLifecycleExecutionStore.CreateForProject(
            context.UnityProjectRoot,
            context.ProjectFingerprint);
    }

    private static async ValueTask<LifecycleExecutionStartBinding> RegisterAsync (
        FileLifecycleExecutionStore store,
        ResolvedUnityProjectContext context,
        LifecycleExecutionDefinition definition,
        Guid executionId)
    {
        var result = await store.StartAsync(
            definition,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new UnityProjectIdentity(
                context.UnityProjectRoot.Value,
                context.ProjectFingerprint,
                context.UnityVersion),
            CreateHost(),
            new UnityEditorGenerationSnapshot(10, 20, 30, 40),
            DeadlineUtc,
            StartedAtUtc,
            CancellationToken.None);
        return result.Binding!;
    }

    private static ActiveExecutionRef CreateReference (
        LifecycleExecutionDefinition definition,
        Guid executionId,
        ExecutionStatusLocator statusLocator)
    {
        return new ActiveExecutionRef(
            definition.ExecutionKind,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(
                TextVocabulary.GetText(LifecycleExecutionState.Registered)),
            statusLocator);
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
        CreateDeadlineTerminalRecord (
            LifecycleExecutionStartBinding binding)
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
            ExecutionApplicationState.Unknown,
            result: null,
            verdict: null,
            artifactRefs: Array.Empty<ArtifactRef>());
    }

    private static ResolvedUnityProjectContext CreateProjectContext (
        TestDirectoryScope scope,
        ProjectFingerprint? projectFingerprint = null)
    {
        var root = AbsolutePath.Parse(scope.FullPath);
        return ResolvedUnityProjectContext.Create(
            root,
            root,
            projectFingerprint
                ?? new ProjectFingerprint(new string('a', 64)),
            UnityProjectPathSource.CommandOption,
            scope.FullPath,
            "6000.1.4f1");
    }
}
