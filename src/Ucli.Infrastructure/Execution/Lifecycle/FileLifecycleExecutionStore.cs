using System.Text;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Infrastructure.Storage;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;

namespace MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

/// <summary>
/// Persists common Lifecycle Execution identity, reconnection, and terminal-publication state.
/// </summary>
internal sealed partial class FileLifecycleExecutionStore
{
    private const int MaximumRecordBytes = 4 * 1024 * 1024;

    private static readonly TimeSpan LockAcquireTimeout = TimeSpan.FromSeconds(5);

    private readonly LifecycleExecutionStorePaths paths;
    private readonly ProjectFingerprint projectFingerprint;

    public FileLifecycleExecutionStore (
        AbsolutePath storageRoot,
        ProjectFingerprint projectFingerprint)
    {
        paths = new LifecycleExecutionStorePaths(
            storageRoot ?? throw new ArgumentNullException(nameof(storageRoot)),
            projectFingerprint ?? throw new ArgumentNullException(nameof(projectFingerprint)));
        this.projectFingerprint = projectFingerprint;
    }

    public static FileLifecycleExecutionStore CreateForProject (
        AbsolutePath projectPath,
        ProjectFingerprint projectFingerprint)
    {
        if (projectPath is null)
        {
            throw new ArgumentNullException(nameof(projectPath));
        }

        return new FileLifecycleExecutionStore(
            UcliStoragePathResolver.ResolveStorageRoot(projectPath),
            projectFingerprint);
    }

