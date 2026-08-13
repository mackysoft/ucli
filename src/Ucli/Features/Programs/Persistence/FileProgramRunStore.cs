using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Artifacts;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Programs.Persistence;

/// <summary> Stores Program Run aggregates under one project-local create-only and compare-and-swap boundary. </summary>
internal sealed class FileProgramRunStore : IProgramRunStore, IProgramArtifactStore
{
    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    private readonly AbsolutePath storageRoot;
    private readonly ProjectFingerprint projectFingerprint;

    public FileProgramRunStore (AbsolutePath storageRoot, ProjectFingerprint projectFingerprint)
    {
        this.storageRoot = storageRoot ?? throw new ArgumentNullException(nameof(storageRoot));
        this.projectFingerprint = projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint));
    }

    public async ValueTask<ProgramRunStoreCreateResult> CreateAsync (
        ProgramRunRecord run,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        EnsureRunProject(run);
        var statePath = ResolveStatePath(run.RunId);
        using var stateLock = await FileExclusiveLock.AcquireAsync(ResolveLockPath(run.RunId), LockTimeout, cancellationToken).ConfigureAwait(false);
        if (File.Exists(statePath.Value))
        {
            return new ProgramRunStoreCreateResult(false, await ReadRequiredWithoutLockAsync(statePath, run.RunId, cancellationToken).ConfigureAwait(false));
        }

        var definition = await ReadDefinitionSnapshotAsync(run, cancellationToken).ConfigureAwait(false);
        ProgramRunDefinitionBinding.Validate(run, definition);
        await WriteWithoutLockAsync(statePath, run, cancellationToken).ConfigureAwait(false);
        return new ProgramRunStoreCreateResult(true, run);
    }

    public async ValueTask<ArtifactRef> PublishDefinitionSnapshotAsync (Guid runId, ProgramDefinitionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        EnsureRunId(runId);
        ArgumentNullException.ThrowIfNull(snapshot);
        snapshot.Validate();
        var destination = ContainedPath.Create(storageRoot, RootRelativePath.Parse(
            $".ucli/local/projects/{StoragePathSegmentCodec.EncodeProjectFingerprint(projectFingerprint)}/artifacts/program-run/{StoragePathSegmentCodec.EncodeGuid(runId, nameof(runId))}/definition-snapshot.json"));
        var bytes = JsonSerializer.SerializeToUtf8Bytes(snapshot, IpcJsonSerializerOptions.Default);
        return await PublishTerminalArtifactAsync(destination, ProgramTerminalArtifactContract.DefinitionSnapshotKind, bytes,
            async (stream, token) =>
            {
                var read = await JsonSerializer.DeserializeAsync<ProgramDefinitionSnapshot>(stream, IpcJsonSerializerOptions.Default, token).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Program definition snapshot is empty.");
                read.Validate();
                if (read.DefinitionDigest != snapshot.DefinitionDigest || !JsonSerializer.SerializeToUtf8Bytes(read, IpcJsonSerializerOptions.Default).AsSpan().SequenceEqual(bytes))
                {
                    throw new InvalidDataException("Program definition snapshot does not preserve its fixed content.");
                }
            }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ArtifactRef> PublishAsync (
        Guid runId,
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken = default)
    {
        EnsureRunId(runId);
        ArgumentNullException.ThrowIfNull(kind);
        ArgumentNullException.ThrowIfNull(mediaType);
        if (content.IsEmpty)
        {
            throw new ArgumentException("Program artifact content must not be empty.", nameof(content));
        }

        var bytes = content.ToArray();
        var digest = Sha256Digest.Compute(bytes);
        var destination = ResolveProgramArtifactPath(runId, kind, mediaType, digest);
        return await PublishImmutableArtifactAsync(destination, kind, mediaType, bytes, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<byte[]?> ReadAsync (ArtifactRef artifact, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        if (artifact is not PathArtifactRef pathArtifact)
        {
            return null;
        }

        var path = ContainedPath.Create(storageRoot, RootRelativePath.Parse(pathArtifact.Path.Value));
        if (!File.Exists(path.Target.Value))
        {
            return null;
        }

        using var session = ImmutableArtifactFileReadBoundary.OpenSession(path, "Program artifact", cancellationToken);
        var before = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
        var bytes = await File.ReadAllBytesAsync(path.Target.Value, cancellationToken).ConfigureAwait(false);
        var after = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
        before.EnsureMatches(after, path.Target, "Program artifact changed during read");
        if (before.Digest != artifact.Digest || before.SizeBytes != artifact.SizeBytes)
        {
            throw new InvalidDataException("Program artifact content does not match its immutable reference.");
        }
        return bytes;
    }

    public async ValueTask<ProgramRunRecord?> ReadAsync (Guid runId, CancellationToken cancellationToken = default)
    {
        EnsureRunId(runId);
        var statePath = ResolveStatePath(runId);
        if (!File.Exists(statePath.Value))
        {
            return null;
        }

        using var stateLock = await FileExclusiveLock.AcquireAsync(ResolveLockPath(runId), LockTimeout, cancellationToken).ConfigureAwait(false);
        return File.Exists(statePath.Value)
            ? await ReadRequiredWithoutLockAsync(statePath, runId, cancellationToken).ConfigureAwait(false)
            : null;
    }

    public async ValueTask<ProgramRunStoredDefinition?> ReadDefinitionAsync (Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await ReadAsync(runId, cancellationToken).ConfigureAwait(false);
        if (run is null)
        {
            return null;
        }
        return new ProgramRunStoredDefinition(run, await ReadDefinitionSnapshotAsync(run, cancellationToken).ConfigureAwait(false));
    }

    public async ValueTask<ProgramRunStoreCompareExchangeResult> CompareExchangeAsync (
        ProgramRunRecord expected,
        ProgramRunRecord replacement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(replacement);
        EnsureRunProject(expected);
        EnsureRunProject(replacement);
        EnsureReplacementPreservesIdentity(expected, replacement);
        var statePath = ResolveStatePath(expected.RunId);
        using var stateLock = await FileExclusiveLock.AcquireAsync(ResolveLockPath(expected.RunId), LockTimeout, cancellationToken).ConfigureAwait(false);
        return await CompareExchangeWithoutLockAsync(statePath, expected, replacement, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<ProgramRunTerminalPublicationResult> PublishRunTerminalAsync (
        ProgramRunRecord expected,
        ProgramRunTerminalRecord terminalRecord,
        Func<ArtifactRef, ProgramRunRecord> createReplacement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(terminalRecord);
        ArgumentNullException.ThrowIfNull(createReplacement);
        EnsureRunProject(expected);
        terminalRecord.Validate();
        var statePath = ResolveStatePath(expected.RunId);
        using var stateLock = await FileExclusiveLock.AcquireAsync(ResolveLockPath(expected.RunId), LockTimeout, cancellationToken).ConfigureAwait(false);
        var current = await ReadRequiredWithoutLockAsync(statePath, expected.RunId, cancellationToken).ConfigureAwait(false);
        if (current.Version != expected.Version)
        {
            var existing = await TryReadIdempotentRunTerminalAsync(current, terminalRecord, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return new ProgramRunTerminalPublicationResult(true, existing, current);
            }
            throw new InvalidOperationException("Program Run terminal publication expected a stale aggregate version.");
        }
        EnsureRunTerminalIdentity(terminalRecord, current);

        var definition = await ReadDefinitionSnapshotAsync(current, cancellationToken).ConfigureAwait(false);

        var candidate = await CreateRunTerminalArtifactCandidateAsync(current.RunId, terminalRecord, cancellationToken).ConfigureAwait(false);
        var replacementTemplate = createReplacement(candidate.Reference) ?? throw new InvalidOperationException("Terminal replacement must be created.");
        EnsureRunTerminalMatchesReplacement(terminalRecord, replacementTemplate, candidate.Reference, definition, nameof(createReplacement));
        EnsureReplacementPreservesIdentity(current, replacementTemplate);
        var artifact = await PublishRunTerminalRecordAsync(candidate, terminalRecord, cancellationToken).ConfigureAwait(false);
        var replacement = ApplyRunTerminalArtifactReference(replacementTemplate, candidate.Reference, artifact);
        await WriteWithoutLockAsync(statePath, replacement, cancellationToken).ConfigureAwait(false);
        return new ProgramRunTerminalPublicationResult(true, artifact, replacement);
    }

    public async ValueTask<ProgramRunStepTerminalPublicationResult> PublishStepTerminalAsync (
        ProgramRunRecord expected,
        int stepIndex,
        ProgramStepTerminalRecord terminalRecord,
        Func<ArtifactRef, ProgramRunRecord> createReplacement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(terminalRecord);
        ArgumentNullException.ThrowIfNull(createReplacement);
        EnsureRunProject(expected);
        terminalRecord.Validate();
        var statePath = ResolveStatePath(expected.RunId);
        using var stateLock = await FileExclusiveLock.AcquireAsync(ResolveLockPath(expected.RunId), LockTimeout, cancellationToken).ConfigureAwait(false);
        var current = await ReadRequiredWithoutLockAsync(statePath, expected.RunId, cancellationToken).ConfigureAwait(false);
        if (current.Version != expected.Version)
        {
            var existing = await TryReadIdempotentStepTerminalAsync(current, stepIndex, terminalRecord, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return new ProgramRunStepTerminalPublicationResult(true, existing, current);
            }
            throw new InvalidOperationException("Program Step terminal publication expected a stale aggregate version.");
        }
        EnsureStepTerminalIdentity(terminalRecord, current, stepIndex);

        var candidate = await CreateStepTerminalArtifactCandidateAsync(current.RunId, stepIndex, terminalRecord, cancellationToken).ConfigureAwait(false);
        var replacementTemplate = createReplacement(candidate.Reference) ?? throw new InvalidOperationException("Terminal replacement must be created.");
        EnsureStepTerminalMatchesReplacement(terminalRecord, replacementTemplate, stepIndex, candidate.Reference, nameof(createReplacement));
        EnsureReplacementPreservesIdentity(current, replacementTemplate);
        var artifact = await PublishStepTerminalRecordAsync(candidate, terminalRecord, cancellationToken).ConfigureAwait(false);
        var replacement = ApplyStepTerminalArtifactReference(replacementTemplate, stepIndex, candidate.Reference, artifact);
        await WriteWithoutLockAsync(statePath, replacement, cancellationToken).ConfigureAwait(false);
        return new ProgramRunStepTerminalPublicationResult(true, artifact, replacement);
    }

    public async ValueTask<ProgramRunTerminalPublicationResult> PublishRunTimeoutTerminalAsync (
        ProgramRunRecord expected,
        int stepIndex,
        ProgramRunTerminalRecord terminalRecord,
        Func<ArtifactRef, ProgramRunRecord> createReplacement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(terminalRecord);
        ArgumentNullException.ThrowIfNull(createReplacement);
        EnsureRunProject(expected);
        terminalRecord.Validate();
        var statePath = ResolveStatePath(expected.RunId);
        using var stateLock = await FileExclusiveLock.AcquireAsync(ResolveLockPath(expected.RunId), LockTimeout, cancellationToken).ConfigureAwait(false);
        var current = await ReadRequiredWithoutLockAsync(statePath, expected.RunId, cancellationToken).ConfigureAwait(false);
        if (current.Version != expected.Version)
        {
            var existing = await TryReadIdempotentRunTerminalAsync(current, terminalRecord, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return new ProgramRunTerminalPublicationResult(true, existing, current);
            }
            throw new InvalidOperationException("Program Run timeout terminal publication expected a stale aggregate version.");
        }
        EnsureRunTimeoutTerminalTransition(current, stepIndex, terminalRecord);
        var definition = await ReadDefinitionSnapshotAsync(current, cancellationToken).ConfigureAwait(false);
        var candidate = await CreateRunTerminalArtifactCandidateAsync(current.RunId, terminalRecord, cancellationToken).ConfigureAwait(false);
        var replacementTemplate = createReplacement(candidate.Reference) ?? throw new InvalidOperationException("Terminal replacement must be created.");
        EnsureRunTerminalMatchesReplacement(terminalRecord, replacementTemplate, candidate.Reference, definition, nameof(createReplacement));
        EnsureReplacementPreservesIdentity(current, replacementTemplate, allowsRunTimeoutPlanningRestoration: true);
        var artifact = await PublishRunTerminalRecordAsync(candidate, terminalRecord, cancellationToken).ConfigureAwait(false);
        var replacement = ApplyRunTerminalArtifactReference(replacementTemplate, candidate.Reference, artifact);
        await WriteWithoutLockAsync(statePath, replacement, cancellationToken).ConfigureAwait(false);
        return new ProgramRunTerminalPublicationResult(true, artifact, replacement);
    }

    private AbsolutePath ResolveStatePath (Guid runId) => Resolve(runId, "state.json");

    private AbsolutePath ResolveLockPath (Guid runId) => Resolve(runId, "state.lock");

    private async ValueTask<ProgramRunStoreCompareExchangeResult> CompareExchangeWithoutLockAsync (
        AbsolutePath statePath,
        ProgramRunRecord expected,
        ProgramRunRecord replacement,
        CancellationToken cancellationToken)
    {
        var current = await ReadRequiredWithoutLockAsync(statePath, expected.RunId, cancellationToken).ConfigureAwait(false);
        if (current.Version != expected.Version)
        {
            return new ProgramRunStoreCompareExchangeResult(false, current);
        }
        EnsureReplacementPreservesIdentity(current, replacement);
        await WriteWithoutLockAsync(statePath, replacement, cancellationToken).ConfigureAwait(false);
        return new ProgramRunStoreCompareExchangeResult(true, replacement);
    }

    private static void EnsureRunTerminalIdentity (ProgramRunTerminalRecord terminal, ProgramRunRecord run)
    {
        if (terminal.RunId != run.RunId || terminal.DefinitionDigest != run.DefinitionDigest
            || terminal.DefinitionSnapshotRef != run.DefinitionSnapshotRef)
        {
            throw new ArgumentException("Program Run terminal record must identify the expected fixed Run.", nameof(terminal));
        }
    }

    private static void EnsureRunTimeoutTerminalTransition (ProgramRunRecord current, int stepIndex, ProgramRunTerminalRecord terminal)
    {
        EnsureRunTerminalIdentity(terminal, current);
        if (terminal.State != ProgramRunState.Failed || terminal.ReasonCode != "PROGRAM_RUN_TIMEOUT"
            || stepIndex < 0 || stepIndex >= current.Steps.Count
            || current.Steps[stepIndex].State != ProgramStepState.Planning
            || current.Steps[stepIndex].ExecutionPortInvoked
            || terminal.Steps.Count != current.Steps.Count)
        {
            throw new ArgumentException("A run-timeout terminal publication must atomically restore one unadmitted planning Step.", nameof(terminal));
        }
        var expected = current.Steps[stepIndex];
        var restored = terminal.Steps[stepIndex];
        if (restored.State != ProgramStepState.Deferred
            || restored.PlanningStartedAtUtc != expected.PlanningStartedAtUtc
            || restored.DeadlineUtc != expected.DeadlineUtc
            || restored.Execution is not null
            || restored.ExecutionPortInvoked
            || restored.StartedAtUtc is not null
            || restored.ApplicationState != ExecutionApplicationState.NotApplied)
        {
            throw new ArgumentException("A run-timeout terminal publication must preserve only the unadmitted planning audit facts.", nameof(terminal));
        }
    }

    private static void EnsureStepTerminalIdentity (ProgramStepTerminalRecord terminal, ProgramRunRecord run, int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= run.Steps.Count || terminal.StepIndex != stepIndex
            || terminal.RunId != run.RunId || terminal.DefinitionDigest != run.DefinitionDigest)
        {
            throw new ArgumentException("Program Step terminal record must identify one Step of the expected fixed Run.", nameof(terminal));
        }
    }

    private async ValueTask<ArtifactRef?> TryReadIdempotentRunTerminalAsync (
        ProgramRunRecord current,
        ProgramRunTerminalRecord terminal,
        CancellationToken cancellationToken)
    {
        if (!ProgramRunStateSemantics.IsTerminal(current.State) || current.TerminalRecordRef is null)
        {
            return null;
        }
        var candidate = await CreateRunTerminalArtifactCandidateAsync(current.RunId, terminal, cancellationToken).ConfigureAwait(false);
        var destination = candidate.Destination;
        if (!File.Exists(destination.Target.Value))
        {
            return null;
        }
        var artifact = await PublishRunTerminalRecordAsync(candidate, terminal, cancellationToken).ConfigureAwait(false);
        return HasSameArtifactContent(current.TerminalRecordRef, artifact) ? artifact : null;
    }

    private async ValueTask<ArtifactRef?> TryReadIdempotentStepTerminalAsync (
        ProgramRunRecord current,
        int stepIndex,
        ProgramStepTerminalRecord terminal,
        CancellationToken cancellationToken)
    {
        if (stepIndex < 0 || stepIndex >= current.Steps.Count || current.Steps[stepIndex].ResultRef is null)
        {
            return null;
        }
        var currentStep = current.Steps[stepIndex];
        if (!ProgramRunStateSemantics.IsTerminal(currentStep.State))
        {
            return null;
        }
        var candidate = await CreateStepTerminalArtifactCandidateAsync(current.RunId, stepIndex, terminal, cancellationToken).ConfigureAwait(false);
        var destination = candidate.Destination;
        if (!File.Exists(destination.Target.Value))
        {
            return null;
        }
        var artifact = await PublishStepTerminalRecordAsync(candidate, terminal, cancellationToken).ConfigureAwait(false);
        return HasSameArtifactContent(currentStep.ResultRef!, artifact)
            && StepTerminalMatchesAggregate(terminal, current, currentStep, stepIndex, artifact)
            ? artifact
            : null;
    }

    private AbsolutePath Resolve (Guid runId, string fileName)
    {
        EnsureRunId(runId);
        var projectDirectory = UcliStoragePathResolver.ResolveProjectDirectory(storageRoot, projectFingerprint);
        return ContainedPath.Create(
            projectDirectory,
            RootRelativePath.Parse($"program-runs/{StoragePathSegmentCodec.EncodeGuid(runId, nameof(runId))}/{fileName}")).Target;
    }

    private async ValueTask<TerminalArtifactCandidate> CreateRunTerminalArtifactCandidateAsync (
        Guid runId,
        ProgramRunTerminalRecord terminalRecord,
        CancellationToken cancellationToken)
    {
        var artifact = CreateRunTerminalArtifact(terminalRecord);
        artifact.Validate();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(artifact, IpcJsonSerializerOptions.Default);
        return CreateTerminalArtifactCandidate(runId, null, ProgramTerminalArtifactContract.RunTerminalRecordKind, bytes);
    }

    private static ProgramRunTerminalArtifact CreateRunTerminalArtifact (ProgramRunTerminalRecord source) => new(
        source.Project,
        source.RunId,
        source.DefinitionDigest,
        source.DefinitionSnapshotRef,
        source.FixedContext.Authorization,
        source.FixedContext.Configuration,
        source.DeadlineUtc,
        source.SourceManifest,
        source.State,
        source.Verdict,
        source.ApplicationState,
        source.Steps.Select(CreateRunTerminalStepArtifact).ToArray(),
        [],
        source.FinalSupervisorSnapshot,
        source.CurrentEditorGeneration,
        source.Cancellation,
        new ProgramRunTerminalSummary(
            source.State,
            source.Verdict,
            source.ReasonCode,
            source.ApplicationState,
            source.Steps.Count(static step => step.State == ProgramStepState.Completed),
            source.Steps.Count(static step => step.StartedAtUtc is null),
            source.CompletedAtUtc),
        source.StartedAtUtc,
        source.CompletedAtUtc);

    private static ProgramRunTerminalStepArtifact CreateRunTerminalStepArtifact (ProgramRunStepRecord source) => new(
        source.Command,
        source.TimeoutMilliseconds,
        source.State,
        source.Verdict,
        source.PlanningStartedAtUtc,
        source.DeadlineUtc,
        source.GenerationBefore,
        source.GenerationAfter,
        source.ApplicationState,
        source.RequestPlanRef,
        source.OperationDescriptorRefs,
        source.LifecycleExecutionRef,
        null,
        source.ResultRef,
        source.ErrorCode,
        source.StartedAtUtc,
        source.CompletedAtUtc);

    private async ValueTask<TerminalArtifactCandidate> CreateStepTerminalArtifactCandidateAsync (
        Guid runId,
        int stepIndex,
        ProgramStepTerminalRecord terminalRecord,
        CancellationToken cancellationToken)
    {
        var artifact = await CreateStepTerminalArtifactAsync(terminalRecord, cancellationToken).ConfigureAwait(false);
        artifact.Validate();
        var bytes = JsonSerializer.SerializeToUtf8Bytes(artifact, IpcJsonSerializerOptions.Default);
        return CreateTerminalArtifactCandidate(runId, stepIndex, ProgramTerminalArtifactContract.StepTerminalRecordKind, bytes);
    }

    private async ValueTask<ProgramStepTerminalArtifact> CreateStepTerminalArtifactAsync (
        ProgramStepTerminalRecord source,
        CancellationToken cancellationToken)
    {
        JsonElement? stepResult = null;
        if (source.StepResultRef is not null)
        {
            var bytes = await ReadAsync(source.StepResultRef, cancellationToken).ConfigureAwait(false)
                ?? throw new InvalidDataException("Program Step result artifact is unavailable.");
            using var document = JsonDocument.Parse(bytes);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Program Step result artifact must contain a JSON object.");
            }
            stepResult = document.RootElement.Clone();
        }
        return new ProgramStepTerminalArtifact(
            source.RunId,
            source.DefinitionDigest,
            source.Command,
            source.State,
            source.Verdict,
            source.ApplicationState,
            source.GenerationBefore,
            source.GenerationAfter,
            source.RequestPlanRef,
            source.OperationDescriptorRefs,
            source.LifecycleExecutionRef,
            null,
            stepResult,
            source.ArtifactRefs.Where(artifact => artifact != source.StepResultRef).ToArray(),
            source.ErrorCode,
            source.StartedAtUtc,
            source.CompletedAtUtc);
    }

    private TerminalArtifactCandidate CreateTerminalArtifactCandidate (Guid runId, int? stepIndex, ArtifactKind kind, byte[] bytes)
    {
        var digest = Sha256Digest.Compute(bytes);
        var destination = ResolveTerminalArtifactPath(runId, stepIndex, digest);
        return new TerminalArtifactCandidate(destination, bytes, new PathArtifactRef(
            kind, ProgramTerminalArtifactContract.JsonMediaType, new ArtifactPath(destination.RelativePath.Value), digest, (ulong)bytes.Length, DateTimeOffset.UnixEpoch));
    }

    private async ValueTask<ArtifactRef> PublishRunTerminalRecordAsync (
        TerminalArtifactCandidate candidate,
        ProgramRunTerminalRecord terminalRecord,
        CancellationToken cancellationToken)
    {
        return await PublishTerminalArtifactAsync(candidate.Destination, candidate.Reference.Kind, candidate.Bytes,
            async (stream, token) =>
            {
                var read = await JsonSerializer.DeserializeAsync<ProgramRunTerminalArtifact>(stream, IpcJsonSerializerOptions.Default, token).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Program Run terminal artifact is empty.");
                read.Validate();
                if (read.RunId != terminalRecord.RunId || read.DefinitionDigest != terminalRecord.DefinitionDigest
                    || !JsonSerializer.SerializeToUtf8Bytes(read, IpcJsonSerializerOptions.Default).AsSpan().SequenceEqual(candidate.Bytes))
                {
                    throw new InvalidDataException("Program Run terminal artifact does not preserve its fixed identity and content.");
                }
            }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ArtifactRef> PublishStepTerminalRecordAsync (
        TerminalArtifactCandidate candidate,
        ProgramStepTerminalRecord terminalRecord,
        CancellationToken cancellationToken)
    {
        return await PublishTerminalArtifactAsync(candidate.Destination, candidate.Reference.Kind, candidate.Bytes,
            async (stream, token) =>
            {
                var read = await JsonSerializer.DeserializeAsync<ProgramStepTerminalArtifact>(stream, IpcJsonSerializerOptions.Default, token).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Program Step terminal artifact is empty.");
                read.Validate();
                if (read.RunId != terminalRecord.RunId || read.DefinitionDigest != terminalRecord.DefinitionDigest
                    || !JsonSerializer.SerializeToUtf8Bytes(read, IpcJsonSerializerOptions.Default).AsSpan().SequenceEqual(candidate.Bytes))
                {
                    throw new InvalidDataException("Program Step terminal artifact does not preserve its fixed identity and content.");
                }
            }, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ArtifactRef> PublishTerminalArtifactAsync (
        ContainedPath destination, ArtifactKind kind, byte[] bytes,
        Func<Stream, CancellationToken, ValueTask> validate,
        CancellationToken cancellationToken)
    {
        if (!destination.Target.TryGetParent(out var parent))
        {
            throw new InvalidOperationException("Program terminal artifact path must have a parent directory.");
        }
        FileSystemAccessBoundary.EnsureSecureDirectory(parent);
        if (File.Exists(destination.Target.Value))
        {
            var existing = await CreateVerifiedArtifactReferenceAsync(destination, kind, validate, cancellationToken).ConfigureAwait(false);
            return existing;
        }
        if (Directory.Exists(destination.Target.Value))
        {
            throw new IOException("Program terminal artifact destination is not a regular file.");
        }

        var publisher = new ImmutableArtifactFilePublisher(static () => DateTimeOffset.UtcNow);
        var artifact = await publisher.PublishAsync(kind, ProgramTerminalArtifactContract.JsonMediaType, destination,
                async (stream, token) => await stream.WriteAsync(bytes, token).ConfigureAwait(false), validate, cancellationToken)
            .ConfigureAwait(false);
        await ImmutableArtifactFileVerifier.VerifyAsync(storageRoot, artifact, cancellationToken).ConfigureAwait(false);
        return artifact;
    }

    private async ValueTask<ArtifactRef> CreateVerifiedArtifactReferenceAsync (
        ContainedPath destination, ArtifactKind kind, Func<Stream, CancellationToken, ValueTask> validate, CancellationToken cancellationToken)
    {
        using var session = ImmutableArtifactFileReadBoundary.OpenSession(destination, "Program terminal artifact", cancellationToken);
        var before = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
        await session.ValidateAsync(validate, cancellationToken).ConfigureAwait(false);
        var after = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
        before.EnsureMatches(after, destination.Target, "Program terminal artifact changed during identity validation");
        return new PathArtifactRef(kind, ProgramTerminalArtifactContract.JsonMediaType,
            new ArtifactPath(destination.RelativePath.Value), before.Digest, before.SizeBytes, DateTimeOffset.UtcNow);
    }

    private ContainedPath ResolveTerminalArtifactPath (Guid runId, int? stepIndex, Sha256Digest contentDigest)
    {
        var contentSegment = StoragePathSegmentCodec.EncodeSha256Digest(contentDigest);
        var suffix = stepIndex is null ? $"terminal/{contentSegment}.json" : $"steps/{stepIndex.Value}/terminal/{contentSegment}.json";
        return ContainedPath.Create(storageRoot, RootRelativePath.Parse(
            $".ucli/local/projects/{StoragePathSegmentCodec.EncodeProjectFingerprint(projectFingerprint)}/artifacts/program-run/{StoragePathSegmentCodec.EncodeGuid(runId, nameof(runId))}/{suffix}"));
    }

    private ContainedPath ResolveProgramArtifactPath (
        Guid runId,
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        Sha256Digest contentDigest)
    {
        var extension = mediaType.Value == ProgramTerminalArtifactContract.JsonMediaType.Value ? "json" : "bin";
        return ContainedPath.Create(storageRoot, RootRelativePath.Parse(
            $".ucli/local/projects/{StoragePathSegmentCodec.EncodeProjectFingerprint(projectFingerprint)}/artifacts/program-run/{StoragePathSegmentCodec.EncodeGuid(runId, nameof(runId))}/content/{kind.Value}/{StoragePathSegmentCodec.EncodeSha256Digest(contentDigest)}.{extension}"));
    }

    private async ValueTask<ArtifactRef> PublishImmutableArtifactAsync (
        ContainedPath destination,
        ArtifactKind kind,
        ArtifactMediaType mediaType,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        if (!destination.Target.TryGetParent(out var parent))
        {
            throw new InvalidOperationException("Program artifact path must have a parent directory.");
        }
        FileSystemAccessBoundary.EnsureSecureDirectory(parent);
        if (File.Exists(destination.Target.Value))
        {
            using var session = ImmutableArtifactFileReadBoundary.OpenSession(destination, "Program artifact", cancellationToken);
            var before = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
            var after = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
            before.EnsureMatches(after, destination.Target, "Program artifact changed during identity validation");
            if (before.Digest != Sha256Digest.Compute(bytes) || before.SizeBytes != (ulong)bytes.Length)
            {
                throw new InvalidDataException("Program artifact destination already contains different immutable content.");
            }
            return new PathArtifactRef(kind, mediaType, new ArtifactPath(destination.RelativePath.Value), before.Digest, before.SizeBytes, DateTimeOffset.UtcNow);
        }
        if (Directory.Exists(destination.Target.Value))
        {
            throw new IOException("Program artifact destination is not a regular file.");
        }

        var publisher = new ImmutableArtifactFilePublisher(static () => DateTimeOffset.UtcNow);
        var artifact = await publisher.PublishAsync(
                kind,
                mediaType,
                destination,
                async (stream, token) => await stream.WriteAsync(bytes, token).ConfigureAwait(false),
                static (_, _) => ValueTask.CompletedTask,
                cancellationToken)
            .ConfigureAwait(false);
        await ImmutableArtifactFileVerifier.VerifyAsync(storageRoot, artifact, cancellationToken).ConfigureAwait(false);
        return artifact;
    }

    private static ProgramRunRecord ApplyRunTerminalArtifactReference (
        ProgramRunRecord replacementTemplate,
        ArtifactRef candidateReference,
        ArtifactRef verifiedReference)
    {
        EnsureVerifiedReferenceMatchesCandidate(candidateReference, verifiedReference);
        return new ProgramRunRecord(
            replacementTemplate.SchemaVersion, replacementTemplate.Version, replacementTemplate.RunId, replacementTemplate.DefinitionDigest, replacementTemplate.DefinitionSnapshotRef,
            replacementTemplate.Project, replacementTemplate.FixedContext, replacementTemplate.Host, replacementTemplate.StartedGeneration, replacementTemplate.CurrentEditorGeneration,
            replacementTemplate.DeadlineUtc, replacementTemplate.StartedAtUtc, replacementTemplate.UpdatedAtUtc, replacementTemplate.State, replacementTemplate.Cursor,
            replacementTemplate.Steps, replacementTemplate.ChildExecutionRefs, replacementTemplate.Cancellation, verifiedReference)
        {
            SupervisorObservation = replacementTemplate.SupervisorObservation,
            HostObservation = replacementTemplate.HostObservation,
            TerminalReasonCode = replacementTemplate.TerminalReasonCode,
        };
    }

    private static ProgramRunRecord ApplyStepTerminalArtifactReference (
        ProgramRunRecord replacementTemplate,
        int stepIndex,
        ArtifactRef candidateReference,
        ArtifactRef verifiedReference)
    {
        EnsureVerifiedReferenceMatchesCandidate(candidateReference, verifiedReference);
        var steps = replacementTemplate.Steps.Select((step, index) => index == stepIndex ? step with { ResultRef = verifiedReference } : step).ToArray();
        return new ProgramRunRecord(
            replacementTemplate.SchemaVersion, replacementTemplate.Version, replacementTemplate.RunId, replacementTemplate.DefinitionDigest, replacementTemplate.DefinitionSnapshotRef,
            replacementTemplate.Project, replacementTemplate.FixedContext, replacementTemplate.Host, replacementTemplate.StartedGeneration, replacementTemplate.CurrentEditorGeneration,
            replacementTemplate.DeadlineUtc, replacementTemplate.StartedAtUtc, replacementTemplate.UpdatedAtUtc, replacementTemplate.State, replacementTemplate.Cursor,
            steps, replacementTemplate.ChildExecutionRefs, replacementTemplate.Cancellation, replacementTemplate.TerminalRecordRef)
        {
            SupervisorObservation = replacementTemplate.SupervisorObservation,
            HostObservation = replacementTemplate.HostObservation,
            TerminalReasonCode = replacementTemplate.TerminalReasonCode,
        };
    }

    private static void EnsureVerifiedReferenceMatchesCandidate (ArtifactRef candidate, ArtifactRef verified)
    {
        if (!HasSameArtifactContent(candidate, verified))
        {
            throw new InvalidDataException("Published Program terminal artifact does not match its prevalidated candidate identity.");
        }
    }

    private sealed record TerminalArtifactCandidate (ContainedPath Destination, byte[] Bytes, PathArtifactRef Reference);

    private static bool HasSameArtifactContent (ArtifactRef left, ArtifactRef right) =>
        left.Kind == right.Kind && left.MediaType == right.MediaType && left.Digest == right.Digest && left.SizeBytes == right.SizeBytes
        && left is PathArtifactRef leftPath && right is PathArtifactRef rightPath && leftPath.Path == rightPath.Path;

    private async ValueTask<ProgramRunRecord> ReadRequiredWithoutLockAsync (
        AbsolutePath statePath,
        Guid expectedRunId,
        CancellationToken cancellationToken)
    {
        FileSystemAccessBoundary.EnsureSecureFile(statePath);
        var json = await File.ReadAllTextAsync(statePath.Value, cancellationToken).ConfigureAwait(false);
        try
        {
            var run = JsonSerializer.Deserialize<ProgramRunRecord>(json, IpcJsonSerializerOptions.Default);
            if (run is null || run.RunId != expectedRunId)
            {
                throw new InvalidDataException("Program Run state does not match its storage identity.");
            }
            EnsureRunProject(run);
            var definition = await VerifyDefinitionSnapshotAsync(run, cancellationToken).ConfigureAwait(false);
            ProgramRunDefinitionBinding.Validate(run, definition);
            await VerifyTerminalRecordsAsync(run, definition, cancellationToken).ConfigureAwait(false);
            return run;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or IOException)
        {
            throw new InvalidDataException("Program Run state is not a valid durable aggregate.", exception);
        }
    }

    private async ValueTask<ProgramDefinitionSnapshotFixedDefinition> VerifyDefinitionSnapshotAsync (ProgramRunRecord run, CancellationToken cancellationToken)
    {
        return await ReadDefinitionSnapshotAsync(run, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask VerifyTerminalRecordsAsync (
        ProgramRunRecord run,
        ProgramDefinitionSnapshotFixedDefinition definition,
        CancellationToken cancellationToken)
    {
        if (run.TerminalRecordRef is not null)
        {
            var terminal = await ReadVerifiedImmutableJsonArtifactAsync<ProgramRunTerminalArtifact, ProgramRunTerminalArtifact>(
                run.TerminalRecordRef,
                ProgramTerminalArtifactContract.RunTerminalRecordKind,
                "Program Run terminal record",
                static value => value.Validate(),
                cancellationToken).ConfigureAwait(false);
            if (!RunTerminalArtifactMatchesAggregate(terminal, run))
            {
                throw new InvalidDataException("Program Run terminal record does not describe its durable aggregate.");
            }
        }

        for (var index = 0; index < run.Steps.Count; index++)
        {
            var step = run.Steps[index];
            if (!ProgramRunStateSemantics.IsTerminal(step.State))
            {
                continue;
            }

            var terminalReference = step.ResultRef
                ?? throw new InvalidDataException("Terminal Program Step does not reference its terminal record.");
            var terminal = await ReadVerifiedImmutableJsonArtifactAsync<ProgramStepTerminalArtifact, ProgramStepTerminalArtifact>(
                terminalReference,
                ProgramTerminalArtifactContract.StepTerminalRecordKind,
                "Program Step terminal record",
                static value => value.Validate(),
                cancellationToken).ConfigureAwait(false);
            if (!await StepTerminalArtifactMatchesAggregateAsync(terminal, run, step, terminalReference, cancellationToken).ConfigureAwait(false))
            {
                throw new InvalidDataException("Program Step terminal record does not describe its durable Step.");
            }
        }
    }

    private async ValueTask<TResult> ReadVerifiedImmutableJsonArtifactAsync<TRecord, TResult> (
        ArtifactRef artifact,
        ArtifactKind expectedKind,
        string subject,
        Func<TRecord, TResult> validate,
        CancellationToken cancellationToken)
    {
        if (artifact.Kind != expectedKind || artifact.MediaType.Value != ProgramTerminalArtifactContract.JsonMediaType.Value)
        {
            throw new InvalidDataException($"{subject} must retain the expected JSON artifact kind.");
        }
        if (artifact is not PathArtifactRef pathArtifact)
        {
            throw new InvalidDataException($"{subject} must retain a local path reference.");
        }

        var path = ContainedPath.Create(storageRoot, RootRelativePath.Parse(pathArtifact.Path.Value));
        using var session = ImmutableArtifactFileReadBoundary.OpenSession(path, subject, cancellationToken);
        var before = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
        if (before.Digest != artifact.Digest || before.SizeBytes != artifact.SizeBytes)
        {
            throw new IOException($"{subject} digest or size changed during verification.");
        }
        TResult result = default!;
        var hasResult = false;
        await session.ValidateAsync(async (stream, token) =>
        {
            var record = await JsonSerializer.DeserializeAsync<TRecord>(stream, IpcJsonSerializerOptions.Default, token).ConfigureAwait(false)
                ?? throw new InvalidDataException($"{subject} is empty.");
            result = validate(record);
            hasResult = true;
        }, cancellationToken).ConfigureAwait(false);
        var after = await session.MeasureAsync(cancellationToken).ConfigureAwait(false);
        before.EnsureMatches(after, path.Target, $"{subject} changed during validation");
        return hasResult ? result : throw new InvalidDataException($"{subject} is empty.");
    }

    private async ValueTask<ProgramDefinitionSnapshotFixedDefinition> ReadDefinitionSnapshotAsync (ProgramRunRecord run, CancellationToken cancellationToken)
    {
        var definition = await ReadVerifiedImmutableJsonArtifactAsync<ProgramDefinitionSnapshot, ProgramDefinitionSnapshotFixedDefinition>(
            run.DefinitionSnapshotRef,
            ProgramTerminalArtifactContract.DefinitionSnapshotKind,
            "Program definition snapshot",
            static snapshot => snapshot.RestoreFixedDefinition(),
            cancellationToken).ConfigureAwait(false);
        if (definition.DefinitionDigest != run.DefinitionDigest)
        {
            throw new InvalidDataException("Program Run definition digest does not match its immutable snapshot.");
        }
        return definition;
    }

    private static async ValueTask WriteWithoutLockAsync (
        AbsolutePath statePath,
        ProgramRunRecord run,
        CancellationToken cancellationToken)
    {
        if (!statePath.TryGetParent(out var directory))
        {
            throw new InvalidOperationException("Program Run state path must have a parent directory.");
        }
        FileSystemAccessBoundary.EnsureSecureDirectory(directory);
        await FileUtilities.WriteAllTextAtomicallyAsync(
                statePath,
                JsonSerializer.Serialize(run, IpcJsonSerializerOptions.Default) + Environment.NewLine,
                cancellationToken)
            .ConfigureAwait(false);
        FileSystemAccessBoundary.EnsureSecureFile(statePath);
    }

    private static void EnsureReplacementPreservesIdentity (
        ProgramRunRecord expected,
        ProgramRunRecord replacement,
        bool allowsRunTimeoutPlanningRestoration = false)
    {
        if (expected.RunId != replacement.RunId
            || expected.DefinitionDigest != replacement.DefinitionDigest
            || expected.DefinitionSnapshotRef != replacement.DefinitionSnapshotRef
            || expected.Project != replacement.Project
            || expected.Host != replacement.Host
            || expected.StartedGeneration != replacement.StartedGeneration
            || expected.DeadlineUtc != replacement.DeadlineUtc
            || expected.StartedAtUtc != replacement.StartedAtUtc
            || replacement.Version != expected.Version + 1)
        {
            throw new ArgumentException("Program Run replacement must preserve fixed identity and advance exactly one version.", nameof(replacement));
        }
        if (!HasSameFixedContext(expected.FixedContext, replacement.FixedContext))
        {
            throw new ArgumentException("Program Run replacement must preserve its fixed authorization, configuration, mode, and supervisor context.", nameof(replacement));
        }
        if (ProgramRunStateSemantics.IsTerminal(expected.State)
            || (expected.State != replacement.State
                && !ProgramRunStateSemantics.CanTransitionTo(expected.State, replacement.State)))
        {
            throw new ArgumentException("Program Run replacement must follow the durable state machine.", nameof(replacement));
        }
        if (!ProgramRunStateSemantics.IsTerminal(replacement.State)
            && replacement.TerminalReasonCode != expected.TerminalReasonCode)
        {
            throw new ArgumentException("A nonterminal Program Run replacement cannot introduce or change a terminal reason.", nameof(replacement));
        }
        if (replacement.Cursor < expected.Cursor)
        {
            throw new ArgumentException("Program Run cursor must be monotonic.", nameof(replacement));
        }
        if (expected.Steps.Count != replacement.Steps.Count
            || expected.Steps.Where((step, index) => step.Command != replacement.Steps[index].Command
                || step.TimeoutMilliseconds != replacement.Steps[index].TimeoutMilliseconds).Any())
        {
            throw new ArgumentException("Program Run replacement must preserve its fixed step definition.", nameof(replacement));
        }
        for (var index = 0; index < expected.Steps.Count; index++)
        {
            EnsureStepReplacementIsAppendOnly(
                expected.Steps[index],
                replacement.Steps[index],
                allowsRunTimeoutPlanningRestoration,
                nameof(replacement));
        }
        EnsureCancellationIsAppendOnly(expected.Cancellation, replacement.Cancellation, nameof(replacement));
        EnsureObservationIsAppendOnly(expected.SupervisorObservation, replacement.SupervisorObservation, nameof(replacement));
        EnsureObservationIsAppendOnly(expected.HostObservation, replacement.HostObservation, nameof(replacement));
    }

    private static void EnsureStepReplacementIsAppendOnly (
        ProgramRunStepRecord expected,
        ProgramRunStepRecord replacement,
        bool allowsRunTimeoutPlanningRestoration,
        string parameterName)
    {
        if (expected.State == ProgramStepState.Planning && replacement.State == ProgramStepState.Deferred)
        {
            EnsureUninvokedPlanningRestoration(expected, replacement, allowsRunTimeoutPlanningRestoration, parameterName);
            return;
        }
        if (expected.State == ProgramStepState.Planning && replacement.State == ProgramStepState.Failed
            && !expected.ExecutionPortInvoked && replacement.ErrorCode == "PROGRAM_STEP_TIMEOUT")
        {
            EnsureUninvokedPlanningTimeout(expected, replacement, parameterName);
            return;
        }
        if (ProgramRunStateSemantics.IsTerminal(expected.State))
        {
            if (!ProgramRunStepsMatch(expected, replacement))
            {
                throw new ArgumentException("Program Run replacement must preserve each Step's terminal facts and allowed transition.", parameterName);
            }
            return;
        }
        if (!ProgramRunStateSemantics.CanTransitionTo(expected.State, replacement.State))
        {
            throw new ArgumentException("Program Run replacement must preserve each Step's terminal facts and allowed transition.", parameterName);
        }
        RequireSameWhenSet(expected.PlanningStartedAtUtc, replacement.PlanningStartedAtUtc, parameterName);
        RequireSameWhenSet(expected.DeadlineUtc, replacement.DeadlineUtc, parameterName);
        RequireSameWhenSet(expected.GenerationBefore, replacement.GenerationBefore, parameterName);
        RequireSameWhenSet(expected.GenerationAfter, replacement.GenerationAfter, parameterName);
        RequireSameWhenSet(expected.RequestPlanRef, replacement.RequestPlanRef, parameterName);
        EnsureLifecycleExecutionReferenceIsAppendOnly(expected.LifecycleExecutionRef, replacement.LifecycleExecutionRef, parameterName);
        if (expected.RequestExecution is not null
            && !HasSameRequestExecutionBoundaryOrNull(expected.RequestExecution, replacement.RequestExecution))
        {
            throw new ArgumentException("Program Run replacement cannot remove or replace an established Program Step fact.", parameterName);
        }
        RequireSameWhenSet(expected.Execution, replacement.Execution, parameterName);
        RequireSameWhenSet(expected.ResultRef, replacement.ResultRef, parameterName);
        RequireSameWhenSet(expected.StepResultRef, replacement.StepResultRef, parameterName);
        RequireSameWhenSet(expected.ErrorCode, replacement.ErrorCode, parameterName);
        RequireSameWhenSet(expected.StartedAtUtc, replacement.StartedAtUtc, parameterName);
        RequireSameWhenSet(expected.CompletedAtUtc, replacement.CompletedAtUtc, parameterName);
        if ((expected.OperationDescriptorRefs.Count > 0 && !expected.OperationDescriptorRefs.SequenceEqual(replacement.OperationDescriptorRefs))
            || (expected.ArtifactRefs.Count > 0 && !expected.ArtifactRefs.SequenceEqual(replacement.ArtifactRefs))
            || expected.ChildExecutionRef is not null || replacement.ChildExecutionRef is not null
            || (expected.Verdict.HasValue && expected.Verdict != replacement.Verdict)
            || (expected.ApplicationState != ExecutionApplicationState.NotApplied && expected.ApplicationState != replacement.ApplicationState))
        {
            throw new ArgumentException("Program Run replacement cannot replace established Program Step facts.", parameterName);
        }
    }

    private static void EnsureUninvokedPlanningRestoration (
        ProgramRunStepRecord expected,
        ProgramRunStepRecord replacement,
        bool allowsRunTimeoutPlanningRestoration,
        string parameterName)
    {
        if (expected.ExecutionPortInvoked
            || replacement.ExecutionPortInvoked
            || replacement.Execution is not null
            || replacement.StartedAtUtc is not null
            || replacement.CompletedAtUtc is not null
            || replacement.ResultRef is not null
            || replacement.StepResultRef is not null
            || replacement.ErrorCode is not null
            || replacement.LifecycleExecutionRef is not null
            || (allowsRunTimeoutPlanningRestoration
                ? !HasSameRequestExecutionBoundaryOrNull(expected.RequestExecution, replacement.RequestExecution)
                : replacement.RequestExecution is not null)
            || replacement.ArtifactRefs.Count != 0
            || replacement.ApplicationState != ExecutionApplicationState.NotApplied
            || replacement.Verdict is not null
            || (allowsRunTimeoutPlanningRestoration
                ? replacement.GenerationBefore != expected.GenerationBefore || replacement.GenerationAfter != expected.GenerationAfter
                : replacement.GenerationBefore is not null || replacement.GenerationAfter is not null)
            || (allowsRunTimeoutPlanningRestoration
                ? replacement.PlanningStartedAtUtc != expected.PlanningStartedAtUtc || replacement.DeadlineUtc != expected.DeadlineUtc
                : replacement.PlanningStartedAtUtc is not null || replacement.DeadlineUtc is not null)
            || !HasSameArtifactContentOrNull(expected.RequestPlanRef, replacement.RequestPlanRef)
            || expected.OperationDescriptorRefs.Count != replacement.OperationDescriptorRefs.Count
            || !expected.OperationDescriptorRefs.Zip(replacement.OperationDescriptorRefs, HasSameArtifactContent).All(static same => same))
        {
            throw new ArgumentException("Only an uninvoked Program planning Step may return to Deferred while preserving its plan facts.", parameterName);
        }
    }

    private static void EnsureUninvokedPlanningTimeout (ProgramRunStepRecord expected, ProgramRunStepRecord replacement, string parameterName)
    {
        if (replacement.ExecutionPortInvoked || replacement.Execution is not null || replacement.StartedAtUtc is not null
            || replacement.ApplicationState != ExecutionApplicationState.NotApplied || replacement.Verdict is not null
            || replacement.ErrorCode != "PROGRAM_STEP_TIMEOUT" || replacement.ResultRef is null || replacement.CompletedAtUtc is null
            || replacement.PlanningStartedAtUtc != expected.PlanningStartedAtUtc || replacement.DeadlineUtc != expected.DeadlineUtc)
        {
            throw new ArgumentException("An uninvoked Program planning Step may fail only as a not-applied step timeout.", parameterName);
        }
        EnsureEstablishedPlanningFactsPreserved(expected, replacement, parameterName);
    }

    private static void EnsureEstablishedPlanningFactsPreserved (
        ProgramRunStepRecord expected,
        ProgramRunStepRecord replacement,
        string parameterName)
    {
        RequireSameWhenSet(expected.GenerationBefore, replacement.GenerationBefore, parameterName);
        RequireSameWhenSet(expected.GenerationAfter, replacement.GenerationAfter, parameterName);
        RequireSameWhenSet(expected.RequestPlanRef, replacement.RequestPlanRef, parameterName);
        EnsureLifecycleExecutionReferenceIsAppendOnly(expected.LifecycleExecutionRef, replacement.LifecycleExecutionRef, parameterName);
        if (!HasSameRequestExecutionBoundaryOrNull(expected.RequestExecution, replacement.RequestExecution))
        {
            throw new ArgumentException("Program Run replacement cannot remove or replace an established Program Step fact.", parameterName);
        }
        RequireSameWhenSet(expected.StepResultRef, replacement.StepResultRef, parameterName);
        if ((expected.OperationDescriptorRefs.Count > 0 && !expected.OperationDescriptorRefs.SequenceEqual(replacement.OperationDescriptorRefs))
            || (expected.ArtifactRefs.Count > 0 && !expected.ArtifactRefs.SequenceEqual(replacement.ArtifactRefs))
            || expected.ChildExecutionRef is not null || replacement.ChildExecutionRef is not null)
        {
            throw new ArgumentException("An uninvoked Program planning Step timeout must retain established planning facts.", parameterName);
        }
    }

    private static void EnsureCancellationIsAppendOnly (ProgramCancellationRecord expected, ProgramCancellationRecord replacement, string parameterName)
    {
        if ((expected.Requested && expected != replacement)
            || (!expected.Requested && replacement.RequestedAtUtc is not null && !replacement.Requested)
            || (!expected.Requested && replacement.ReasonCode is not null && !replacement.Requested))
        {
            throw new ArgumentException("Program Run cancellation facts are append-only.", parameterName);
        }
    }

    /// <summary>
    /// A Lifecycle reference first establishes its immutable logical identity
    /// at durable start. The action can later mature that exact active
    /// reference into the terminal reference that carries its verified
    /// terminal-record artifact. No other replacement is a new Program fact.
    /// </summary>
    private static void EnsureLifecycleExecutionReferenceIsAppendOnly (
        ExecutionRef? expected,
        ExecutionRef? replacement,
        string parameterName)
    {
        if (expected is null)
        {
            return;
        }
        if (replacement is null)
        {
            throw new ArgumentException("Program Run replacement cannot remove an established Lifecycle Execution reference.", parameterName);
        }
        if (expected.Lifecycle == ExecutionLifecycle.Terminal)
        {
            if (expected != replacement)
            {
                throw new ArgumentException("A terminal Lifecycle Execution reference is immutable.", parameterName);
            }
            return;
        }
        if (expected == replacement)
        {
            return;
        }
        if (expected.Lifecycle == ExecutionLifecycle.Active
            && replacement.Lifecycle == ExecutionLifecycle.Terminal
            && expected.Kind == replacement.Kind
            && expected.Id == replacement.Id
            && expected.DefinitionDigest == replacement.DefinitionDigest)
        {
            return;
        }

        throw new ArgumentException(
            "A Lifecycle Execution reference may mature only from its active durable identity to its same terminal reference.",
            parameterName);
    }

    private static void EnsureObservationIsAppendOnly (
        ProgramProcessLivenessObservation? expected,
        ProgramProcessLivenessObservation? replacement,
        string parameterName)
    {
        if (expected is not null && (replacement is null || replacement.ObservedAtUtc < expected.ObservedAtUtc))
        {
            throw new ArgumentException("Program process liveness observations must not be removed or move backward in time.", parameterName);
        }
    }

    private static void RequireSameWhenSet<T> (T? expected, T? replacement, string parameterName)
        where T : struct
    {
        if (expected.HasValue && (!replacement.HasValue || !EqualityComparer<T>.Default.Equals(expected.Value, replacement.Value)))
        {
            throw new ArgumentException("Program Run replacement cannot remove or replace an established Program Step fact.", parameterName);
        }
    }

    private static void RequireSameWhenSet<T> (T? expected, T? replacement, string parameterName)
        where T : class
    {
        if (expected is not null && !EqualityComparer<T>.Default.Equals(expected, replacement))
        {
            throw new ArgumentException("Program Run replacement cannot remove or replace an established Program Step fact.", parameterName);
        }
    }

    private static void EnsureRunTerminalMatchesReplacement (
        ProgramRunTerminalRecord terminal,
        ProgramRunRecord replacement,
        ArtifactRef artifact,
        ProgramDefinitionSnapshotFixedDefinition definition,
        string parameterName)
    {
        if (!RunTerminalMatchesAggregate(terminal, replacement, artifact, definition))
        {
            throw new ArgumentException("Program Run terminal record must exactly describe its terminal replacement aggregate.", parameterName);
        }
    }

    private static void EnsureStepTerminalMatchesReplacement (ProgramStepTerminalRecord terminal, ProgramRunRecord replacement, int stepIndex, ArtifactRef artifact, string parameterName)
    {
        var replacementStep = replacement.Steps.Count > stepIndex ? replacement.Steps[stepIndex] : null;
        if (replacementStep is null || !StepTerminalMatchesAggregate(terminal, replacement, replacementStep, stepIndex, artifact))
        {
            throw new ArgumentException("Program Step terminal record must exactly describe its terminal Step.", parameterName);
        }
    }

    private static bool RunTerminalMatchesAggregate (
        ProgramRunTerminalRecord terminal,
        ProgramRunRecord run,
        ArtifactRef artifact,
        ProgramDefinitionSnapshotFixedDefinition definition)
    {
        return GetRunTerminalAggregateMismatch(terminal, run, artifact, definition) is null;
    }

    private static string? GetRunTerminalAggregateMismatch (
        ProgramRunTerminalRecord terminal,
        ProgramRunRecord run,
        ArtifactRef artifact,
        ProgramDefinitionSnapshotFixedDefinition definition)
    {
        return !ProgramRunStateSemantics.IsTerminal(run.State) ? "state"
            : run.TerminalRecordRef is null ? "reference"
            : !HasSameArtifactContent(run.TerminalRecordRef, artifact) ? "artifact"
            : terminal.Project != run.Project ? "project"
            : terminal.RunId != run.RunId ? "runId"
            : terminal.DefinitionDigest != run.DefinitionDigest ? "definitionDigest"
            : terminal.DefinitionSnapshotRef != run.DefinitionSnapshotRef ? "definitionSnapshotRef"
            : terminal.DeadlineUtc != run.DeadlineUtc ? "deadline"
            : !HasSameSourceManifest(terminal.SourceManifest, ProgramDefinitionSnapshotManifest.FromResolved(definition.SourceManifest)) ? "sourceManifest"
            : !HasSameFixedContext(terminal.FixedContext, run.FixedContext) ? "fixedContext"
            : terminal.State != run.State ? "state"
            : terminal.Verdict != run.Verdict ? "verdict"
            : terminal.ApplicationState != run.ApplicationState ? "applicationState"
            : terminal.Steps.Count != run.Steps.Count || !terminal.Steps.Zip(run.Steps, ProgramRunStepsMatch).All(static same => same) ? "steps"
            : terminal.ChildExecutionRefs.Count != 0 || run.ChildExecutionRefs.Count != 0 ? "childExecutionRefs"
            : terminal.Cancellation != run.Cancellation ? "cancellation"
            : terminal.CurrentEditorGeneration != run.CurrentEditorGeneration ? "generation"
            : terminal.FinalSupervisorObservation != run.SupervisorObservation ? "supervisorObservation"
            : terminal.FinalHostObservation != run.HostObservation ? "hostObservation"
            : !HasExpectedFinalSupervisorSnapshot(terminal, run) ? "supervisorSnapshot"
            : terminal.ReasonCode != run.TerminalReasonCode ? "reasonCode"
            : terminal.StartedAtUtc != run.StartedAtUtc ? "startedAtUtc"
            : terminal.CompletedAtUtc != run.UpdatedAtUtc ? "completedAtUtc"
            : null;
    }

    private static bool HasExpectedFinalSupervisorSnapshot (ProgramRunTerminalRecord terminal, ProgramRunRecord run)
    {
        var initial = run.FixedContext.Supervisor;
        var observation = run.SupervisorObservation;
        var lost = observation?.Status == ProcessIdentityStatus.ExitedOrReplaced;
        var expected = new ProgramAttachedSupervisorSnapshot(
            initial.SupervisorId,
            initial.HostId,
            initial.OwnerProcess,
            lost ? ProgramSupervisorConnection.Lost : initial.Connection,
            lost ? ProgramSupervisorAvailability.Unavailable : initial.Availability,
            observation?.ObservedAtUtc ?? initial.LastObservedAtUtc).Validate();
        return terminal.FinalSupervisorSnapshot == expected;
    }

    private static bool StepTerminalMatchesAggregate (
        ProgramStepTerminalRecord terminal,
        ProgramRunRecord run,
        ProgramRunStepRecord step,
        int stepIndex,
        ArtifactRef artifact)
    {
        return stepIndex >= 0
            && stepIndex < run.Steps.Count
            && ProgramRunStateSemantics.IsTerminal(step.State)
            && step.ResultRef is not null
            && HasSameArtifactContent(step.ResultRef, artifact)
            && terminal.RunId == run.RunId
            && terminal.DefinitionDigest == run.DefinitionDigest
            && terminal.StepIndex == stepIndex
            && terminal.Command == step.Command
            && terminal.State == step.State
            && terminal.Verdict == step.Verdict
            && terminal.ApplicationState == step.ApplicationState
            && terminal.GenerationBefore == step.GenerationBefore
            && terminal.GenerationAfter == step.GenerationAfter
            && terminal.RequestPlanRef == step.RequestPlanRef
            && terminal.OperationDescriptorRefs.SequenceEqual(step.OperationDescriptorRefs)
            && terminal.LifecycleExecutionRef == step.LifecycleExecutionRef
            && terminal.StepResultRef == step.StepResultRef
            && terminal.ArtifactRefs.Count == step.ArtifactRefs.Count
            && terminal.ArtifactRefs.Zip(step.ArtifactRefs, HasSameArtifactContent).All(static same => same)
            && terminal.ErrorCode == step.ErrorCode
            && terminal.StartedAtUtc == step.StartedAtUtc
            && terminal.CompletedAtUtc == step.CompletedAtUtc;
    }

    private static bool RunTerminalArtifactMatchesAggregate (ProgramRunTerminalArtifact terminal, ProgramRunRecord run)
    {
        return ProgramRunStateSemantics.IsTerminal(run.State)
            && terminal.Project == run.Project
            && terminal.RunId == run.RunId
            && terminal.DefinitionDigest == run.DefinitionDigest
            && terminal.DefinitionSnapshotRef == run.DefinitionSnapshotRef
            && terminal.Authorization.Digest == run.FixedContext.Authorization.Digest
            && terminal.Authorization.CapturedAtUtc == run.FixedContext.Authorization.CapturedAtUtc
            && terminal.Configuration.Digest == run.FixedContext.Configuration.Digest
            && terminal.Configuration.CapturedAtUtc == run.FixedContext.Configuration.CapturedAtUtc
            && terminal.DeadlineUtc == run.DeadlineUtc
            && terminal.State == run.State
            && terminal.Verdict == run.Verdict
            && terminal.ApplicationState == run.ApplicationState
            && terminal.ChildExecutionRefs.Count == 0
            && terminal.CurrentEditorGeneration == run.CurrentEditorGeneration
            && terminal.Cancellation == run.Cancellation
            && terminal.StartedAtUtc == run.StartedAtUtc
            && terminal.CompletedAtUtc == run.UpdatedAtUtc
            && terminal.Steps.Count == run.Steps.Count
            && terminal.Steps.Zip(run.Steps, static (artifact, record) => RunTerminalStepMatchesAggregate(artifact, record)).All(static value => value);
    }

    private async ValueTask<bool> StepTerminalArtifactMatchesAggregateAsync (
        ProgramStepTerminalArtifact terminal,
        ProgramRunRecord run,
        ProgramRunStepRecord step,
        ArtifactRef artifact,
        CancellationToken cancellationToken)
    {
        if (!ProgramRunStateSemantics.IsTerminal(step.State)
            || step.ResultRef is null
            || !HasSameArtifactContent(step.ResultRef, artifact)
            || terminal.RunId != run.RunId
            || terminal.DefinitionDigest != run.DefinitionDigest
            || terminal.Command != step.Command
            || terminal.State != step.State
            || terminal.Verdict != step.Verdict
            || terminal.ApplicationState != step.ApplicationState
            || terminal.GenerationBefore != step.GenerationBefore
            || terminal.GenerationAfter != step.GenerationAfter
            || terminal.RequestPlanRef != step.RequestPlanRef
            || !terminal.OperationDescriptorRefs.SequenceEqual(step.OperationDescriptorRefs)
            || terminal.LifecycleExecutionRef != step.LifecycleExecutionRef
            || terminal.ChildExecutionRef is not null
            || !terminal.ArtifactRefs.SequenceEqual(step.ArtifactRefs.Where(candidate => candidate != step.StepResultRef))
            || terminal.ErrorCode != step.ErrorCode
            || terminal.StartedAtUtc != step.StartedAtUtc
            || terminal.CompletedAtUtc != step.CompletedAtUtc)
        {
            return false;
        }

        if (step.StepResultRef is null)
        {
            return terminal.StepResult is null;
        }
        var bytes = await ReadAsync(step.StepResultRef, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return false;
        }
        using var document = JsonDocument.Parse(bytes);
        return terminal.StepResult is { } result
            && string.Equals(result.GetRawText(), document.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    private static bool RunTerminalStepMatchesAggregate (ProgramRunTerminalStepArtifact artifact, ProgramRunStepRecord record) =>
        artifact.Command == record.Command
        && artifact.TimeoutMilliseconds == record.TimeoutMilliseconds
        && artifact.State == record.State
        && artifact.Verdict == record.Verdict
        && artifact.PlanningStartedAtUtc == record.PlanningStartedAtUtc
        && artifact.StepDeadlineAtUtc == record.DeadlineUtc
        && artifact.GenerationBefore == record.GenerationBefore
        && artifact.GenerationAfter == record.GenerationAfter
        && artifact.ApplicationState == record.ApplicationState
        && artifact.RequestPlanRef == record.RequestPlanRef
        && artifact.OperationDescriptorRefs.SequenceEqual(record.OperationDescriptorRefs)
        && artifact.LifecycleExecutionRef == record.LifecycleExecutionRef
        && artifact.ChildExecutionRef is null
        && artifact.ResultRef == record.ResultRef
        && artifact.ErrorCode == record.ErrorCode
        && artifact.StartedAtUtc == record.StartedAtUtc
        && artifact.CompletedAtUtc == record.CompletedAtUtc;

    private static bool ProgramRunStepsMatch (ProgramRunStepRecord left, ProgramRunStepRecord right)
    {
        return GetProgramRunStepMismatch(left, right) is null;
    }

    private static string? GetProgramRunStepMismatch (ProgramRunStepRecord left, ProgramRunStepRecord right)
    {
        return left.Command != right.Command ? "command"
            : left.TimeoutMilliseconds != right.TimeoutMilliseconds ? "timeout"
            : left.State != right.State ? "state"
            : left.Verdict != right.Verdict ? "verdict"
            : left.PlanningStartedAtUtc != right.PlanningStartedAtUtc ? "planningStartedAtUtc"
            : left.DeadlineUtc != right.DeadlineUtc ? "deadline"
            : left.GenerationBefore != right.GenerationBefore ? "generationBefore"
            : left.GenerationAfter != right.GenerationAfter ? "generationAfter"
            : left.ApplicationState != right.ApplicationState ? "applicationState"
            : !HasSameArtifactContentOrNull(left.RequestPlanRef, right.RequestPlanRef) ? "requestPlanRef"
            : left.OperationDescriptorRefs.Count != right.OperationDescriptorRefs.Count ? "operationDescriptorRefs"
            : !left.OperationDescriptorRefs.Zip(right.OperationDescriptorRefs, HasSameArtifactContent).All(static same => same) ? "operationDescriptorRefs"
            : left.LifecycleExecutionRef != right.LifecycleExecutionRef ? "lifecycleExecutionRef"
            : !HasSameRequestExecutionBoundaryOrNull(left.RequestExecution, right.RequestExecution) ? "requestExecution"
            : left.Execution != right.Execution ? "execution"
            : left.ExecutionPortInvoked != right.ExecutionPortInvoked ? "executionPortInvoked"
            : left.ChildExecutionRef != right.ChildExecutionRef ? "childExecutionRef"
            : !HasSameArtifactContentOrNull(left.ResultRef, right.ResultRef) ? "resultRef"
            : !HasSameArtifactContentOrNull(left.StepResultRef, right.StepResultRef) ? "stepResultRef"
            : left.ArtifactRefs.Count != right.ArtifactRefs.Count ? "artifactRefs"
            : !left.ArtifactRefs.Zip(right.ArtifactRefs, HasSameArtifactContent).All(static same => same) ? "artifactRefs"
            : left.ErrorCode != right.ErrorCode ? "errorCode"
            : left.StartedAtUtc != right.StartedAtUtc ? "startedAtUtc"
            : left.CompletedAtUtc != right.CompletedAtUtc ? "completedAtUtc"
            : null;
    }

    private static bool HasSameArtifactContentOrNull (ArtifactRef? left, ArtifactRef? right)
    {
        return left is null ? right is null : right is not null && HasSameArtifactContent(left, right);
    }

    private static bool HasSameRequestExecutionBoundaryOrNull (
        ProgramRequestExecutionBoundary? left,
        ProgramRequestExecutionBoundary? right)
    {
        return left is null
            ? right is null
            : right is not null
                && left.ExecutionId == right.ExecutionId
                && left.Project == right.Project
                && left.Host == right.Host
                && left.StartedGeneration == right.StartedGeneration
                && HasSameArtifactContent(left.RequestPlanRef, right.RequestPlanRef)
                && left.OperationDescriptorRefs.Count == right.OperationDescriptorRefs.Count
                && left.OperationDescriptorRefs.Zip(right.OperationDescriptorRefs, HasSameArtifactContent).All(static same => same)
                && left.StartedAtUtc == right.StartedAtUtc
                && left.DeadlineUtc == right.DeadlineUtc;
    }

    private static bool HasSameFixedContext (ProgramRunFixedContext left, ProgramRunFixedContext right)
    {
        return left.Authorization == right.Authorization
            && left.ExecutionMode == right.ExecutionMode
            && left.Supervisor == right.Supervisor
            && left.Configuration.SchemaVersion == right.Configuration.SchemaVersion
            && left.Configuration.OperationPolicy == right.Configuration.OperationPolicy
            && left.Configuration.PlanTokenMode == right.Configuration.PlanTokenMode
            && left.Configuration.ReadIndexDefaultMode == right.Configuration.ReadIndexDefaultMode
            && left.Configuration.IpcDefaultTimeoutMilliseconds == right.Configuration.IpcDefaultTimeoutMilliseconds
            && left.Configuration.EvalEnabled == right.Configuration.EvalEnabled
            && left.Configuration.Digest == right.Configuration.Digest
            && left.Configuration.CapturedAtUtc == right.Configuration.CapturedAtUtc
            && left.Configuration.OperationAllowlist.SequenceEqual(right.Configuration.OperationAllowlist, StringComparer.Ordinal)
            && left.Configuration.IpcTimeoutMillisecondsByCommand.Count == right.Configuration.IpcTimeoutMillisecondsByCommand.Count
            && left.Configuration.IpcTimeoutMillisecondsByCommand.All(item => right.Configuration.IpcTimeoutMillisecondsByCommand.TryGetValue(item.Key, out var value) && value == item.Value);
    }

    private static bool HasSameSourceManifest (ProgramDefinitionSnapshotManifest left, ProgramDefinitionSnapshotManifest right)
    {
        return left.Digest == right.Digest && left.RootSource == right.RootSource && left.RootPath == right.RootPath
            && left.PresetId == right.PresetId && left.ProgramDigest == right.ProgramDigest
            && left.Sources.SequenceEqual(right.Sources);
    }

    private static void EnsureRunId (Guid runId)
    {
        if (runId == Guid.Empty)
        {
            throw new ArgumentException("Program Run id must not be empty.", nameof(runId));
        }
    }

    private void EnsureRunProject (ProgramRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);
        if (run.Project.ProjectFingerprint != projectFingerprint)
        {
            throw new ArgumentException("Program Run must belong to this project store.", nameof(run));
        }
    }
}
