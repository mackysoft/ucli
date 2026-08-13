using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Programs.Persistence;

/// <summary> Holds the complete execution facts fixed when a Program Run is registered. </summary>
internal sealed record ProgramRunFixedContext (
    ProgramEffectiveAuthorizationSnapshot Authorization,
    ProgramEffectiveConfigurationSnapshot Configuration,
    ProgramExecutionModeSnapshot ExecutionMode,
    ProgramAttachedSupervisorSnapshot Supervisor)
{
    /// <summary> Gets whether this Run must refuse a waitable admission state without waiting. </summary>
    public bool FailFast { get; init; }

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
        var digest = Sha256Digest.Parse(Digest ?? throw new ArgumentNullException(nameof(Digest)));
        if (CapturedAtUtc == default || CapturedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Authorization snapshot time must be a non-default UTC timestamp.", nameof(CapturedAtUtc));
        }
        if (digest != IpcProgramEffectiveAuthorizationSnapshot.ComputeDigest(AllowDangerous, AllowPlayMode))
        {
            throw new ArgumentException("Authorization snapshot digest does not match its effective permissions.");
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
        if (Digest != IpcProgramEffectiveConfigurationSnapshot.ComputeDigest(
                SchemaVersion,
                TextVocabulary.GetText(OperationPolicy),
                TextVocabulary.GetText(PlanTokenMode),
                TextVocabulary.GetText(ReadIndexDefaultMode),
                OperationAllowlist,
                IpcDefaultTimeoutMilliseconds,
                IpcTimeoutMillisecondsByCommand,
                EvalEnabled))
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
    ProcessIdentity OwnerProcess,
    ProgramSupervisorConnection Connection,
    ProgramSupervisorAvailability Availability,
    DateTimeOffset LastObservedAtUtc)
{
    public ProgramAttachedSupervisorSnapshot Validate ()
    {
        if (SupervisorId == Guid.Empty || HostId == Guid.Empty || OwnerProcess is null || !TextVocabulary.IsDefined(Connection)
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