    public async ValueTask<LifecycleExecutionStartResult> StartAsync (
        LifecycleExecutionDefinition definition,
        Guid executionId,
        Sha256Digest requestedDefinitionDigest,
        UnityProjectIdentity project,
        LifecycleExecutionHostRegistration host,
        UnityEditorGenerationSnapshot startedGeneration,
        DateTimeOffset deadlineUtc,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
    {
        if (definition is null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (executionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Lifecycle Execution identifier must not be empty.",
                nameof(executionId));
        }

        if (requestedDefinitionDigest is null)
        {
            throw new ArgumentNullException(nameof(requestedDefinitionDigest));
        }

        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (project.ProjectFingerprint != projectFingerprint)
        {
            throw new ArgumentException(
                "Lifecycle Execution project does not belong to this store.",
                nameof(project));
        }

        if (host is null)
        {
            throw new ArgumentNullException(nameof(host));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var lockPath = paths.ResolveLockPath(definition.Kind, executionId);
        using var executionLock = await FileExclusiveLock.AcquireAsync(
                lockPath,
                LockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var existing = await ReadRecordWithoutLockAsync(
                definition.Kind,
                executionId,
                cancellationToken)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return ClassifyExistingStart(
                existing,
                requestedDefinitionDigest,
                project,
                host);
        }

        var expectedDefinitionDigest =
            LifecycleExecutionDefinitionDigest.Calculate(definition);
        if (requestedDefinitionDigest != expectedDefinitionDigest)
        {
            return new LifecycleExecutionStartResult(
                LifecycleExecutionStartOutcome.InvalidDefinition,
                Binding: null);
        }
        if (host.FirstEndpointRegistrationGenerationId
            != host.CurrentEndpointRegistrationGenerationId)
        {
            throw new ArgumentException(
                "A new Lifecycle Execution must start from its first endpoint registration generation.",
                nameof(host));
        }

        var statusLocator = paths.CreateStatusLocator(definition.Kind, executionId);
        var registeredReference = LifecycleExecutionReferenceFactory.CreateRegistered(
            definition,
            executionId,
            statusLocator);
        var candidate = new LifecycleExecutionStartBinding(
            registeredReference,
            project,
            host,
            startedGeneration,
            deadlineUtc,
            startedAtUtc);
        var record = new LifecycleExecutionStoreRecord(
            LifecycleExecutionStoreRecord.CurrentSchemaVersion,
            candidate,
            terminalReference: null,
            terminalPublication: null,
            sideEffectRightOwnerEndpointRegistrationGenerationId: null,
            acceptedEndpointRegistrationGenerationIds:
                new[] { host.FirstEndpointRegistrationGenerationId });
        await WriteRecordWithoutLockAsync(definition.Kind, executionId, record, cancellationToken)
            .ConfigureAwait(false);
        return new LifecycleExecutionStartResult(
            LifecycleExecutionStartOutcome.Registered,
            candidate);
    }

    public async ValueTask<StoredLifecycleExecution?> ReadAsync (
        LifecycleExecutionKind kind,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var record = await ReadRecordWithoutLockAsync(kind, executionId, cancellationToken)
            .ConfigureAwait(false);
        return record?.ToStoredExecution();
    }

    public IReadOnlyList<LifecycleExecutionStoreEntry> ListEntries (
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entries = new List<LifecycleExecutionStoreEntry>();
        foreach (var kind in GetKinds())
        {
            var kindDirectory = paths.ResolveKindDirectory(kind);
            if (!DirectoryUtilities.Exists(kindDirectory))
            {
                continue;
            }

            foreach (var segment in DirectoryUtilities.EnumerateDirectoryNames(kindDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!StoragePathSegmentCodec.TryDecodeNonEmptyGuid(segment, out var executionId))
                {
                    continue;
                }

                entries.Add(new LifecycleExecutionStoreEntry(kind, executionId));
            }
        }

        return entries;
    }

    public async ValueTask<LifecycleExecutionReferenceUpdateOutcome> TryUpdateReferenceAsync (
        ExecutionRef expectedReference,
        ExecutionRef nextReference,
        CancellationToken cancellationToken)
    {
        if (expectedReference is null)
        {
            throw new ArgumentNullException(nameof(expectedReference));
        }
        if (IsRegisteredReference(expectedReference))
        {
            throw new ArgumentException(
                "The registered state can advance only through side-effect right acquisition.",
                nameof(expectedReference));
        }

        var result = await TryUpdateReferenceCoreAsync(
                expectedReference,
                nextReference,
                sideEffectRightClaimantEndpointRegistrationGenerationId: null,
                cancellationToken)
            .ConfigureAwait(false);
        return result.Outcome;
    }

    /// <summary>
    /// Converges one open Lifecycle Execution onto the common recovering projection.
    /// The owning action decides when recovery is required and retains its recovery workflow.
    /// </summary>
    public async ValueTask<LifecycleExecutionRecoveryTransitionOutcome>
        TryEnterRecoveryAsync (
            LifecycleExecutionKind kind,
            Guid executionId,
            CancellationToken cancellationToken)
    {
        if (executionId == Guid.Empty)
        {
            throw new ArgumentException(
                "Lifecycle Execution identifier must not be empty.",
                nameof(executionId));
        }

        while (true)
        {
            var stored = await ReadAsync(kind, executionId, cancellationToken)
                .ConfigureAwait(false);
            if (stored is null)
            {
                return LifecycleExecutionRecoveryTransitionOutcome.Missing;
            }
            if (stored.IsTerminal || stored.IsPublishing)
            {
                return LifecycleExecutionRecoveryTransitionOutcome
                    .TerminalOrPublishing;
            }
            if (stored.CurrentReference.Lifecycle
                    == ExecutionLifecycle.Active
                && string.Equals(
                    stored.CurrentReference.State.Value,
                    TextVocabulary.GetText(
                        LifecycleExecutionState.Registered),
                    StringComparison.Ordinal))
            {
                return LifecycleExecutionRecoveryTransitionOutcome
                    .SideEffectAdmissionRequired;
            }
            if (stored.CurrentReference.Lifecycle
                == ExecutionLifecycle.Recovery)
            {
                return LifecycleExecutionRecoveryTransitionOutcome
                    .AlreadyRecovering;
            }

            var recoveringReference =
                LifecycleExecutionReferenceFactory.CreateStateProjection(
                    stored.CurrentReference,
                    ExecutionLifecycle.Recovery,
                    LifecycleExecutionState.Recovering);
            var outcome = await TryUpdateReferenceAsync(
                    stored.CurrentReference,
                    recoveringReference,
                    cancellationToken)
                .ConfigureAwait(false);
            switch (outcome)
            {
                case LifecycleExecutionReferenceUpdateOutcome.Updated:
                    return LifecycleExecutionRecoveryTransitionOutcome.Entered;
                case LifecycleExecutionReferenceUpdateOutcome.AlreadyTerminal:
                    return LifecycleExecutionRecoveryTransitionOutcome
                        .TerminalOrPublishing;
                case LifecycleExecutionReferenceUpdateOutcome.Missing:
                    return LifecycleExecutionRecoveryTransitionOutcome.Missing;
                case LifecycleExecutionReferenceUpdateOutcome.Conflict:
                    continue;
                default:
                    throw new InvalidOperationException(
                        $"Lifecycle Execution recovery transition could not classify reference update outcome '{outcome}'.");
            }
        }
    }

    /// <summary>
    /// Atomically acquires the durable right to issue one action-specific side effect.
    /// </summary>
    /// <param name="expectedReference">
    /// The exact durable reference from which the action permits its side effect.
    /// </param>
    /// <param name="nextReference">
    /// The exact action-owned reference that proves the right was acquired.
    /// </param>
    /// <param name="cancellationToken"> Cancels durable right acquisition. </param>
    /// <returns>
    /// The acquisition outcome and the execution that was authoritative while the
    /// compare-and-swap operation was resolved.
    /// </returns>
    public async ValueTask<LifecycleExecutionSideEffectRightResult>
        TryAcquireSideEffectRightAsync (
            ExecutionRef expectedReference,
            ExecutionRef nextReference,
            Guid claimantEndpointRegistrationGenerationId,
            CancellationToken cancellationToken)
    {
        if (expectedReference is null)
        {
            throw new ArgumentNullException(nameof(expectedReference));
        }
        if (!IsRegisteredReference(expectedReference))
        {
            throw new ArgumentException(
                "A side-effect right can be acquired only from the registered state.",
                nameof(expectedReference));
        }
        if (nextReference is null)
        {
            throw new ArgumentNullException(nameof(nextReference));
        }
        if (nextReference.Lifecycle != ExecutionLifecycle.Active
            || IsRegisteredReference(nextReference))
        {
            throw new ArgumentException(
                "A side-effect right must advance to an action-owned active state.",
                nameof(nextReference));
        }
        if (claimantEndpointRegistrationGenerationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Side-effect right claimant endpoint registration generation must not be empty.",
                nameof(claimantEndpointRegistrationGenerationId));
        }

        var result = await TryUpdateReferenceCoreAsync(
                expectedReference,
                nextReference,
                claimantEndpointRegistrationGenerationId,
                cancellationToken)
            .ConfigureAwait(false);
        return ClassifySideEffectRightResult(result);
    }

    /// <summary>
    /// Transfers an already acquired side-effect right to a proven successor endpoint
    /// when the prior owner disappeared before persisting its admission marker.
    /// </summary>
    public async ValueTask<LifecycleExecutionSideEffectRightResult>
        TryTakeOverSideEffectRightAsync (
            StoredLifecycleExecution expectedExecution,
            Guid claimantEndpointRegistrationGenerationId,
            CancellationToken cancellationToken)
    {
        if (expectedExecution is null)
        {
            throw new ArgumentNullException(nameof(expectedExecution));
        }
        if (expectedExecution.CurrentReference.Lifecycle
            != ExecutionLifecycle.Active)
        {
            throw new ArgumentException(
                "A side-effect right can be taken over only from an active action state.",
                nameof(expectedExecution));
        }
        if (claimantEndpointRegistrationGenerationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Side-effect right claimant endpoint registration generation must not be empty.",
                nameof(claimantEndpointRegistrationGenerationId));
        }

        var kind = GetLifecycleKind(expectedExecution.CurrentReference);
        var executionId = expectedExecution.CurrentReference.Id;
        var lockPath = paths.ResolveLockPath(kind, executionId);
        using var executionLock = await FileExclusiveLock.AcquireAsync(
                lockPath,
                LockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var record = await ReadRecordWithoutLockAsync(
                kind,
                executionId,
                cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
        {
            return new LifecycleExecutionSideEffectRightResult(
                LifecycleExecutionSideEffectRightOutcome.Missing,
                AuthoritativeExecution: null);
        }

        var authoritativeExecution = record.ToStoredExecution();
        if (authoritativeExecution.IsTerminal
            || authoritativeExecution.IsPublishing)
        {
            return new LifecycleExecutionSideEffectRightResult(
                LifecycleExecutionSideEffectRightOutcome
                    .TerminalOrPublishing,
                authoritativeExecution);
        }

        var expectedOwner =
            expectedExecution
                .SideEffectRightOwnerEndpointRegistrationGenerationId;
        var authoritativeOwner =
            authoritativeExecution
                .SideEffectRightOwnerEndpointRegistrationGenerationId;
        var expectedEndpoint =
            expectedExecution.Start.Host
                .CurrentEndpointRegistrationGenerationId;
        var authoritativeEndpoint =
            authoritativeExecution.Start.Host
                .CurrentEndpointRegistrationGenerationId;
        if (record.Start.LifecycleExecutionRef
                != expectedExecution.CurrentReference
            || authoritativeOwner != expectedOwner
            || expectedEndpoint
                != claimantEndpointRegistrationGenerationId
            || authoritativeEndpoint
                != claimantEndpointRegistrationGenerationId)
        {
            return new LifecycleExecutionSideEffectRightResult(
                LifecycleExecutionSideEffectRightOutcome.Contended,
                authoritativeExecution);
        }
        if (!authoritativeOwner.HasValue)
        {
            throw new InvalidOperationException(
                "An active Lifecycle Execution side-effect right has no durable endpoint owner.");
        }
        if (authoritativeOwner.Value == authoritativeEndpoint)
        {
            return new LifecycleExecutionSideEffectRightResult(
                LifecycleExecutionSideEffectRightOutcome.Contended,
                authoritativeExecution);
        }

        var updatedRecord = record with
        {
            SideEffectRightOwnerEndpointRegistrationGenerationId =
                claimantEndpointRegistrationGenerationId,
        };
        await WriteRecordWithoutLockAsync(
                kind,
                executionId,
                updatedRecord,
                cancellationToken)
            .ConfigureAwait(false);
        return new LifecycleExecutionSideEffectRightResult(
            LifecycleExecutionSideEffectRightOutcome.Acquired,
            updatedRecord.ToStoredExecution());
    }

    private static LifecycleExecutionSideEffectRightResult
        ClassifySideEffectRightResult ((
            LifecycleExecutionReferenceUpdateOutcome Outcome,
            StoredLifecycleExecution? AuthoritativeExecution) result)
    {
        var authoritativeExecution = result.AuthoritativeExecution;
        var outcome = result.Outcome switch
        {
            LifecycleExecutionReferenceUpdateOutcome.Updated =>
                LifecycleExecutionSideEffectRightOutcome.Acquired,
            LifecycleExecutionReferenceUpdateOutcome.Missing =>
                LifecycleExecutionSideEffectRightOutcome.Missing,
            _ when authoritativeExecution is not null
                && (authoritativeExecution.IsTerminal
                    || authoritativeExecution.IsPublishing) =>
                LifecycleExecutionSideEffectRightOutcome.TerminalOrPublishing,
            LifecycleExecutionReferenceUpdateOutcome.Conflict =>
                LifecycleExecutionSideEffectRightOutcome.Contended,
            _ => throw new InvalidOperationException(
                $"Lifecycle Execution side-effect right could not classify reference update outcome '{result.Outcome}'."),
        };
        return new LifecycleExecutionSideEffectRightResult(
            outcome,
            authoritativeExecution);
    }

    public async ValueTask<LifecycleExecutionEndpointAdvanceOutcome> TryAdvanceEndpointRegistrationAsync (
        LifecycleExecutionKind kind,
        Guid executionId,
        UnityProjectIdentity currentProject,
        ProcessIdentity currentProcess,
        Guid currentEditorInstanceId,
        Guid successorEndpointRegistrationGenerationId,
        DaemonLifecycleRecoveryLease? recoveryLease,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (currentProject is null)
        {
            throw new ArgumentNullException(nameof(currentProject));
        }

        if (currentProcess is null)
        {
            throw new ArgumentNullException(nameof(currentProcess));
        }

        if (currentEditorInstanceId == Guid.Empty)
        {
            throw new ArgumentException(
                "Editor instance identifier must not be empty.",
                nameof(currentEditorInstanceId));
        }

        if (successorEndpointRegistrationGenerationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Successor endpoint registration generation must not be empty.",
                nameof(successorEndpointRegistrationGenerationId));
        }

        if (nowUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Current time must use the UTC offset.", nameof(nowUtc));
        }

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
            return LifecycleExecutionEndpointAdvanceOutcome.Missing;
        }

        if (record.TerminalReference is not null)
        {
            return LifecycleExecutionEndpointAdvanceOutcome.AlreadyTerminal;
        }

        if (record.TerminalPublication is not null)
        {
            return LifecycleExecutionEndpointAdvanceOutcome.TerminalPublicationFixed;
        }

        if (record.Start.Project != currentProject)
        {
            return LifecycleExecutionEndpointAdvanceOutcome.ProjectMismatch;
        }

        var registeredHost = record.Start.Host;
        if (registeredHost.Process != currentProcess
            || registeredHost.EditorInstanceId != currentEditorInstanceId)
        {
            return LifecycleExecutionEndpointAdvanceOutcome.HostMismatch;
        }

        if (registeredHost.CurrentEndpointRegistrationGenerationId
            == successorEndpointRegistrationGenerationId)
        {
            return LifecycleExecutionEndpointAdvanceOutcome.AlreadyCurrent;
        }

        if (record.AcceptedEndpointRegistrationGenerationIds.Contains(
                successorEndpointRegistrationGenerationId))
        {
            return LifecycleExecutionEndpointAdvanceOutcome.GenerationMismatch;
        }

        if (recoveryLease is null
            || recoveryLease.SessionGenerationId
                != registeredHost.CurrentEndpointRegistrationGenerationId)
        {
            return LifecycleExecutionEndpointAdvanceOutcome.GenerationMismatch;
        }

        if (recoveryLease.ExpiresAtUtc <= nowUtc)
        {
            return LifecycleExecutionEndpointAdvanceOutcome.RecoveryLeaseExpired;
        }

        var successorHost = new LifecycleExecutionHostRegistration(
            registeredHost.Process,
            registeredHost.EditorInstanceId,
            registeredHost.FirstEndpointRegistrationGenerationId,
            successorEndpointRegistrationGenerationId);
        var updatedStart = CopyStart(
            record.Start,
            record.Start.LifecycleExecutionRef,
            successorHost);
        var acceptedEndpointRegistrationGenerationIds =
            new Guid[record.AcceptedEndpointRegistrationGenerationIds.Count + 1];
        for (var index = 0;
            index < record.AcceptedEndpointRegistrationGenerationIds.Count;
            index++)
        {
            acceptedEndpointRegistrationGenerationIds[index] =
                record.AcceptedEndpointRegistrationGenerationIds[index];
        }
        acceptedEndpointRegistrationGenerationIds[^1] =
            successorEndpointRegistrationGenerationId;
        await WriteRecordWithoutLockAsync(
                kind,
                executionId,
                new LifecycleExecutionStoreRecord(
                    LifecycleExecutionStoreRecord.CurrentSchemaVersion,
                    updatedStart,
                    record.TerminalReference,
                    record.TerminalPublication,
                    record
                        .SideEffectRightOwnerEndpointRegistrationGenerationId,
                    acceptedEndpointRegistrationGenerationIds),
                cancellationToken)
            .ConfigureAwait(false);
        return LifecycleExecutionEndpointAdvanceOutcome.Advanced;
    }

