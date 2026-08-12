using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.UnityIntegration.Ipc.Dispatch;

/// <summary> Defines the safety boundary for replaying one request after response delivery was interrupted. </summary>
internal enum UnityIpcResponseReplayPolicy
{
    /// <summary> Does not replay after the request may have reached its handler. </summary>
    None,

    /// <summary> Replays an intrinsically side-effect-free read on any host serving the same project. </summary>
    StatelessAnyHostSuccessor,

    /// <summary>
    /// Replays the same logical Lifecycle Execution only on a successor endpoint owned by the same Unity host.
    /// </summary>
    LifecycleExecutionSameHostSuccessor,
}

/// <summary> Represents one IPC method dispatch request after application payload conversion. </summary>
internal sealed record UnityIpcDispatchRequest
{
    private readonly JsonElement payload;

    private readonly Func<LifecycleExecutionStartBinding, JsonElement>? lifecyclePayloadFactory;

    private readonly ILifecycleExecutionStartObserver? lifecycleStartObserver;

    private readonly LifecycleExecutionStartObserverGate lifecycleStartObserverGate;

    /// <summary> Initializes a new instance of the <see cref="UnityIpcDispatchRequest" /> class. </summary>
    /// <param name="method"> The defined Unity IPC method. </param>
    /// <param name="payload"> The IPC payload element. </param>
    /// <param name="launchOptions"> The explicit process launch options used only when oneshot execution is selected. </param>
    public UnityIpcDispatchRequest (
        UnityIpcMethod method,
        JsonElement payload,
        UnityBatchmodeLaunchOptions launchOptions)
    {
        if (!TextVocabulary.IsDefined(method))
        {
            throw new ArgumentOutOfRangeException(nameof(method), method, "Unity IPC method must be defined.");
        }

        ArgumentNullException.ThrowIfNull(launchOptions);
        if (launchOptions.ActiveBuildProfilePath is not null && method != UnityIpcMethod.BuildRun)
        {
            throw new ArgumentException(
                "An active Unity Build Profile may be specified only for build.run dispatch.",
                nameof(launchOptions));
        }

        Method = method;
        this.payload = payload;
        LaunchOptions = launchOptions;
        lifecycleStartObserverGate = new LifecycleExecutionStartObserverGate(
            observer: null);
    }

    private UnityIpcDispatchRequest (
        UnityIpcMethod method,
        LifecycleExecutionRegistration registration,
        LifecycleExecutionStartBinding? requiredStart,
        Func<LifecycleExecutionStartBinding, JsonElement> lifecyclePayloadFactory,
        ILifecycleExecutionStartAdmissionPolicy? startAdmissionPolicy,
        ILifecycleExecutionStartObserver? lifecycleStartObserver)
    {
        if (!UnityIpcMethodCapabilities.SupportsLifecycleExecution(method))
        {
            throw new ArgumentOutOfRangeException(
                nameof(method),
                method,
                "Lifecycle dispatch requires a Lifecycle Execution action method.");
        }

        Registration = registration ?? throw new ArgumentNullException(nameof(registration));
        if (requiredStart is not null
            && (!registration.HasSameIdentity(
                    requiredStart.LifecycleExecutionRef)
                || requiredStart.DeadlineUtc != registration.DeadlineUtc
                || requiredStart.StartedAtUtc != registration.StartedAtUtc))
        {
            throw new ArgumentException(
                "The required Lifecycle Execution start does not match the immutable registration.",
                nameof(requiredStart));
        }

        RequiredStart = requiredStart;
        this.lifecyclePayloadFactory = lifecyclePayloadFactory
            ?? throw new ArgumentNullException(nameof(lifecyclePayloadFactory));
        EnsureKindMatchesMethod(method, registration.Definition.Kind);
        Method = method;
        LaunchOptions = UnityBatchmodeLaunchOptions.Default;
        if (startAdmissionPolicy is not null && requiredStart is not null)
        {
            throw new ArgumentException(
                "A reconnected Lifecycle Execution must not repeat new-execution start admission.",
                nameof(startAdmissionPolicy));
        }

        StartAdmissionPolicy = startAdmissionPolicy;
        this.lifecycleStartObserver = lifecycleStartObserver;
        lifecycleStartObserverGate = new LifecycleExecutionStartObserverGate(
            lifecycleStartObserver);
    }

