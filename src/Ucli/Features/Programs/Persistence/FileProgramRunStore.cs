using System.Text.Json;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Artifacts;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Features.Programs.Persistence;

/// <summary> Stores Program Run aggregates under one project-local create-only and compare-and-swap boundary. </summary>
internal sealed class FileProgramRunStore : IProgramRunStore
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

        var candidate = CreateRunTerminalArtifactCandidate(current.RunId, terminalRecord);
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

        var candidate = CreateStepTerminalArtifactCandidate(current.RunId, stepIndex, terminalRecord);
        var replacementTemplate = createReplacement(candidate.Reference) ?? throw new InvalidOperationException("Terminal replacement must be created.");
        EnsureStepTerminalMatchesReplacement(terminalRecord, replacementTemplate, stepIndex, candidate.Reference, nameof(createReplacement));
        EnsureReplacementPreservesIdentity(current, replacementTemplate);
        var artifact = await PublishStepTerminalRecordAsync(candidate, terminalRecord, cancellationToken).ConfigureAwait(false);
        var replacement = ApplyStepTerminalArtifactReference(replacementTemplate, stepIndex, candidate.Reference, artifact);
        await WriteWithoutLockAsync(statePath, replacement, cancellationToken).ConfigureAwait(false);
        return new ProgramRunStepTerminalPublicationResult(true, artifact, replacement);
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
        var candidate = CreateRunTerminalArtifactCandidate(current.RunId, terminal);
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
        var candidate = CreateStepTerminalArtifactCandidate(current.RunId, stepIndex, terminal);
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

    private TerminalArtifactCandidate CreateRunTerminalArtifactCandidate (Guid runId, ProgramRunTerminalRecord terminalRecord)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(terminalRecord, IpcJsonSerializerOptions.Default);
        return CreateTerminalArtifactCandidate(runId, null, ProgramTerminalArtifactContract.RunTerminalRecordKind, bytes);
    }

    private TerminalArtifactCandidate CreateStepTerminalArtifactCandidate (Guid runId, int stepIndex, ProgramStepTerminalRecord terminalRecord)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(terminalRecord, IpcJsonSerializerOptions.Default);
        return CreateTerminalArtifactCandidate(runId, stepIndex, ProgramTerminalArtifactContract.StepTerminalRecordKind, bytes);
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
                var read = await JsonSerializer.DeserializeAsync<ProgramRunTerminalRecord>(stream, IpcJsonSerializerOptions.Default, token).ConfigureAwait(false)
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
                var read = await JsonSerializer.DeserializeAsync<ProgramStepTerminalRecord>(stream, IpcJsonSerializerOptions.Default, token).ConfigureAwait(false)
                    ?? throw new InvalidDataException("Program Step terminal artifact is empty.");
                read.Validate();
                if (read.RunId != terminalRecord.RunId || read.DefinitionDigest != terminalRecord.DefinitionDigest
                    || read.StepIndex != terminalRecord.StepIndex
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
            replacementTemplate.Steps, replacementTemplate.ChildExecutionRefs, replacementTemplate.Cancellation, verifiedReference);
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
            steps, replacementTemplate.ChildExecutionRefs, replacementTemplate.Cancellation, replacementTemplate.TerminalRecordRef);
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
            var terminal = await ReadVerifiedImmutableJsonArtifactAsync<ProgramRunTerminalRecord, ProgramRunTerminalRecord>(
                run.TerminalRecordRef,
                ProgramTerminalArtifactContract.RunTerminalRecordKind,
                "Program Run terminal record",
                static value => value.Validate(),
                cancellationToken).ConfigureAwait(false);
            var mismatch = GetRunTerminalAggregateMismatch(terminal, run, run.TerminalRecordRef, definition);
            if (mismatch is not null)
            {
                throw new InvalidDataException($"Program Run terminal record does not describe its durable aggregate: {mismatch}");
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
            var terminal = await ReadVerifiedImmutableJsonArtifactAsync<ProgramStepTerminalRecord, ProgramStepTerminalRecord>(
                terminalReference,
                ProgramTerminalArtifactContract.StepTerminalRecordKind,
                "Program Step terminal record",
                static value => value.Validate(),
                cancellationToken).ConfigureAwait(false);
            if (!StepTerminalMatchesAggregate(terminal, run, step, index, terminalReference))
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

    private static void EnsureReplacementPreservesIdentity (ProgramRunRecord expected, ProgramRunRecord replacement)
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
            EnsureStepReplacementIsAppendOnly(expected.Steps[index], replacement.Steps[index], nameof(replacement));
        }
        EnsureCancellationIsAppendOnly(expected.Cancellation, replacement.Cancellation, nameof(replacement));
    }

    private static void EnsureStepReplacementIsAppendOnly (ProgramRunStepRecord expected, ProgramRunStepRecord replacement, string parameterName)
    {
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
        RequireSameWhenSet(expected.LifecycleExecutionRef, replacement.LifecycleExecutionRef, parameterName);
        RequireSameWhenSet(expected.RequestExecution, replacement.RequestExecution, parameterName);
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

    private static void EnsureCancellationIsAppendOnly (ProgramCancellationRecord expected, ProgramCancellationRecord replacement, string parameterName)
    {
        if ((expected.Requested && expected != replacement)
            || (!expected.Requested && replacement.RequestedAtUtc is not null && !replacement.Requested)
            || (!expected.Requested && replacement.ReasonCode is not null && !replacement.Requested))
        {
            throw new ArgumentException("Program Run cancellation facts are append-only.", parameterName);
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
        if (expected is not null && expected != replacement)
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
            : terminal.StartedAtUtc != run.StartedAtUtc ? "startedAtUtc"
            : terminal.CompletedAtUtc != run.UpdatedAtUtc ? "completedAtUtc"
            : null;
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
            : left.RequestExecution != right.RequestExecution ? "requestExecution"
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
