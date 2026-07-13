using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Observation;

/// <summary> Represents one validated daemon lifecycle observation persisted outside the IPC endpoint. </summary>
internal sealed record DaemonLifecycleObservation
{
    /// <summary> Initializes one daemon lifecycle observation after validating every persisted value. </summary>
    /// <param name="processId"> The positive Unity process identifier. </param>
    /// <param name="processStartedAtUtc"> The non-default Unity process start timestamp. </param>
    /// <param name="editorMode"> The canonical daemon Editor mode literal. </param>
    /// <param name="lifecycleState"> The canonical Editor lifecycle-state literal. </param>
    /// <param name="blockingReason"> The canonical Editor blocking-reason literal, or <see langword="null" /> when no reason exists. </param>
    /// <param name="compileState"> The canonical compile-state literal. </param>
    /// <param name="compileGeneration"> The non-blank compile generation, or <see langword="null" /> when unavailable. </param>
    /// <param name="domainReloadGeneration"> The non-blank domain-reload generation, or <see langword="null" /> when unavailable. </param>
    /// <param name="observedAtUtc"> The non-default observation timestamp. </param>
    /// <param name="actionRequired"> The supported action-required literal, or <see langword="null" /> when no action is required. </param>
    /// <param name="primaryDiagnostic"> The validated primary diagnostic, or <see langword="null" /> when no diagnostic exists. </param>
    /// <param name="serverVersion"> The non-blank daemon server version, or <see langword="null" /> when unavailable. </param>
    /// <param name="canAcceptExecutionRequests"> Whether the daemon accepted normal execution requests at observation time. </param>
    /// <param name="editorInstanceId"> The non-empty Unity Editor process instance identifier. </param>
    /// <param name="playMode"> The validated Play Mode snapshot, or <see langword="null" /> when unavailable. </param>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="processId" /> is not positive. </exception>
    /// <exception cref="ArgumentException"> Thrown when a timestamp, literal, optional string, nested contract, or <paramref name="editorInstanceId" /> violates its documented constraint. </exception>
    public DaemonLifecycleObservation (
        int processId,
        DateTimeOffset processStartedAtUtc,
        string editorMode,
        string lifecycleState,
        string? blockingReason,
        string compileState,
        string? compileGeneration,
        string? domainReloadGeneration,
        DateTimeOffset observedAtUtc,
        string? actionRequired,
        IpcPrimaryDiagnostic? primaryDiagnostic,
        string? serverVersion,
        bool canAcceptExecutionRequests,
        Guid editorInstanceId,
        IpcPlayModeSnapshot? playMode)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId), processId, "Process identifier must be positive.");
        }

        if (processStartedAtUtc == default)
        {
            throw new ArgumentException("Process start timestamp must not be the default value.", nameof(processStartedAtUtc));
        }

        if (!ContractLiteralCodec.IsDefined<DaemonEditorMode>(editorMode))
        {
            throw new ArgumentException("Editor mode must be a canonical supported literal.", nameof(editorMode));
        }

        if (!IpcEditorLifecycleStateCodec.TryParse(lifecycleState, out var canonicalLifecycleState)
            || !string.Equals(lifecycleState, canonicalLifecycleState, StringComparison.Ordinal))
        {
            throw new ArgumentException("Lifecycle state must be a canonical supported literal.", nameof(lifecycleState));
        }

        if (blockingReason is not null
            && (!IpcEditorBlockingReasonCodec.TryParse(blockingReason, out var canonicalBlockingReason)
                || !string.Equals(blockingReason, canonicalBlockingReason, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Blocking reason must be null or a canonical supported literal.", nameof(blockingReason));
        }

        if (!IpcCompileStateCodec.TryParse(compileState, out var canonicalCompileState)
            || !string.Equals(compileState, canonicalCompileState, StringComparison.Ordinal))
        {
            throw new ArgumentException("Compile state must be a canonical supported literal.", nameof(compileState));
        }

        ThrowIfOptionalStringIsBlank(compileGeneration, nameof(compileGeneration), "Compile generation");
        ThrowIfOptionalStringIsBlank(domainReloadGeneration, nameof(domainReloadGeneration), "Domain-reload generation");

        if (observedAtUtc == default)
        {
            throw new ArgumentException("Observation timestamp must not be the default value.", nameof(observedAtUtc));
        }

        if (actionRequired is not null && !DaemonDiagnosisActionRequiredValues.IsSupported(actionRequired))
        {
            throw new ArgumentException("Action required must be null or a canonical supported literal.", nameof(actionRequired));
        }

        ValidatePrimaryDiagnostic(primaryDiagnostic);
        ThrowIfOptionalStringIsBlank(serverVersion, nameof(serverVersion), "Server version");

        if (editorInstanceId == Guid.Empty)
        {
            throw new ArgumentException("Editor instance identifier must not be empty.", nameof(editorInstanceId));
        }

        ValidatePlayMode(playMode);

        ProcessId = processId;
        ProcessStartedAtUtc = processStartedAtUtc;
        EditorMode = editorMode;
        LifecycleState = lifecycleState;
        BlockingReason = blockingReason;
        CompileState = compileState;
        CompileGeneration = compileGeneration;
        DomainReloadGeneration = domainReloadGeneration;
        ObservedAtUtc = observedAtUtc;
        ActionRequired = actionRequired;
        PrimaryDiagnostic = primaryDiagnostic;
        ServerVersion = serverVersion;
        CanAcceptExecutionRequests = canAcceptExecutionRequests;
        EditorInstanceId = editorInstanceId;
        PlayMode = playMode;
    }

    /// <summary> Gets the observed Unity process identifier. </summary>
    public int ProcessId { get; }

    /// <summary> Gets the observed Unity process start timestamp. </summary>
    public DateTimeOffset ProcessStartedAtUtc { get; }

    /// <summary> Gets the daemon Editor mode identifier. </summary>
    public string EditorMode { get; }

    /// <summary> Gets the Editor lifecycle state. </summary>
    public string LifecycleState { get; }

    /// <summary> Gets the current blocking reason when one exists. </summary>
    public string? BlockingReason { get; }

    /// <summary> Gets the compile state. </summary>
    public string CompileState { get; }

    /// <summary> Gets the compile generation when one exists. </summary>
    public string? CompileGeneration { get; }

    /// <summary> Gets the domain-reload generation when one exists. </summary>
    public string? DomainReloadGeneration { get; }

    /// <summary> Gets the observation timestamp. </summary>
    public DateTimeOffset ObservedAtUtc { get; }

    /// <summary> Gets the action required to resolve the lifecycle state when one exists. </summary>
    public string? ActionRequired { get; }

    /// <summary> Gets the primary diagnostic when one exists. </summary>
    public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }

    /// <summary> Gets the daemon server version that wrote the observation. </summary>
    public string? ServerVersion { get; }

    /// <summary> Gets whether the observed daemon accepted normal execution requests at observation time. </summary>
    public bool CanAcceptExecutionRequests { get; }

    /// <summary> Gets the Unity Editor process instance identifier that survives domain reloads within the process. </summary>
    public Guid EditorInstanceId { get; }

    /// <summary> Gets the Play Mode subsystem snapshot captured with the lifecycle observation. </summary>
    public IpcPlayModeSnapshot? PlayMode { get; }

    /// <summary> Gets a value indicating whether this observation means the same Unity process may recover its endpoint. </summary>
    public bool IsRecovering => string.Equals(LifecycleState, IpcEditorLifecycleStateCodec.Recovering, StringComparison.Ordinal)
        || string.Equals(LifecycleState, IpcEditorLifecycleStateCodec.DomainReloading, StringComparison.Ordinal);

    private static void ValidatePrimaryDiagnostic (IpcPrimaryDiagnostic? primaryDiagnostic)
    {
        if (primaryDiagnostic is null)
        {
            return;
        }

        if (primaryDiagnostic.Kind is null
            || !DaemonDiagnosisPrimaryDiagnosticKindValues.IsSupported(primaryDiagnostic.Kind))
        {
            throw new ArgumentException(
                "Primary diagnostic kind must be a canonical supported literal.",
                nameof(primaryDiagnostic));
        }

        ThrowIfOptionalStringIsBlank(
            primaryDiagnostic.Code,
            nameof(primaryDiagnostic),
            "Primary diagnostic code");
        ThrowIfOptionalStringIsBlank(
            primaryDiagnostic.File,
            nameof(primaryDiagnostic),
            "Primary diagnostic file");

        if (primaryDiagnostic.Line is <= 0)
        {
            throw new ArgumentException(
                "Primary diagnostic line must be null or positive.",
                nameof(primaryDiagnostic));
        }

        if (primaryDiagnostic.Column is <= 0)
        {
            throw new ArgumentException(
                "Primary diagnostic column must be null or positive.",
                nameof(primaryDiagnostic));
        }

        ThrowIfOptionalStringIsBlank(
            primaryDiagnostic.Message,
            nameof(primaryDiagnostic),
            "Primary diagnostic message");
    }

    private static void ValidatePlayMode (IpcPlayModeSnapshot? playMode)
    {
        if (playMode is null)
        {
            return;
        }

        if (!ContractLiteralCodec.IsDefined<IpcPlayModeState>(playMode.State))
        {
            throw new ArgumentException(
                "Play Mode state must be a canonical supported literal.",
                nameof(playMode));
        }

        if (!ContractLiteralCodec.IsDefined<IpcPlayModeTransition>(playMode.Transition))
        {
            throw new ArgumentException(
                "Play Mode transition must be a canonical supported literal.",
                nameof(playMode));
        }

        ThrowIfOptionalStringIsBlank(playMode.Generation, nameof(playMode), "Play Mode generation");
    }

    private static void ThrowIfOptionalStringIsBlank (
        string? value,
        string parameterName,
        string fieldName)
    {
        if (value is not null && string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{fieldName} must be null or non-blank.", parameterName);
        }
    }
}
