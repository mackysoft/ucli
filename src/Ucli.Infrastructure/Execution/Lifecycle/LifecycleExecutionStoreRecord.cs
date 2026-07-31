using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

/// <summary> Represents the common durable projection without owning action-specific checkpoints. </summary>
internal sealed record LifecycleExecutionStoreRecord
{
    public const int CurrentSchemaVersion = 1;

    [JsonConstructor]
    public LifecycleExecutionStoreRecord (
        int schemaVersion,
        LifecycleExecutionStartBinding start,
        TerminalExecutionRef? terminalReference,
        LifecycleExecutionTerminalPublicationIntent? terminalPublication,
        Guid? sideEffectRightOwnerEndpointRegistrationGenerationId,
        IReadOnlyList<Guid> acceptedEndpointRegistrationGenerationIds)
    {
        if (schemaVersion != CurrentSchemaVersion)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schemaVersion),
                schemaVersion,
                "Unsupported Lifecycle Execution store record schema version.");
        }

        SchemaVersion = schemaVersion;
        Start = start ?? throw new ArgumentNullException(nameof(start));
        TerminalReference = terminalReference;
        TerminalPublication = terminalPublication;
        if (sideEffectRightOwnerEndpointRegistrationGenerationId
                == Guid.Empty)
        {
            throw new ArgumentException(
                "Side-effect right owner endpoint registration generation must not be empty.",
                nameof(
                    sideEffectRightOwnerEndpointRegistrationGenerationId));
        }

        SideEffectRightOwnerEndpointRegistrationGenerationId =
            sideEffectRightOwnerEndpointRegistrationGenerationId;
        if (acceptedEndpointRegistrationGenerationIds is null)
        {
            throw new ArgumentNullException(
                nameof(acceptedEndpointRegistrationGenerationIds));
        }
        if (acceptedEndpointRegistrationGenerationIds.Count == 0)
        {
            throw new ArgumentException(
                "Accepted endpoint registration generation history must not be empty.",
                nameof(acceptedEndpointRegistrationGenerationIds));
        }

        var acceptedGenerations =
            new Guid[acceptedEndpointRegistrationGenerationIds.Count];
        var distinctGenerations = new HashSet<Guid>();
        for (var index = 0;
            index < acceptedEndpointRegistrationGenerationIds.Count;
            index++)
        {
            var generationId = acceptedEndpointRegistrationGenerationIds[index];
            if (generationId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Accepted endpoint registration generation history must not contain an empty identifier.",
                    nameof(acceptedEndpointRegistrationGenerationIds));
            }
            if (!distinctGenerations.Add(generationId))
            {
                throw new ArgumentException(
                    "Accepted endpoint registration generation history must not contain duplicates.",
                    nameof(acceptedEndpointRegistrationGenerationIds));
            }

            acceptedGenerations[index] = generationId;
        }

        AcceptedEndpointRegistrationGenerationIds =
            Array.AsReadOnly(acceptedGenerations);
    }

    public int SchemaVersion { get; init; }

    public LifecycleExecutionStartBinding Start { get; init; }

    public TerminalExecutionRef? TerminalReference { get; init; }

    public LifecycleExecutionTerminalPublicationIntent? TerminalPublication { get; init; }

    public Guid? SideEffectRightOwnerEndpointRegistrationGenerationId
    {
        get;
        init;
    }

    public IReadOnlyList<Guid> AcceptedEndpointRegistrationGenerationIds { get; }

    public StoredLifecycleExecution ToStoredExecution ()
    {
        return new StoredLifecycleExecution(
            Start,
            TerminalReference,
            SideEffectRightOwnerEndpointRegistrationGenerationId);
    }
}

/// <summary>
/// Retains the exact artifact bytes and accepted endpoint generation needed to recover a create-only publication.
/// </summary>
internal sealed record LifecycleExecutionTerminalPublicationIntent
{
    [JsonConstructor]
    public LifecycleExecutionTerminalPublicationIntent (
        Guid acceptedEndpointRegistrationGenerationId,
        byte[] terminalRecordBytes)
    {
        if (terminalRecordBytes is null || terminalRecordBytes.Length == 0)
        {
            throw new ArgumentException(
                "Terminal record bytes must not be empty.",
                nameof(terminalRecordBytes));
        }

        AcceptedEndpointRegistrationGenerationId =
            ContractArgumentGuard.RequireNonEmptyGuid(
                acceptedEndpointRegistrationGenerationId,
                nameof(acceptedEndpointRegistrationGenerationId));
        TerminalRecordBytes = terminalRecordBytes;
    }

    public Guid AcceptedEndpointRegistrationGenerationId { get; }

    public byte[] TerminalRecordBytes { get; }
}