    internal LifecycleExecutionStorePaths Paths => paths;

    private static LifecycleExecutionStartResult ClassifyExistingStart (
        LifecycleExecutionStoreRecord existing,
        Sha256Digest requestedDefinitionDigest,
        UnityProjectIdentity project,
        LifecycleExecutionHostRegistration host)
    {
        var established = existing.Start;
        if (established.LifecycleExecutionRef.DefinitionDigest
            != requestedDefinitionDigest)
        {
            return new LifecycleExecutionStartResult(
                LifecycleExecutionStartOutcome.DefinitionConflict,
                Binding: null);
        }

        if (established.Project != project)
        {
            return new LifecycleExecutionStartResult(
                LifecycleExecutionStartOutcome.ProjectMismatch,
                Binding: null);
        }

        if (established.Host.Process != host.Process
            || established.Host.EditorInstanceId != host.EditorInstanceId)
        {
            return new LifecycleExecutionStartResult(
                LifecycleExecutionStartOutcome.HostMismatch,
                Binding: null);
        }

        return new LifecycleExecutionStartResult(
            LifecycleExecutionStartOutcome.Reconnected,
            established);
    }

    private static LifecycleExecutionStartBinding CopyStart (
        LifecycleExecutionStartBinding start,
        ExecutionRef reference,
        LifecycleExecutionHostRegistration host)
    {
        return new LifecycleExecutionStartBinding(
            reference,
            start.Project,
            host,
            start.StartedGeneration,
            start.DeadlineUtc,
            start.StartedAtUtc);
    }

