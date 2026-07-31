using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Artifacts;
using MackySoft.Ucli.Infrastructure.Storage;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;

namespace MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

internal sealed partial class FileLifecycleExecutionStore
{
    /// <summary>
    /// Fixes the first terminal record, publishes and reverifies it, and returns that fixed record
    /// to every later publisher for the same execution.
    /// </summary>
    public ValueTask<LifecycleExecutionTerminalPublicationResult> PublishTerminalAsync (
        LifecycleExecutionTerminalRecord terminalRecord,
        CancellationToken cancellationToken)
    {
        if (terminalRecord is null)
        {
            throw new ArgumentNullException(nameof(terminalRecord));
        }

        return PublishTerminalCoreAsync(
            expectedExecution: null,
            terminalRecord,
            cancellationToken);
    }

    /// <summary>
    /// Publishes a Terminal Record only while the supplied durable start, current reference, and
    /// side-effect right owner remain authoritative.
    /// </summary>
    public ValueTask<LifecycleExecutionTerminalPublicationResult>
        TryPublishTerminalAsync (
            StoredLifecycleExecution expectedExecution,
            LifecycleExecutionTerminalRecord terminalRecord,
            CancellationToken cancellationToken)
    {
        if (expectedExecution is null)
        {
            throw new ArgumentNullException(nameof(expectedExecution));
        }
        if (terminalRecord is null)
        {
            throw new ArgumentNullException(nameof(terminalRecord));
        }
        if (GetLifecycleKind(expectedExecution.CurrentReference)
                != terminalRecord.ExecutionKind
            || expectedExecution.CurrentReference.Id
                != terminalRecord.ExecutionId)
        {
            throw new ArgumentException(
                "Expected Lifecycle Execution does not identify the Terminal Record candidate.",
                nameof(expectedExecution));
        }

        return PublishTerminalCoreAsync(
            expectedExecution,
            terminalRecord,
            cancellationToken);
    }

