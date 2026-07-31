using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using TextVocabulary = MackySoft.Text.Vocabularies.Vocabulary;

namespace MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

/// <summary>
/// Orders one durable side-effect right before its action-owned admission marker.
/// </summary>
internal sealed class LifecycleExecutionSideEffectAdmissionCoordinator
{
    private static readonly object LocalAdmissionGatesSync = new();
    private static readonly Dictionary<LocalAdmissionIdentity, LocalAdmissionGate>
        LocalAdmissionGates = new();

    private readonly FileLifecycleExecutionStore executionStore;

    public LifecycleExecutionSideEffectAdmissionCoordinator (
        FileLifecycleExecutionStore executionStore)
    {
        this.executionStore = executionStore
            ?? throw new ArgumentNullException(nameof(executionStore));
    }

    public async ValueTask<Resolution<TCheckpoint>> AcquireAsync<TCheckpoint> (
        LifecycleExecutionKind kind,
        StoredLifecycleExecution expectedExecution,
        ExecutionRef nextReference,
        Guid claimantEndpointRegistrationGenerationId,
        ILifecycleExecutionSideEffectAdmissionCheckpointStore<TCheckpoint>
            checkpointStore,
        TCheckpoint checkpoint,
        CancellationToken cancellationToken)
        where TCheckpoint : class
    {
        if (expectedExecution is null)
        {
            throw new ArgumentNullException(nameof(expectedExecution));
        }
        if (nextReference is null)
        {
            throw new ArgumentNullException(nameof(nextReference));
        }
        ValidateCheckpointStore(checkpointStore, checkpoint);
        ValidateClaimantEndpointRegistrationGeneration(
            claimantEndpointRegistrationGenerationId);
        using var localAdmissionLease = await EnterLocalAdmissionAsync(
                new LocalAdmissionIdentity(
                    executionStore.Paths.ResolveLockPath(
                        kind,
                        expectedExecution.Start.LifecycleExecutionRef.Id),
                    claimantEndpointRegistrationGenerationId),
                cancellationToken)
            .ConfigureAwait(false);
        if (checkpointStore.IsAdmitted(checkpoint))
        {
            return await ResolvePersistedAdmissionAsync(
                    kind,
                    expectedExecution.Start.LifecycleExecutionRef.Id,
                    checkpoint,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var sideEffectRight =
            await executionStore.TryAcquireSideEffectRightAsync(
                    expectedExecution.CurrentReference,
                    nextReference,
                    claimantEndpointRegistrationGenerationId,
                    cancellationToken)
                .ConfigureAwait(false);
        switch (sideEffectRight.Outcome)
        {
            case LifecycleExecutionSideEffectRightOutcome.Acquired:
                return await PersistAcquiredAdmissionAsync(
                        RequireAuthoritativeExecution(sideEffectRight),
                        kind,
                        claimantEndpointRegistrationGenerationId,
                        checkpointStore,
                        checkpoint,
                        cancellationToken)
                    .ConfigureAwait(false);
            case LifecycleExecutionSideEffectRightOutcome.Contended:
                return await AwaitContendedAdmissionAsync(
                        kind,
                        RequireAuthoritativeExecution(sideEffectRight),
                        claimantEndpointRegistrationGenerationId,
                        checkpointStore,
                        checkpoint,
                        cancellationToken)
                    .ConfigureAwait(false);
            case LifecycleExecutionSideEffectRightOutcome.TerminalOrPublishing:
                return new Resolution<TCheckpoint>(
                    Outcome.Terminal,
                    RequireAuthoritativeExecution(sideEffectRight),
                    checkpoint);
            case LifecycleExecutionSideEffectRightOutcome.Missing:
                throw new InvalidOperationException(
                    "Lifecycle Execution disappeared during side-effect admission.");
            default:
                throw new InvalidOperationException(
                    $"Lifecycle Execution side-effect admission could not classify outcome '{sideEffectRight.Outcome}'.");
        }
    }

    public async ValueTask<Resolution<TCheckpoint>>
        ReconnectAsync<TCheckpoint> (
        LifecycleExecutionKind kind,
        StoredLifecycleExecution authoritativeExecution,
        Guid claimantEndpointRegistrationGenerationId,
        ILifecycleExecutionSideEffectAdmissionCheckpointStore<TCheckpoint>
            checkpointStore,
        TCheckpoint checkpoint,
        CancellationToken cancellationToken)
        where TCheckpoint : class
    {
        if (authoritativeExecution is null)
        {
            throw new ArgumentNullException(nameof(authoritativeExecution));
        }
        ValidateCheckpointStore(checkpointStore, checkpoint);
        ValidateClaimantEndpointRegistrationGeneration(
            claimantEndpointRegistrationGenerationId);
        using var localAdmissionLease = await EnterLocalAdmissionAsync(
                new LocalAdmissionIdentity(
                    executionStore.Paths.ResolveLockPath(
                        kind,
                        authoritativeExecution.Start.LifecycleExecutionRef.Id),
                    claimantEndpointRegistrationGenerationId),
                cancellationToken)
            .ConfigureAwait(false);
        return await AwaitContendedAdmissionAsync(
                kind,
                authoritativeExecution,
                claimantEndpointRegistrationGenerationId,
                checkpointStore,
                checkpoint,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<Resolution<TCheckpoint>>
        AwaitContendedAdmissionAsync<TCheckpoint> (
        LifecycleExecutionKind kind,
        StoredLifecycleExecution authoritativeExecution,
        Guid claimantEndpointRegistrationGenerationId,
        ILifecycleExecutionSideEffectAdmissionCheckpointStore<TCheckpoint>
            checkpointStore,
        TCheckpoint checkpoint,
        CancellationToken cancellationToken)
        where TCheckpoint : class
    {
        var executionId =
            authoritativeExecution.Start.LifecycleExecutionRef.Id;
        while (true)
        {
            if (authoritativeExecution.IsTerminal
                || authoritativeExecution.IsPublishing)
            {
                return new Resolution<TCheckpoint>(
                    Outcome.Terminal,
                    authoritativeExecution,
                    checkpoint);
            }

            checkpoint = await checkpointStore.ReadAsync(
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "Lifecycle Execution action checkpoint disappeared during side-effect admission.");
            if (checkpointStore.IsAdmitted(checkpoint))
            {
                return await ResolvePersistedAdmissionAsync(
                        kind,
                        executionId,
                        checkpoint,
                        cancellationToken)
                    .ConfigureAwait(false);
            }

            var ownerEndpoint =
                authoritativeExecution
                    .SideEffectRightOwnerEndpointRegistrationGenerationId;
            var currentEndpoint =
                authoritativeExecution.Start.Host
                    .CurrentEndpointRegistrationGenerationId;
            if (!ownerEndpoint.HasValue)
            {
                if (!IsRegistered(authoritativeExecution.CurrentReference))
                {
                    throw new InvalidOperationException(
                        "An admitted Lifecycle Execution state has no durable endpoint owner.");
                }
            }
            else if (ownerEndpoint.Value
                    == claimantEndpointRegistrationGenerationId
                && currentEndpoint
                    == claimantEndpointRegistrationGenerationId)
            {
                // The durable owner proves that this endpoint acquired the right,
                // while the local gate proves that its earlier marker attempt has
                // ended. Retrying the marker cannot issue the action side effect;
                // that remains gated by the successful Acquired resolution.
                return await PersistAcquiredAdmissionAsync(
                        authoritativeExecution,
                        kind,
                        claimantEndpointRegistrationGenerationId,
                        checkpointStore,
                        checkpoint,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            else if (currentEndpoint
                    == claimantEndpointRegistrationGenerationId
                && ownerEndpoint.Value
                    != claimantEndpointRegistrationGenerationId)
            {
                var takeover =
                    await executionStore.TryTakeOverSideEffectRightAsync(
                            authoritativeExecution,
                            claimantEndpointRegistrationGenerationId,
                            cancellationToken)
                        .ConfigureAwait(false);
                switch (takeover.Outcome)
                {
                    case LifecycleExecutionSideEffectRightOutcome.Acquired:
                        return await PersistAcquiredAdmissionAsync(
                                RequireAuthoritativeExecution(takeover),
                                kind,
                                claimantEndpointRegistrationGenerationId,
                                checkpointStore,
                                checkpoint,
                                cancellationToken)
                            .ConfigureAwait(false);
                    case LifecycleExecutionSideEffectRightOutcome.Contended:
                        authoritativeExecution =
                            RequireAuthoritativeExecution(takeover);
                        continue;
                    case LifecycleExecutionSideEffectRightOutcome
                        .TerminalOrPublishing:
                        return new Resolution<TCheckpoint>(
                            Outcome.Terminal,
                            RequireAuthoritativeExecution(takeover),
                            checkpoint);
                    case LifecycleExecutionSideEffectRightOutcome.Missing:
                        throw new InvalidOperationException(
                            "Lifecycle Execution disappeared during side-effect admission takeover.");
                    default:
                        throw new InvalidOperationException(
                            $"Lifecycle Execution side-effect admission takeover could not classify outcome '{takeover.Outcome}'.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            authoritativeExecution = await executionStore.ReadAsync(
                    kind,
                    executionId,
                    cancellationToken)
                .ConfigureAwait(false)
                ?? throw new InvalidOperationException(
                    "Lifecycle Execution disappeared during side-effect admission.");
        }
    }

    private async ValueTask<Resolution<TCheckpoint>>
        PersistAcquiredAdmissionAsync<TCheckpoint> (
        StoredLifecycleExecution authoritativeExecution,
        LifecycleExecutionKind kind,
        Guid claimantEndpointRegistrationGenerationId,
        ILifecycleExecutionSideEffectAdmissionCheckpointStore<TCheckpoint>
            checkpointStore,
        TCheckpoint checkpoint,
        CancellationToken cancellationToken)
        where TCheckpoint : class
    {
        checkpoint = await checkpointStore.MarkAdmittedAsync(
                checkpoint,
                cancellationToken)
            .ConfigureAwait(false);
        if (!checkpointStore.IsAdmitted(checkpoint))
        {
            throw new InvalidOperationException(
                "Lifecycle Execution side-effect admission marker was not persisted.");
        }

        var reverifiedExecution = await executionStore.ReadAsync(
                kind,
                authoritativeExecution.Start.LifecycleExecutionRef.Id,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Lifecycle Execution disappeared after side-effect admission.");
        if (reverifiedExecution.IsTerminal
            || reverifiedExecution.IsPublishing)
        {
            return new Resolution<TCheckpoint>(
                Outcome.Terminal,
                reverifiedExecution,
                checkpoint);
        }
        if (reverifiedExecution.CurrentReference
                != authoritativeExecution.CurrentReference
            || reverifiedExecution
                    .SideEffectRightOwnerEndpointRegistrationGenerationId
                != claimantEndpointRegistrationGenerationId
            || reverifiedExecution.Start.Host
                    .CurrentEndpointRegistrationGenerationId
                != claimantEndpointRegistrationGenerationId)
        {
            return new Resolution<TCheckpoint>(
                Outcome.Recover,
                reverifiedExecution,
                checkpoint);
        }

        return new Resolution<TCheckpoint>(
            Outcome.Acquired,
            reverifiedExecution,
            checkpoint);
    }

    private async ValueTask<Resolution<TCheckpoint>>
        ResolvePersistedAdmissionAsync<TCheckpoint> (
        LifecycleExecutionKind kind,
        Guid executionId,
        TCheckpoint checkpoint,
        CancellationToken cancellationToken)
        where TCheckpoint : class
    {
        var authoritativeExecution = await executionStore.ReadAsync(
                kind,
                executionId,
                cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Lifecycle Execution disappeared during side-effect admission.");
        return new Resolution<TCheckpoint>(
            authoritativeExecution.IsTerminal
                || authoritativeExecution.IsPublishing
                    ? Outcome.Terminal
                    : Outcome.Recover,
            authoritativeExecution,
            checkpoint);
    }

    private static StoredLifecycleExecution RequireAuthoritativeExecution (
        LifecycleExecutionSideEffectRightResult sideEffectRight)
    {
        return sideEffectRight.AuthoritativeExecution
            ?? throw new InvalidOperationException(
                "Lifecycle Execution side-effect admission did not return its authoritative execution.");
    }

    private static void ValidateCheckpointStore<TCheckpoint> (
        ILifecycleExecutionSideEffectAdmissionCheckpointStore<TCheckpoint>
            checkpointStore,
        TCheckpoint checkpoint)
        where TCheckpoint : class
    {
        _ = checkpointStore
            ?? throw new ArgumentNullException(nameof(checkpointStore));
        _ = checkpoint
            ?? throw new ArgumentNullException(nameof(checkpoint));
    }

    private static void ValidateClaimantEndpointRegistrationGeneration (
        Guid claimantEndpointRegistrationGenerationId)
    {
        if (claimantEndpointRegistrationGenerationId == Guid.Empty)
        {
            throw new ArgumentException(
                "Side-effect admission claimant endpoint registration generation must not be empty.",
                nameof(claimantEndpointRegistrationGenerationId));
        }
    }

    private static bool IsRegistered (ExecutionRef executionReference)
    {
        return executionReference.Lifecycle == ExecutionLifecycle.Active
            && string.Equals(
                executionReference.State.Value,
                TextVocabulary.GetText(LifecycleExecutionState.Registered),
                StringComparison.Ordinal);
    }

    private static async ValueTask<LocalAdmissionLease>
        EnterLocalAdmissionAsync (
        LocalAdmissionIdentity admissionIdentity,
        CancellationToken cancellationToken)
    {
        LocalAdmissionGate gate;
        lock (LocalAdmissionGatesSync)
        {
            if (!LocalAdmissionGates.TryGetValue(
                    admissionIdentity,
                    out gate!))
            {
                gate = new LocalAdmissionGate();
                LocalAdmissionGates.Add(admissionIdentity, gate);
            }

            gate.ReferenceCount++;
        }

        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            ReleaseLocalAdmissionReference(admissionIdentity, gate);
            throw;
        }

        return new LocalAdmissionLease(admissionIdentity, gate);
    }

    private static void ReleaseLocalAdmissionReference (
        LocalAdmissionIdentity admissionIdentity,
        LocalAdmissionGate gate)
    {
        lock (LocalAdmissionGatesSync)
        {
            gate.ReferenceCount--;
            if (gate.ReferenceCount != 0)
            {
                return;
            }

            if (LocalAdmissionGates.TryGetValue(
                    admissionIdentity,
                    out var registeredGate)
                && ReferenceEquals(registeredGate, gate))
            {
                LocalAdmissionGates.Remove(admissionIdentity);
            }

            gate.Semaphore.Dispose();
        }
    }

    internal enum Outcome
    {
        Acquired = 1,
        Recover,
        Terminal,
    }

    internal sealed record Resolution<TCheckpoint> (
        Outcome State,
        StoredLifecycleExecution AuthoritativeExecution,
        TCheckpoint Checkpoint)
        where TCheckpoint : class;

    private readonly record struct LocalAdmissionIdentity (
        AbsolutePath ExecutionLockPath,
        Guid ClaimantEndpointRegistrationGenerationId);

    private sealed class LocalAdmissionGate
    {
        public SemaphoreSlim Semaphore { get; } = new(1, 1);

        public int ReferenceCount { get; set; }
    }

    private sealed class LocalAdmissionLease : IDisposable
    {
        private readonly LocalAdmissionIdentity admissionIdentity;
        private LocalAdmissionGate? gate;

        public LocalAdmissionLease (
            LocalAdmissionIdentity admissionIdentity,
            LocalAdmissionGate gate)
        {
            this.admissionIdentity = admissionIdentity;
            this.gate = gate;
        }

        public void Dispose ()
        {
            var ownedGate = Interlocked.Exchange(ref gate, null);
            if (ownedGate is null)
            {
                return;
            }

            ownedGate.Semaphore.Release();
            ReleaseLocalAdmissionReference(admissionIdentity, ownedGate);
        }
    }
}