    /// <summary> Gets the defined Unity IPC method. </summary>
    public UnityIpcMethod Method { get; }

    /// <summary> Gets the IPC payload element for a request that is not a Lifecycle Execution. </summary>
    /// <exception cref="InvalidOperationException">
    /// The dispatch requires a persisted Lifecycle Execution start binding.
    /// </exception>
    public JsonElement Payload => Registration == null
        ? payload
        : throw new InvalidOperationException(
            "Lifecycle Execution payloads must be created from a persisted start binding.");

    /// <summary>
    /// Gets the provider-neutral registration fixed by the action application handler, or
    /// <see langword="null" /> for a request that is not a Lifecycle Execution.
    /// </summary>
    public LifecycleExecutionRegistration? Registration { get; }

    /// <summary>
    /// Gets the authoritative durable Start Record that a reconnect must bind to, or
    /// <see langword="null" /> for a new Lifecycle Execution.
    /// </summary>
    public LifecycleExecutionStartBinding? RequiredStart { get; }

    /// <summary>
    /// Gets the action-owned policy to apply before requesting a new Start Record, or
    /// <see langword="null" /> when the action has no caller-side admission or is reconnecting.
    /// </summary>
    public ILifecycleExecutionStartAdmissionPolicy? StartAdmissionPolicy { get; }

    /// <summary>
    /// Gets the durable-start observer that must complete successfully before a new action enters
    /// its provider, or <see langword="null" /> for callers that do not own an additional durable
    /// record.
    /// </summary>
    public ILifecycleExecutionStartObserver? LifecycleStartObserver => lifecycleStartObserver;

    /// <summary>
    /// Observes a provider-confirmed Start Record at most once for this logical dispatch. Response
    /// recovery may report the same persisted binding again, but never re-runs durable-start
    /// persistence.
    /// </summary>
    public ValueTask<LifecycleExecutionStartObservation> ObserveLifecycleStartAsync (
        LifecycleExecutionStartBinding start)
    {
        ArgumentNullException.ThrowIfNull(start);
        return lifecycleStartObserverGate.ObserveAsync(start);
    }

    /// <summary>
    /// Gets whether this dispatch may create its immutable Start Record rather than reconnecting
    /// an already-started execution.
    /// </summary>
    public bool BeginsLifecycleExecution =>
        Registration is not null
        && RequiredStart is null;

    /// <summary>
    /// Gets whether this request may be replayed to the same host or any host serving the same project
    /// after response interruption.
    /// </summary>
    public UnityIpcResponseReplayPolicy ResponseReplayPolicy =>
        Registration != null
            ? UnityIpcResponseReplayPolicy.LifecycleExecutionSameHostSuccessor
            : UnityIpcMethodCapabilities.SupportsStatelessReadReplay(Method)
                ? UnityIpcResponseReplayPolicy.StatelessAnyHostSuccessor
                : UnityIpcResponseReplayPolicy.None;

    /// <summary> Gets the process launch options used when oneshot execution is selected. </summary>
    public UnityBatchmodeLaunchOptions LaunchOptions { get; }

    /// <summary> Creates one Lifecycle Execution dispatch whose payload requires a persisted start binding. </summary>
    public static UnityIpcDispatchRequest LifecycleExecution (
        UnityIpcMethod method,
        LifecycleExecutionRegistration registration,
        LifecycleExecutionStartBinding? requiredStart,
        Func<LifecycleExecutionStartBinding, JsonElement> payloadFactory,
        ILifecycleExecutionStartAdmissionPolicy? startAdmissionPolicy = null,
        ILifecycleExecutionStartObserver? lifecycleStartObserver = null)
    {
        return new UnityIpcDispatchRequest(
            method,
            registration,
            requiredStart,
            payloadFactory,
            startAdmissionPolicy,
            lifecycleStartObserver);
    }

