namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Persists Program Run aggregates with create-only registration and versioned replacement. </summary>
internal interface IProgramRunStore
{
    ValueTask<ArtifactRef> PublishDefinitionSnapshotAsync (Guid runId, ProgramDefinitionSnapshot snapshot, CancellationToken cancellationToken = default);

    ValueTask<ProgramRunStoreCreateResult> CreateAsync (ProgramRunRecord run, CancellationToken cancellationToken = default);

    ValueTask<ProgramRunRecord?> ReadAsync (Guid runId, CancellationToken cancellationToken = default);

    ValueTask<ProgramRunStoredDefinition?> ReadDefinitionAsync (Guid runId, CancellationToken cancellationToken = default);

    ValueTask<ProgramRunStoreCompareExchangeResult> CompareExchangeAsync (
        ProgramRunRecord expected,
        ProgramRunRecord replacement,
        CancellationToken cancellationToken = default);

    ValueTask<ProgramRunTerminalPublicationResult> PublishRunTerminalAsync (
        ProgramRunRecord expected,
        ProgramRunTerminalRecord terminalRecord,
        Func<ArtifactRef, ProgramRunRecord> createReplacement,
        CancellationToken cancellationToken = default);

    ValueTask<ProgramRunStepTerminalPublicationResult> PublishStepTerminalAsync (
        ProgramRunRecord expected,
        int stepIndex,
        ProgramStepTerminalRecord terminalRecord,
        Func<ArtifactRef, ProgramRunRecord> createReplacement,
        CancellationToken cancellationToken = default);
}

/// <summary> Reports create-only Program Run registration. </summary>
internal sealed record ProgramRunStoreCreateResult (bool Created, ProgramRunRecord Current);

/// <summary> Reports one compare-and-swap Program Run update. </summary>
internal sealed record ProgramRunStoreCompareExchangeResult (bool Exchanged, ProgramRunRecord Current);

/// <summary> Returns one durable Run together with its verified fixed definition. </summary>
internal sealed record ProgramRunStoredDefinition (ProgramRunRecord Run, ProgramDefinitionSnapshotFixedDefinition Definition);

/// <summary> Reports publication and CAS admission of one immutable Program Run terminal record. </summary>
internal sealed record ProgramRunTerminalPublicationResult (bool Published, ArtifactRef TerminalRecordRef, ProgramRunRecord Current);

/// <summary> Reports publication and CAS admission of one immutable Program Step terminal record. </summary>
internal sealed record ProgramRunStepTerminalPublicationResult (bool Published, ArtifactRef TerminalRecordRef, ProgramRunRecord Current);
