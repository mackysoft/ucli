namespace MackySoft.Ucli.Contracts;

internal static class LifecycleExecutionErrorCodeDescriptors
{
    private static readonly UcliCommand[] LifecycleCommands =
    [
        UcliCommandIds.Refresh,
        UcliCommandIds.Compile,
        UcliCommandIds.PlayEnter,
        UcliCommandIds.PlayExit,
    ];

    private static readonly string[] LifecycleInspectTargets =
    [
        "errors[].code",
        "errors[].message",
        "payload.lifecycleExecutionRef",
        "payload.applicationState",
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        Create(
            LifecycleExecutionErrorCodes.DefinitionConflict,
            "An execution identifier is already bound to a different definition.",
            "The supplied ExecutionRef or execution identifier does not match the immutable action definition recorded for that logical execution.",
            ["reconnectValidation", "startAdmission"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            UcliErrorRetryClass.No,
            "Use the original matching ExecutionRef, or start a new execution without reusing its identifier."),

        Create(
            LifecycleExecutionErrorCodes.ProjectMismatch,
            "The selected project does not own the Lifecycle Execution.",
            "The persisted project identity differs from the guarded project selected for this request.",
            ["reconnectValidation", "terminalization"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            UcliErrorRetryClass.No,
            "Reconnect from the project identified by the original execution reference."),

        Create(
            LifecycleExecutionErrorCodes.HostMismatch,
            "The available Unity Editor host does not match the fixed execution host.",
            "Lifecycle Execution reconnection could not prove the exact process generation and Editor instance fixed before side effects began.",
            ["providerReconnect", "terminalization"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            UcliErrorRetryClass.ContextDependent,
            "Inspect the execution reference and registered daemon state before reconnecting again."),

        Create(
            LifecycleExecutionErrorCodes.GenerationMismatch,
            "The observed endpoint or Editor generation is not a proven successor.",
            "The provider returned an endpoint registration or Editor generation that regressed or did not descend from the execution start.",
            ["providerReconnect", "terminalization"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            UcliErrorRetryClass.ContextDependent,
            "Inspect the Terminal Record or reconnect through the original ExecutionRef after Unity settles."),

        Create(
            LifecycleExecutionErrorCodes.DeadlineExceeded,
            "The immutable Lifecycle Execution deadline was reached.",
            "The logical execution reached the deadline fixed in its durable Start Record, independently from any caller wait timeout.",
            ["execution", "terminalization"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            UcliErrorRetryClass.ContextDependent,
            "Inspect applicationState and the Terminal Record before deciding whether to start a new execution."),

        Create(
            LifecycleExecutionErrorCodes.UnityExited,
            "The fixed Unity Editor process exited before completion.",
            "The exact process generation recorded for the Lifecycle Execution was confirmed dead and no action result established completion.",
            ["providerReconnect", "terminalization"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            UcliErrorRetryClass.ContextDependent,
            "Inspect applicationState and the Terminal Record, then start a new execution only if the resulting project state permits it."),

        Create(
            LifecycleExecutionErrorCodes.TerminalPublicationFailed,
            "The immutable Terminal Record could not be published or reverified.",
            "Terminal convergence did not produce a safe terminal ExecutionRef; the returned active or recovery reference remains reconnectable.",
            ["terminalPublication", "reconnectValidation"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            UcliErrorRetryClass.Yes,
            "Reconnect with the returned ExecutionRef so uCLI can recover and reverify Terminal Record publication."),
    ];

    private static UcliErrorDescriptor Create (
        UcliCode code,
        string summary,
        string meaning,
        IReadOnlyList<string> possiblePhases,
        bool? impliesNotApplied,
        bool mayBeIndeterminate,
        UcliErrorRetryClass safeToRetry,
        string nextAction)
    {
        return UcliErrorDescriptorFactory.Create(
            code,
            category: "lifecycleExecution",
            summary,
            meaning,
            appliesTo: LifecycleCommands,
            possiblePhases,
            impliesNotApplied,
            mayBeIndeterminate,
            safeToRetry,
            inspect: LifecycleInspectTargets,
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: nextAction),
            ],
            relatedCodes: LifecycleExecutionErrorCodes.AllExcept(code));
    }
}
