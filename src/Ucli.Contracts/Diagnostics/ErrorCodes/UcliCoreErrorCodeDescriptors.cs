namespace MackySoft.Ucli.Contracts;

internal static class UcliCoreErrorCodeDescriptors
{
    private static readonly UcliCommand[] AllCommands = UcliPublicCommandCatalog.KnownCommands.ToArray();

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: UcliCoreErrorCodes.InvalidArgument,
            category: "input",
            summary: "The command or request arguments are invalid.",
            meaning: "uCLI rejected input before executing the requested operation because it violates command-line, JSON, or contract constraints.",
            appliesTo: AllCommands,
            possiblePhases: ["argumentParsing", "staticValidation"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClass.No,
            inspect: ["errors[].code", "errors[].message", "errors[].opId"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Fix the invalid argument or request field, then run the command again."),
            ],
            relatedCodes: null),

        UcliErrorDescriptorFactory.Create(
            code: UcliCoreErrorCodes.NotInitialized,
            category: "workspace",
            summary: "Required uCLI workspace initialization has not been completed.",
            meaning: "The command requires repository-local uCLI state or generated metadata that is not available yet.",
            appliesTo: AllCommands,
            possiblePhases: ["preflight"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClass.No,
            inspect: ["status", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Run the initialization command required by the message, then retry the original command."),
            ],
            relatedCodes: [UcliCoreErrorCodes.InvalidArgument]),

        UcliErrorDescriptorFactory.Create(
            code: UcliCoreErrorCodes.CommandNotImplemented,
            category: "command",
            summary: "The selected command is recognized but not implemented.",
            meaning: "The current uCLI build contains the command surface but does not provide an executable use case for it.",
            appliesTo: AllCommands,
            possiblePhases: ["dispatch"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClass.No,
            inspect: ["command", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Use an implemented command or update uCLI to a build that includes this command."),
            ],
            relatedCodes: [UcliCoreErrorCodes.InternalError]),

        UcliErrorDescriptorFactory.Create(
            code: UcliCoreErrorCodes.InternalError,
            category: "internal",
            summary: "An unexpected internal failure occurred.",
            meaning: "uCLI reached an unplanned failure path. The exact application state depends on the command phase and returned payload.",
            appliesTo: AllCommands,
            possiblePhases: ["dispatch", "preflight", "execution", "projection"],
            impliesNotApplied: null,
            mayBeIndeterminate: true,
            safeToRetry: UcliErrorRetryClass.Unknown,
            inspect: ["status", "errors[].code", "errors[].message", "payload.requestId", "payload.opResults", "payload.readPostcondition", UcliErrorInspectTargets.DaemonErrorLogsCommand, UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect the full result and relevant logs before retrying or changing project state."),
            ],
            relatedCodes: null),
    ];
}
