using System.Buffers;
using System.Text.Json;
using MackySoft.Json.Canonicalization;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;

namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Holds the complete execution facts fixed when a Program Run is registered. </summary>
internal sealed record ProgramRunFixedContext (
    ProgramEffectiveAuthorizationSnapshot Authorization,
    ProgramEffectiveConfigurationSnapshot Configuration,
    ProgramExecutionModeSnapshot ExecutionMode,
    ProgramAttachedSupervisorSnapshot Supervisor)
{
    public ProgramRunFixedContext Validate ()
    {
        (Authorization ?? throw new ArgumentNullException(nameof(Authorization))).Validate();
        (Configuration ?? throw new ArgumentNullException(nameof(Configuration))).Validate();
        (ExecutionMode ?? throw new ArgumentNullException(nameof(ExecutionMode))).Validate();
        (Supervisor ?? throw new ArgumentNullException(nameof(Supervisor))).Validate();
        return this;
    }
}

/// <summary> Captures the two explicit permissions propagated only to child Requests. </summary>
internal sealed record ProgramEffectiveAuthorizationSnapshot (
    bool AllowDangerous,
    bool AllowPlayMode,
    string Digest,
    DateTimeOffset CapturedAtUtc)
{
    public ProgramEffectiveAuthorizationSnapshot Validate ()
    {
        _ = Sha256Digest.Parse(Digest ?? throw new ArgumentNullException(nameof(Digest)));
        if (CapturedAtUtc == default || CapturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Authorization snapshot time must be a non-default UTC timestamp.", nameof(CapturedAtUtc));
        }
        return this;
    }
}

/// <summary> Captures every configuration value that can influence a Program child Request. </summary>
internal sealed record ProgramEffectiveConfigurationSnapshot (
    int SchemaVersion,
    OperationPolicy OperationPolicy,
    PlanTokenMode PlanTokenMode,
    ReadIndexMode ReadIndexDefaultMode,
    IReadOnlyList<string> OperationAllowlist,
    int IpcDefaultTimeoutMilliseconds,
    IReadOnlyDictionary<string, int> IpcTimeoutMillisecondsByCommand,
    bool EvalEnabled,
    Sha256Digest Digest,
    DateTimeOffset CapturedAtUtc)
{
    public static Sha256Digest ComputeDigest (
        int schemaVersion,
        OperationPolicy operationPolicy,
        PlanTokenMode planTokenMode,
        ReadIndexMode readIndexDefaultMode,
        IReadOnlyList<string> operationAllowlist,
        int ipcDefaultTimeoutMilliseconds,
        IReadOnlyDictionary<string, int> ipcTimeoutMillisecondsByCommand,
        bool evalEnabled)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", schemaVersion);
            writer.WriteString("operationPolicy", Vocabulary.GetText(operationPolicy));
            writer.WriteString("planTokenMode", Vocabulary.GetText(planTokenMode));
            writer.WriteString("readIndexDefaultMode", Vocabulary.GetText(readIndexDefaultMode));
            writer.WritePropertyName("operationAllowlist");
            writer.WriteStartArray();
            foreach (var entry in operationAllowlist)
            {
                writer.WriteStringValue(entry);
            }
            writer.WriteEndArray();
            writer.WriteNumber("ipcDefaultTimeoutMilliseconds", ipcDefaultTimeoutMilliseconds);
            writer.WritePropertyName("ipcTimeoutMillisecondsByCommand");
            writer.WriteStartObject();
            foreach (var entry in ipcTimeoutMillisecondsByCommand.OrderBy(static entry => entry.Key, StringComparer.Ordinal))
            {
                writer.WriteNumber(entry.Key, entry.Value);
            }
            writer.WriteEndObject();
            writer.WriteBoolean("evalEnabled", evalEnabled);
            writer.WriteEndObject();
        }
        using var document = JsonDocument.Parse(buffer.WrittenMemory);
        return Sha256Digest.Compute(Rfc8785JsonCanonicalizer.Canonicalize(document.RootElement));
    }

    public ProgramEffectiveConfigurationSnapshot Validate ()
    {
        if (SchemaVersion < 1 || !TextVocabulary.IsDefined(OperationPolicy) || !TextVocabulary.IsDefined(PlanTokenMode)
            || !TextVocabulary.IsDefined(ReadIndexDefaultMode) || OperationAllowlist is null || IpcDefaultTimeoutMilliseconds < 1
            || IpcTimeoutMillisecondsByCommand is null || Digest is null || CapturedAtUtc == default || CapturedAtUtc.Offset != TimeSpan.Zero
            || OperationAllowlist.Any(string.IsNullOrWhiteSpace) || OperationAllowlist.Distinct(StringComparer.Ordinal).Count() != OperationAllowlist.Count
            || IpcTimeoutMillisecondsByCommand.Any(static item => string.IsNullOrWhiteSpace(item.Key) || item.Value < 1))
        {
            throw new ArgumentException("Program configuration snapshot must be complete, effective, and closed.");
        }
        if (Digest != ComputeDigest(SchemaVersion, OperationPolicy, PlanTokenMode, ReadIndexDefaultMode, OperationAllowlist,
                IpcDefaultTimeoutMilliseconds, IpcTimeoutMillisecondsByCommand, EvalEnabled))
        {
            throw new ArgumentException("Program configuration snapshot digest does not match its effective settings.");
        }
        return this;
    }
}

/// <summary> Identifies the requested and resolved Unity execution path fixed for one Run. </summary>
internal sealed record ProgramExecutionModeSnapshot (string RequestedMode, string ResolvedMode)
{
    public ProgramExecutionModeSnapshot Validate ()
    {
        if (RequestedMode is not ("auto" or "daemon" or "oneshot") || ResolvedMode is not ("daemon" or "oneshot"))
        {
            throw new ArgumentException("Program execution mode snapshot must contain one requested and resolved mode.");
        }
        return this;
    }
}

/// <summary> Captures the attached CLI owner and its last observed connection facts without granting control. </summary>
internal sealed record ProgramAttachedSupervisorSnapshot (
    Guid SupervisorId,
    Guid HostId,
    ProgramSupervisorConnection Connection,
    ProgramSupervisorAvailability Availability,
    DateTimeOffset LastObservedAtUtc)
{
    public ProgramAttachedSupervisorSnapshot Validate ()
    {
        if (SupervisorId == Guid.Empty || HostId == Guid.Empty || !TextVocabulary.IsDefined(Connection)
            || !TextVocabulary.IsDefined(Availability) || LastObservedAtUtc == default || LastObservedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Attached supervisor snapshot must retain a complete identity and UTC observation.");
        }
        return this;
    }
}

[VocabularyDefinition]
internal enum ProgramSupervisorConnection
{
    [VocabularyText("connected")]
    Connected,
    [VocabularyText("lost")]
    Lost,
}

[VocabularyDefinition]
internal enum ProgramSupervisorAvailability
{
    [VocabularyText("available")]
    Available,
    [VocabularyText("unavailable")]
    Unavailable,
}