    private async ValueTask<LifecycleExecutionTerminalPublicationResult>
        PublishTerminalCoreAsync (
            StoredLifecycleExecution? expectedExecution,
            LifecycleExecutionTerminalRecord terminalRecord,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var kind = terminalRecord.ExecutionKind;
        var executionId = terminalRecord.ExecutionId;
        var lockPath = paths.ResolveLockPath(kind, executionId);
        using var executionLock = await FileExclusiveLock.AcquireAsync(
                lockPath,
                LockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var record = await ReadRecordWithoutLockAsync(kind, executionId, cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
        {
            return new LifecycleExecutionTerminalPublicationResult(
                LifecycleExecutionTerminalPublicationOutcome.Missing,
                TerminalReference: null,
                TerminalRecord: null);
        }

        if (record.TerminalReference is not null)
        {
            ValidateTerminalRecord(record.Start, terminalRecord);
            var publishedTerminalRecord = await ReadVerifiedTerminalAsync(
                    kind,
                    executionId,
                    record.TerminalReference.TerminalRecordRef,
                    record.TerminalReference,
                    record.Start,
                    acceptedEndpointRegistrationGenerationId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (publishedTerminalRecord is null)
            {
                return new LifecycleExecutionTerminalPublicationResult(
                    LifecycleExecutionTerminalPublicationOutcome.Conflict,
                    TerminalReference: null,
                    TerminalRecord: null);
            }

            return new LifecycleExecutionTerminalPublicationResult(
                LifecycleExecutionTerminalPublicationOutcome.Reconnected,
                record.TerminalReference,
                publishedTerminalRecord);
        }

        var authoritativeExecution = record.ToStoredExecution();
        if (record.TerminalPublication is null
            && expectedExecution is not null
            && (expectedExecution.Start
                    != authoritativeExecution.Start
                || expectedExecution.CurrentReference
                    != authoritativeExecution.CurrentReference
                || expectedExecution
                        .SideEffectRightOwnerEndpointRegistrationGenerationId
                    != authoritativeExecution
                        .SideEffectRightOwnerEndpointRegistrationGenerationId))
        {
            return new LifecycleExecutionTerminalPublicationResult(
                LifecycleExecutionTerminalPublicationOutcome.Conflict,
                TerminalReference: null,
                TerminalRecord: null,
                AuthoritativeExecution: authoritativeExecution);
        }

        LifecycleExecutionTerminalPublicationIntent intent;
        LifecycleExecutionTerminalRecord fixedTerminalRecord;
        if (record.TerminalPublication is null)
        {
            ValidateTerminalRecord(record.Start, terminalRecord);
            var terminalBytes =
                JsonSerializer.SerializeToUtf8Bytes<LifecycleExecutionTerminalRecord>(
                    terminalRecord,
                    IpcJsonSerializerOptions.Default);
            EnsureFitsRecordSizeLimit(
                terminalBytes.Length,
                "Lifecycle Execution Terminal Record");
            intent = new LifecycleExecutionTerminalPublicationIntent(
                terminalRecord.Host.CurrentEndpointRegistrationGenerationId,
                terminalBytes);
            var publishingReference =
                LifecycleExecutionReferenceFactory.CreateStateProjection(
                    record.Start.LifecycleExecutionRef,
                    ExecutionLifecycle.Recovery,
                    LifecycleExecutionState.Publishing);
            var publishingStart = CopyStart(
                record.Start,
                publishingReference,
                record.Start.Host);
            record = record with
            {
                Start = publishingStart,
                TerminalPublication = intent,
            };
            await WriteRecordWithoutLockAsync(kind, executionId, record, cancellationToken)
                .ConfigureAwait(false);
            fixedTerminalRecord = terminalRecord;
        }
        else
        {
            intent = record.TerminalPublication;
            fixedTerminalRecord = DeserializeTerminalRecord(
                intent.TerminalRecordBytes,
                "Lifecycle Execution terminal publication intent");
            ValidateTerminalRecord(
                record.Start,
                fixedTerminalRecord,
                intent.AcceptedEndpointRegistrationGenerationId);
            ValidateTerminalRecord(record.Start, terminalRecord);
        }

        return await TryCompleteFixedTerminalPublicationWithoutLockAsync(
                kind,
                executionId,
                record,
                intent,
                fixedTerminalRecord,
                LifecycleExecutionTerminalPublicationOutcome.Published,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Completes a terminal publication whose exact record bytes were durably fixed before an
    /// earlier publisher stopped.
    /// </summary>
    public async ValueTask<LifecycleExecutionTerminalPublicationResult>
        TryRecoverTerminalPublicationAsync (
            LifecycleExecutionKind kind,
            Guid executionId,
            CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var lockPath = paths.ResolveLockPath(kind, executionId);
        using var executionLock = await FileExclusiveLock.AcquireAsync(
                lockPath,
                LockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var record = await ReadRecordWithoutLockAsync(kind, executionId, cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
        {
            return new LifecycleExecutionTerminalPublicationResult(
                LifecycleExecutionTerminalPublicationOutcome.Missing,
                TerminalReference: null,
                TerminalRecord: null);
        }

        if (record.TerminalReference is not null)
        {
            var verifiedTerminalRecord = await ReadVerifiedTerminalAsync(
                    kind,
                    executionId,
                    record.TerminalReference.TerminalRecordRef,
                    record.TerminalReference,
                    record.Start,
                    acceptedEndpointRegistrationGenerationId: null,
                    cancellationToken)
                .ConfigureAwait(false);
            if (verifiedTerminalRecord is null)
            {
                return new LifecycleExecutionTerminalPublicationResult(
                    LifecycleExecutionTerminalPublicationOutcome.Conflict,
                    TerminalReference: null,
                    TerminalRecord: null);
            }

            return new LifecycleExecutionTerminalPublicationResult(
                LifecycleExecutionTerminalPublicationOutcome.Reconnected,
                record.TerminalReference,
                verifiedTerminalRecord);
        }

        if (record.TerminalPublication is null)
        {
            return new LifecycleExecutionTerminalPublicationResult(
                LifecycleExecutionTerminalPublicationOutcome.NotPublishing,
                TerminalReference: null,
                TerminalRecord: null);
        }

        var intent = record.TerminalPublication;
        var fixedTerminalRecord = DeserializeTerminalRecord(
            intent.TerminalRecordBytes,
            "Lifecycle Execution terminal publication intent");
        ValidateTerminalRecord(
            record.Start,
            fixedTerminalRecord,
            intent.AcceptedEndpointRegistrationGenerationId);
        return await TryCompleteFixedTerminalPublicationWithoutLockAsync(
                kind,
                executionId,
                record,
                intent,
                fixedTerminalRecord,
                LifecycleExecutionTerminalPublicationOutcome.Published,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<LifecycleExecutionTerminalPublicationResult>
        TryCompleteFixedTerminalPublicationWithoutLockAsync (
            LifecycleExecutionKind kind,
            Guid executionId,
            LifecycleExecutionStoreRecord record,
            LifecycleExecutionTerminalPublicationIntent intent,
            LifecycleExecutionTerminalRecord fixedTerminalRecord,
            LifecycleExecutionTerminalPublicationOutcome successOutcome,
            CancellationToken cancellationToken)
    {
        try
        {
            await VerifyPrecedingArtifactsAsync(
                    kind,
                    executionId,
                    fixedTerminalRecord,
                    cancellationToken)
                .ConfigureAwait(false);
            return await CompleteTerminalPublicationWithoutLockAsync(
                    kind,
                    executionId,
                    record,
                    intent,
                    fixedTerminalRecord,
                    successOutcome,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new LifecycleExecutionTerminalPublicationResult(
                LifecycleExecutionTerminalPublicationOutcome.PublicationFailed,
                TerminalReference: null,
                TerminalRecord: fixedTerminalRecord,
                ReconnectableReference: record.Start.LifecycleExecutionRef,
                Failure: exception);
        }
    }

    private async ValueTask<LifecycleExecutionTerminalPublicationResult>
        CompleteTerminalPublicationWithoutLockAsync (
            LifecycleExecutionKind kind,
            Guid executionId,
            LifecycleExecutionStoreRecord record,
            LifecycleExecutionTerminalPublicationIntent intent,
            LifecycleExecutionTerminalRecord fixedTerminalRecord,
            LifecycleExecutionTerminalPublicationOutcome successOutcome,
            CancellationToken cancellationToken)
    {
        var terminalArtifactReference = await PublishOrRecoverTerminalArtifactAsync(
                kind,
                executionId,
                intent,
                record.Start,
                cancellationToken)
            .ConfigureAwait(false);
        var terminalReference = LifecycleExecutionReferenceFactory.CreateTerminal(
            record.Start.LifecycleExecutionRef,
            fixedTerminalRecord.TerminalReason,
            terminalArtifactReference);
        var terminalRecordState = record with
        {
            TerminalReference = terminalReference,
            TerminalPublication = null,
        };
        var terminalStateWasWritten = false;
        try
        {
            await WriteRecordWithoutLockAsync(
                    kind,
                    executionId,
                    terminalRecordState,
                    cancellationToken)
                .ConfigureAwait(false);
            terminalStateWasWritten = true;

            var reloaded = await ReadRecordWithoutLockAsync(
                    kind,
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (reloaded?.TerminalReference != terminalReference)
            {
                throw new IOException(
                    $"Lifecycle Execution terminal reference did not survive durable re-read for kind '{TextVocabulary.GetText(kind)}' and id '{executionId:D}'.");
            }
        }
        catch (Exception publicationException) when (
            publicationException is not OperationCanceledException)
        {
            if (terminalStateWasWritten)
            {
                try
                {
                    await WriteRecordWithoutLockAsync(
                            kind,
                            executionId,
                            record,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception restorationException)
                {
                    throw new IOException(
                        $"Lifecycle Execution terminal publication failed and its publishing state could not be restored for kind '{TextVocabulary.GetText(kind)}' and id '{executionId:D}'.",
                        new AggregateException(
                            publicationException,
                            restorationException));
                }
            }

            throw;
        }

        return new LifecycleExecutionTerminalPublicationResult(
            successOutcome,
            terminalReference,
            fixedTerminalRecord);
    }

    private async ValueTask<PathArtifactRef> PublishOrRecoverTerminalArtifactAsync (
        LifecycleExecutionKind kind,
        Guid executionId,
        LifecycleExecutionTerminalPublicationIntent intent,
        LifecycleExecutionStartBinding start,
        CancellationToken cancellationToken)
    {
        var destination = paths.ResolveTerminalRecordPath(kind, executionId);
        if (!destination.Target.TryGetParent(out var destinationDirectory))
        {
            throw new InvalidOperationException(
                $"Lifecycle Execution terminal record directory could not be resolved: {destination.Target.Value}");
        }

        FileSystemAccessBoundary.EnsureSecureDirectory(destinationDirectory);
        PathArtifactRef artifactReference;
        if (FileUtilities.FileExists(destination.Target))
        {
            using var session = ImmutableArtifactFileReadBoundary.OpenSession(
                destination,
                "Lifecycle Execution terminal record",
                cancellationToken);
            var publicationTimeUtc = DateTimeOffset.UtcNow;
            var measurement = await session.MeasureAsync(cancellationToken)
                .ConfigureAwait(false);
            artifactReference = new PathArtifactRef(
                LifecycleExecutionArtifactContract.TerminalRecordKind,
                LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                paths.CreateTerminalRecordArtifactPath(kind, executionId),
                measurement.Digest,
                measurement.SizeBytes,
                publicationTimeUtc);
        }
        else
        {
            var publisher = new ImmutableArtifactFilePublisher(
                () => DateTimeOffset.UtcNow);
            artifactReference = await publisher.PublishAsync(
                    LifecycleExecutionArtifactContract.TerminalRecordKind,
                    LifecycleExecutionArtifactContract.TerminalRecordMediaType,
                    destination,
                    (stream, token) => WriteTerminalBytesAsync(
                        stream,
                        intent.TerminalRecordBytes,
                        token),
                    (stream, token) => ValidateTerminalStreamAsync(
                        stream,
                        intent.TerminalRecordBytes,
                        start,
                        intent.AcceptedEndpointRegistrationGenerationId,
                        token),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await ImmutableArtifactFileVerifier.VerifyAsync(
                paths.StorageRoot,
                artifactReference,
                cancellationToken)
            .ConfigureAwait(false);
        if (!await HasExactVerifiedTerminalAsync(
                kind,
                executionId,
                artifactReference,
                terminalReference: null,
                intent.TerminalRecordBytes,
                start,
                intent.AcceptedEndpointRegistrationGenerationId,
                cancellationToken)
            .ConfigureAwait(false))
        {
            throw new IOException(
                $"Published Lifecycle Execution terminal record did not match its durable publication intent for kind '{TextVocabulary.GetText(kind)}' and id '{executionId:D}'.");
        }

        return artifactReference;
    }

    private async ValueTask<bool> HasExactVerifiedTerminalAsync (
        LifecycleExecutionKind kind,
        Guid executionId,
        ArtifactRef artifactReference,
        TerminalExecutionRef? terminalReference,
        byte[] expectedBytes,
        LifecycleExecutionStartBinding start,
        Guid acceptedEndpointRegistrationGenerationId,
        CancellationToken cancellationToken)
    {
        var persistedRecord = await ReadVerifiedTerminalAsync(
                kind,
                executionId,
                artifactReference,
                terminalReference,
                start,
                acceptedEndpointRegistrationGenerationId,
                cancellationToken)
            .ConfigureAwait(false);
        if (persistedRecord is null)
        {
            return false;
        }

        var reserialized = JsonSerializer.SerializeToUtf8Bytes<LifecycleExecutionTerminalRecord>(
            persistedRecord,
            IpcJsonSerializerOptions.Default);
        return reserialized.AsSpan().SequenceEqual(expectedBytes);
    }

    private async ValueTask<LifecycleExecutionTerminalRecord?> ReadVerifiedTerminalAsync (
        LifecycleExecutionKind kind,
        Guid executionId,
        ArtifactRef artifactReference,
        TerminalExecutionRef? terminalReference,
        LifecycleExecutionStartBinding start,
        Guid? acceptedEndpointRegistrationGenerationId,
        CancellationToken cancellationToken)
    {
        if (!HasTerminalRecordArtifactContract(artifactReference)
            || !paths.HasExpectedTerminalRecordArtifactPath(
                kind,
                executionId,
                artifactReference))
        {
            return null;
        }

        try
        {
            var persistedBytes = await ReadVerifiedTerminalBytesAsync(
                    kind,
                    executionId,
                    artifactReference,
                    cancellationToken)
                .ConfigureAwait(false);
            if (persistedBytes is null)
            {
                return null;
            }

            var persistedRecord = JsonSerializer.Deserialize<LifecycleExecutionTerminalRecord>(
                persistedBytes,
                IpcJsonSerializerOptions.Default);
            if (persistedRecord is null)
            {
                return null;
            }

            ValidateTerminalRecord(
                start,
                persistedRecord,
                acceptedEndpointRegistrationGenerationId);
            if (terminalReference is not null
                && !TerminalReferenceStateMatchesReason(
                    terminalReference,
                    persistedRecord.TerminalReason))
            {
                return null;
            }

            await VerifyPrecedingArtifactsAsync(
                    kind,
                    executionId,
                    persistedRecord,
                    cancellationToken)
                .ConfigureAwait(false);
            var reserialized = JsonSerializer.SerializeToUtf8Bytes<LifecycleExecutionTerminalRecord>(
                persistedRecord,
                IpcJsonSerializerOptions.Default);
            return reserialized.AsSpan().SequenceEqual(persistedBytes)
                ? persistedRecord
                : null;
        }
        catch (Exception exception) when (
            exception is IOException
                or JsonException
                or ArgumentException)
        {
            return null;
        }
    }

    private async ValueTask<byte[]?> ReadVerifiedTerminalBytesAsync (
        LifecycleExecutionKind kind,
        Guid executionId,
        ArtifactRef artifactReference,
        CancellationToken cancellationToken)
    {
        using var session = ImmutableArtifactFileReadBoundary.OpenSession(
            paths.ResolveTerminalRecordPath(kind, executionId),
            "Lifecycle Execution terminal record",
            cancellationToken);
        var before = await session.MeasureAsync(cancellationToken)
            .ConfigureAwait(false);
        if (before.SizeBytes != artifactReference.SizeBytes
            || before.Digest != artifactReference.Digest
            || before.SizeBytes > MaximumRecordBytes)
        {
            return null;
        }

        byte[]? persistedBytes = null;
        await session.ValidateAsync(
                async (stream, token) =>
                {
                    using var contents = new MemoryStream((int)before.SizeBytes);
                    await stream.CopyToAsync(contents, 81920, token)
                        .ConfigureAwait(false);
                    persistedBytes = contents.ToArray();
                },
                cancellationToken)
            .ConfigureAwait(false);
        var after = await session.MeasureAsync(cancellationToken)
            .ConfigureAwait(false);
        return before == after
            && persistedBytes is not null
            && (ulong)persistedBytes.LongLength == after.SizeBytes
            && after.SizeBytes == artifactReference.SizeBytes
            && after.Digest == artifactReference.Digest
                ? persistedBytes
                : null;
    }

    private async ValueTask VerifyPrecedingArtifactsAsync (
        LifecycleExecutionKind kind,
        Guid executionId,
        LifecycleExecutionTerminalRecord terminalRecord,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < terminalRecord.ArtifactRefs.Count; index++)
        {
            var artifactReference = terminalRecord.ArtifactRefs[index];
            if (paths.HasExpectedTerminalRecordArtifactPath(
                    kind,
                    executionId,
                    artifactReference))
            {
                throw new IOException(
                    "Lifecycle Execution Terminal Record cannot reference its own canonical artifact path.");
            }

            switch (artifactReference)
            {
                case PathArtifactRef:
                case PathAndUriArtifactRef:
                    await ImmutableArtifactFileVerifier.VerifyAsync(
                            paths.StorageRoot,
                            artifactReference,
                            cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case UriArtifactRef:
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported artifact reference type '{artifactReference.GetType().FullName}'.");
            }
        }
    }

    private static bool TerminalReferenceStateMatchesReason (
        TerminalExecutionRef terminalReference,
        LifecycleExecutionTerminalReason terminalReason)
    {
        var expectedState = terminalReason
            == LifecycleExecutionTerminalReason.Completed
                ? LifecycleExecutionState.Completed
                : LifecycleExecutionState.Failed;
        return string.Equals(
            terminalReference.State.Value,
            TextVocabulary.GetText(expectedState),
            StringComparison.Ordinal);
    }

    private static async ValueTask WriteTerminalBytesAsync (
        Stream destination,
        byte[] terminalBytes,
        CancellationToken cancellationToken)
    {
        await destination.WriteAsync(
                terminalBytes.AsMemory(),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async ValueTask ValidateTerminalStreamAsync (
        Stream stream,
        byte[] expectedBytes,
        LifecycleExecutionStartBinding start,
        Guid acceptedEndpointRegistrationGenerationId,
        CancellationToken cancellationToken)
    {
        stream.Position = 0;
        using var contents = new MemoryStream();
        await stream.CopyToAsync(contents, 81920, cancellationToken).ConfigureAwait(false);
        var actualBytes = contents.ToArray();
        if (!actualBytes.AsSpan().SequenceEqual(expectedBytes))
        {
            throw new IOException("Lifecycle Execution terminal record bytes changed before publication.");
        }

        var record = JsonSerializer.Deserialize<LifecycleExecutionTerminalRecord>(
            actualBytes,
            IpcJsonSerializerOptions.Default);
        if (record is null)
        {
            throw new IOException("Lifecycle Execution terminal record could not be deserialized.");
        }

        ValidateTerminalRecord(
            start,
            record,
            acceptedEndpointRegistrationGenerationId);
    }

    private static bool IsPublishingReference (ExecutionRef executionReference)
    {
        return executionReference.Lifecycle == ExecutionLifecycle.Recovery
            && string.Equals(
                executionReference.State.Value,
                TextVocabulary.GetText(LifecycleExecutionState.Publishing),
                StringComparison.Ordinal);
    }

    private static void ValidateTerminalRecord (
        LifecycleExecutionStartBinding start,
        LifecycleExecutionTerminalRecord terminalRecord,
        Guid? acceptedEndpointRegistrationGenerationId = null)
    {
        var expectedEndpointRegistrationGenerationId =
            acceptedEndpointRegistrationGenerationId
            ?? start.Host.CurrentEndpointRegistrationGenerationId;
        if (expectedEndpointRegistrationGenerationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Lifecycle Execution accepted endpoint registration generation must not be empty.",
                nameof(acceptedEndpointRegistrationGenerationId));
        }

        if (terminalRecord.ExecutionId != start.LifecycleExecutionRef.Id
            || terminalRecord.DefinitionDigest
                != start.LifecycleExecutionRef.DefinitionDigest
            || terminalRecord.Project != start.Project
            || terminalRecord.Host.Process != start.Host.Process
            || terminalRecord.Host.EditorInstanceId != start.Host.EditorInstanceId
            || terminalRecord.Host.FirstEndpointRegistrationGenerationId
                != start.Host.FirstEndpointRegistrationGenerationId
            || terminalRecord.Host.CurrentEndpointRegistrationGenerationId
                != expectedEndpointRegistrationGenerationId
            || terminalRecord.StartedGeneration != start.StartedGeneration
            || terminalRecord.DeadlineUtc != start.DeadlineUtc
            || terminalRecord.StartedAtUtc != start.StartedAtUtc)
        {
            throw new ArgumentException(
                "Lifecycle Execution terminal record does not match its durable start binding.",
                nameof(terminalRecord));
        }

        if (terminalRecord.TerminalGeneration is not null
            && !LifecycleExecutionGenerationRules.IsMonotonicSuccessor(
                start.StartedGeneration,
                terminalRecord.TerminalGeneration))
        {
            throw new ArgumentException(
                "Lifecycle Execution terminal generation must not regress from its start generation.",
                nameof(terminalRecord));
        }
    }

    private static LifecycleExecutionTerminalRecord DeserializeTerminalRecord (
        ReadOnlySpan<byte> bytes,
        string source)
    {
        try
        {
            return JsonSerializer.Deserialize<LifecycleExecutionTerminalRecord>(
                    bytes,
                    IpcJsonSerializerOptions.Default)
                ?? throw new IOException($"{source} is empty.");
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException)
        {
            throw new IOException($"{source} is invalid.", exception);
        }
    }

}