    private async ValueTask<LifecycleExecutionStoreRecord?> ReadRecordWithoutLockAsync (
        LifecycleExecutionKind kind,
        Guid executionId,
        CancellationToken cancellationToken)
    {
        var bytes = await FileUtilities.ReadBytesOrNullWithinLimitAsync(
                paths.ResolveRecordPath(kind, executionId),
                MaximumRecordBytes,
                cancellationToken)
            .ConfigureAwait(false);
        if (!bytes.HasValue)
        {
            return null;
        }

        LifecycleExecutionStoreRecord? record;
        try
        {
            record = JsonSerializer.Deserialize<LifecycleExecutionStoreRecord>(
                bytes.Value.Span,
                IpcJsonSerializerOptions.Default);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            throw new IOException(
                $"Lifecycle Execution store record is invalid for kind '{TextVocabulary.GetText(kind)}' and id '{executionId:D}'.",
                exception);
        }

        if (record is null)
        {
            throw new IOException(
                $"Lifecycle Execution store record is empty for kind '{TextVocabulary.GetText(kind)}' and id '{executionId:D}'.");
        }

        ValidateStoredRecord(kind, executionId, record);
        return record;
    }

    private async ValueTask WriteRecordWithoutLockAsync (
        LifecycleExecutionKind kind,
        Guid executionId,
        LifecycleExecutionStoreRecord record,
        CancellationToken cancellationToken)
    {
        ValidateStoredRecord(kind, executionId, record);
        var json = JsonSerializer.Serialize(record, IpcJsonSerializerOptions.Default)
            + Environment.NewLine;
        EnsureFitsRecordSizeLimit(
            Encoding.UTF8.GetByteCount(json),
            "Lifecycle Execution store record");
        await FileUtilities.WriteAllTextAtomicallyAsync(
                paths.ResolveRecordPath(kind, executionId),
                json,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<(
        LifecycleExecutionReferenceUpdateOutcome Outcome,
        StoredLifecycleExecution? AuthoritativeExecution)>
        TryUpdateReferenceCoreAsync (
            ExecutionRef expectedReference,
            ExecutionRef nextReference,
            Guid?
                sideEffectRightClaimantEndpointRegistrationGenerationId,
            CancellationToken cancellationToken)
    {
        ValidateReferencePair(expectedReference, nextReference);
        if (IsPublishingReference(nextReference))
        {
            throw new ArgumentException(
                "The publishing projection must be persisted together with its exact terminal publication intent.",
                nameof(nextReference));
        }

        var kind = GetLifecycleKind(expectedReference);
        var lockPath = paths.ResolveLockPath(kind, expectedReference.Id);
        using var executionLock = await FileExclusiveLock.AcquireAsync(
                lockPath,
                LockAcquireTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var record = await ReadRecordWithoutLockAsync(
                kind,
                expectedReference.Id,
                cancellationToken)
            .ConfigureAwait(false);
        if (record is null)
        {
            return (
                LifecycleExecutionReferenceUpdateOutcome.Missing,
                AuthoritativeExecution: null);
        }

        var authoritativeExecution = record.ToStoredExecution();
        if (record.TerminalReference is not null)
        {
            return (
                LifecycleExecutionReferenceUpdateOutcome.AlreadyTerminal,
                authoritativeExecution);
        }

        if (authoritativeExecution.IsPublishing)
        {
            return (
                LifecycleExecutionReferenceUpdateOutcome.Conflict,
                authoritativeExecution);
        }

        if (record.Start.LifecycleExecutionRef != expectedReference)
        {
            return (
                LifecycleExecutionReferenceUpdateOutcome.Conflict,
                authoritativeExecution);
        }
        if (sideEffectRightClaimantEndpointRegistrationGenerationId
                .HasValue
            && record.Start.Host
                    .CurrentEndpointRegistrationGenerationId
                != sideEffectRightClaimantEndpointRegistrationGenerationId
                    .Value)
        {
            return (
                LifecycleExecutionReferenceUpdateOutcome.Conflict,
                authoritativeExecution);
        }

        var updatedStart = CopyStart(record.Start, nextReference, record.Start.Host);
        var updatedRecord = record with
        {
            Start = updatedStart,
            SideEffectRightOwnerEndpointRegistrationGenerationId =
                sideEffectRightClaimantEndpointRegistrationGenerationId
                    ?? record
                        .SideEffectRightOwnerEndpointRegistrationGenerationId,
        };
        await WriteRecordWithoutLockAsync(
                kind,
                expectedReference.Id,
                updatedRecord,
                cancellationToken)
            .ConfigureAwait(false);
        return (
            LifecycleExecutionReferenceUpdateOutcome.Updated,
            updatedRecord.ToStoredExecution());
    }

    private static void EnsureFitsRecordSizeLimit (
        int sizeBytes,
        string subject)
    {
        if (sizeBytes > MaximumRecordBytes)
        {
            throw new IOException(
                $"{subject} exceeds the maximum size of {MaximumRecordBytes} bytes.");
        }
    }

    private void ValidateStoredRecord (
        LifecycleExecutionKind kind,
        Guid executionId,
        LifecycleExecutionStoreRecord record)
    {
        var start = record.Start;
        var reference = start.LifecycleExecutionRef;
        var acceptedEndpointRegistrationGenerationIds =
            record.AcceptedEndpointRegistrationGenerationIds;
        if (reference.Id != executionId
            || GetLifecycleKind(reference) != kind
            || start.Project.ProjectFingerprint != projectFingerprint
            || !paths.HasExpectedStatusLocator(kind, executionId, reference.StatusLocator))
        {
            throw new IOException(
                $"Lifecycle Execution store record identity does not match kind '{TextVocabulary.GetText(kind)}' and id '{executionId:D}'.");
        }

        if (acceptedEndpointRegistrationGenerationIds[0]
                != start.Host.FirstEndpointRegistrationGenerationId
            || acceptedEndpointRegistrationGenerationIds[^1]
                != start.Host.CurrentEndpointRegistrationGenerationId)
        {
            throw new IOException(
                "Lifecycle Execution accepted endpoint registration generation history does not match its start binding.");
        }

        var sideEffectRightOwner =
            record.SideEffectRightOwnerEndpointRegistrationGenerationId;
        if (sideEffectRightOwner.HasValue
            && !acceptedEndpointRegistrationGenerationIds.Contains(
                sideEffectRightOwner.Value))
        {
            throw new IOException(
                "Lifecycle Execution side-effect right owner is not an accepted endpoint registration generation.");
        }
        if (reference.Lifecycle == ExecutionLifecycle.Active
            && IsRegisteredReference(reference)
            && sideEffectRightOwner.HasValue)
        {
            throw new IOException(
                "A registered Lifecycle Execution cannot own a side-effect right.");
        }
        if (reference.Lifecycle == ExecutionLifecycle.Active
            && !IsRegisteredReference(reference)
            && !sideEffectRightOwner.HasValue)
        {
            throw new IOException(
                "An action-active Lifecycle Execution must retain its accepted side-effect right owner.");
        }

        if (record.TerminalReference is not null)
        {
            if (record.TerminalPublication is not null)
            {
                throw new IOException(
                    "A terminal Lifecycle Execution cannot retain a pending terminal publication intent.");
            }

            ValidateReferencePair(
                reference,
                record.TerminalReference,
                allowTerminalCandidate: true);
            ValidateStoredTerminalReference(
                kind,
                executionId,
                record.TerminalReference);
        }
        else if (record.TerminalPublication is not null)
        {
            if (!IsPublishingReference(reference))
            {
                throw new IOException(
                    "A pending Lifecycle Execution terminal publication requires the publishing state.");
            }

            var fixedTerminalRecord = DeserializeTerminalRecord(
                record.TerminalPublication.TerminalRecordBytes,
                "Lifecycle Execution terminal publication intent");
            ValidateTerminalRecord(
                start,
                fixedTerminalRecord,
                record.TerminalPublication.AcceptedEndpointRegistrationGenerationId);
        }
    }

    private void ValidateStoredTerminalReference (
        LifecycleExecutionKind kind,
        Guid executionId,
        TerminalExecutionRef terminalReference)
    {
        if (!HasTerminalRecordArtifactContract(
                terminalReference.TerminalRecordRef)
            || !paths.HasExpectedTerminalRecordArtifactPath(
                kind,
                executionId,
                terminalReference.TerminalRecordRef))
        {
            throw new IOException(
                "Lifecycle Execution terminal reference must identify its canonical Terminal Record JSON artifact.");
        }

        var state = terminalReference.State.Value;
        if (!string.Equals(
                state,
                TextVocabulary.GetText(LifecycleExecutionState.Completed),
                StringComparison.Ordinal)
            && !string.Equals(
                state,
                TextVocabulary.GetText(LifecycleExecutionState.Failed),
                StringComparison.Ordinal))
        {
            throw new IOException(
                "Lifecycle Execution terminal reference must use a completed or failed state.");
        }
    }

    private static bool HasTerminalRecordArtifactContract (
        ArtifactRef artifactReference)
    {
        return artifactReference.Kind
                == LifecycleExecutionArtifactContract.TerminalRecordKind
            && artifactReference.MediaType
                == LifecycleExecutionArtifactContract.TerminalRecordMediaType;
    }

    private static bool IsRegisteredReference (
        ExecutionRef executionReference)
    {
        return executionReference.Lifecycle == ExecutionLifecycle.Active
            && string.Equals(
                executionReference.State.Value,
                TextVocabulary.GetText(LifecycleExecutionState.Registered),
                StringComparison.Ordinal);
    }

    private static void ValidateReferencePair (
        ExecutionRef establishedReference,
        ExecutionRef candidateReference,
        bool allowTerminalCandidate = false)
    {
        if (establishedReference is null)
        {
            throw new ArgumentNullException(nameof(establishedReference));
        }

        if (candidateReference is null)
        {
            throw new ArgumentNullException(nameof(candidateReference));
        }

        if (establishedReference.Kind != candidateReference.Kind
            || establishedReference.Id != candidateReference.Id
            || establishedReference.DefinitionDigest != candidateReference.DefinitionDigest
            || establishedReference.StatusLocator != candidateReference.StatusLocator)
        {
            throw new ArgumentException(
                "Lifecycle Execution reference update must preserve kind, id, definition digest, and status locator.",
                nameof(candidateReference));
        }

        if (!allowTerminalCandidate
            && candidateReference.Lifecycle == ExecutionLifecycle.Terminal)
        {
            throw new ArgumentException(
                "Terminal references must be committed by terminal publication.",
                nameof(candidateReference));
        }
    }

    private static LifecycleExecutionKind GetLifecycleKind (ExecutionRef executionReference)
    {
        if (!TextVocabulary.TryGetValue(
                executionReference.Kind.Value,
                out LifecycleExecutionKind kind))
        {
            throw new ArgumentException(
                "Execution reference kind is not a Lifecycle Execution kind.",
                nameof(executionReference));
        }

        return kind;
    }

    private static IEnumerable<LifecycleExecutionKind> GetKinds ()
    {
        yield return LifecycleExecutionKind.Refresh;
        yield return LifecycleExecutionKind.Compile;
        yield return LifecycleExecutionKind.PlayEnter;
        yield return LifecycleExecutionKind.PlayExit;
    }
}