    /// <summary> Creates the provider-private registration request sent before the action request. </summary>
    public IpcLifecycleExecutionStartRequest CreateLifecycleStartRequest ()
    {
        var registration = Registration
            ?? throw new InvalidOperationException(
                "The dispatch request does not represent a Lifecycle Execution.");
        return new IpcLifecycleExecutionStartRequest(
            registration.Definition.Kind,
            registration.ExecutionId,
            LifecycleExecutionDefinitionDigest.Calculate(registration.Definition),
            registration.DeadlineUtc,
            registration.StartedAtUtc);
    }

    /// <summary> Creates the action request payload from the start binding returned by the provider. </summary>
    public JsonElement CreateLifecycleActionPayload (LifecycleExecutionStartBinding start)
    {
        ArgumentNullException.ThrowIfNull(start);
        var registration = Registration
            ?? throw new InvalidOperationException(
                "The dispatch request does not represent a Lifecycle Execution.");
        if (start.LifecycleExecutionRef.Id != registration.ExecutionId
            || start.LifecycleExecutionRef.DefinitionDigest
            != LifecycleExecutionDefinitionDigest.Calculate(registration.Definition)
            || start.DeadlineUtc != registration.DeadlineUtc
            || start.StartedAtUtc != registration.StartedAtUtc)
        {
            throw new ArgumentException(
                "Lifecycle Execution start binding does not match the pending registration.",
                nameof(start));
        }
        if (RequiredStart is not null
            && (start.Project != RequiredStart.Project
                || start.Host.Process != RequiredStart.Host.Process
                || start.Host.EditorInstanceId
                    != RequiredStart.Host.EditorInstanceId
                || start.Host.FirstEndpointRegistrationGenerationId
                    != RequiredStart.Host.FirstEndpointRegistrationGenerationId
                || start.StartedGeneration != RequiredStart.StartedGeneration))
        {
            throw new ArgumentException(
                "Lifecycle Execution start binding does not belong to the required original project and Unity host.",
                nameof(start));
        }

        EnsureKindMatchesMethod(Method, registration.Definition.Kind);
        return lifecyclePayloadFactory!(start);
    }

    /// <summary>
    /// Classifies a provider-returned start that conflicts with the authoritative reconnect binding.
    /// </summary>
    /// <returns>
    /// The common Lifecycle Execution mismatch code, or <see langword="null" /> when the provider
    /// returned the required original project, host, and first accepted generation.
    /// </returns>
    public UcliCode? GetRequiredStartMismatchCode (
        LifecycleExecutionStartBinding candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (RequiredStart is null)
        {
            return null;
        }

        if (candidate.Project != RequiredStart.Project)
        {
            return LifecycleExecutionErrorCodes.ProjectMismatch;
        }

        if (candidate.Host.Process != RequiredStart.Host.Process
            || candidate.Host.EditorInstanceId
                != RequiredStart.Host.EditorInstanceId)
        {
            return LifecycleExecutionErrorCodes.HostMismatch;
        }

        return candidate.Host.FirstEndpointRegistrationGenerationId
                != RequiredStart.Host.FirstEndpointRegistrationGenerationId
            || candidate.StartedGeneration != RequiredStart.StartedGeneration
                ? LifecycleExecutionErrorCodes.GenerationMismatch
                : null;
    }

    private static void EnsureKindMatchesMethod (
        UnityIpcMethod method,
        LifecycleExecutionKind kind)
    {
        var expectedKind = method switch
        {
            UnityIpcMethod.Refresh => LifecycleExecutionKind.Refresh,
            UnityIpcMethod.Compile => LifecycleExecutionKind.Compile,
            UnityIpcMethod.PlayEnter => LifecycleExecutionKind.PlayEnter,
            UnityIpcMethod.PlayExit => LifecycleExecutionKind.PlayExit,
            _ => throw new ArgumentOutOfRangeException(
                nameof(method),
                method,
                "Method is not a Lifecycle Execution action."),
        };
        if (kind != expectedKind)
        {
            throw new ArgumentException(
                $"Lifecycle Execution kind '{kind}' does not match method '{method}'.",
                nameof(kind));
        }
    }
}
