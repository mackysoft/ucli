namespace MackySoft.Ucli.Contracts;

internal static class ReadIndexErrorCodeDescriptors
{
    private static readonly UcliCommand[] ReadIndexCommands =
    [
        UcliCommandIds.Validate,
        UcliCommandIds.Plan,
        UcliCommandIds.Resolve,
        UcliCommandIds.Query,
        UcliCommandIds.Ops,
    ];

    public static IReadOnlyList<UcliErrorDescriptor> All { get; } =
    [
        UcliErrorDescriptorFactory.Create(
            code: ReadIndexErrorCodes.ReadIndexBootstrapFailed,
            category: "readIndex",
            summary: "Read-index bootstrap cannot be completed.",
            meaning: "uCLI could not create or load the read-index required for static request analysis or selector resolution.",
            appliesTo: ReadIndexCommands,
            possiblePhases: ["readIndexBootstrap"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.readIndex", "status", UcliErrorInspectTargets.DaemonErrorLogsCommand, UcliErrorInspectTargets.UnityErrorLogsCommand],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Inspect read-index diagnostics and refresh or regenerate the index before retrying."),
            ],
            relatedCodes:
            [
                ReadIndexErrorCodes.ReadIndexFormatInvalid,
                ReadIndexErrorCodes.ReadIndexFreshRequired,
            ]),

        UcliErrorDescriptorFactory.Create(
            code: ReadIndexErrorCodes.ReadIndexFormatInvalid,
            category: "readIndex",
            summary: "Read-index files are malformed.",
            meaning: "The persisted read-index exists but cannot be parsed as the expected contract.",
            appliesTo: ReadIndexCommands,
            possiblePhases: ["readIndexLoad"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.readIndex", "errors[].message"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Regenerate the read-index, then rerun the command."),
            ],
            relatedCodes: [ReadIndexErrorCodes.ReadIndexBootstrapFailed]),

        UcliErrorDescriptorFactory.Create(
            code: ReadIndexErrorCodes.ReadIndexFreshRequired,
            category: "readIndex",
            summary: "The request requires a fresh read-index.",
            meaning: "The command cannot rely on stale indexed project state for this request.",
            appliesTo: ReadIndexCommands,
            possiblePhases: ["readIndexFreshness"],
            impliesNotApplied: true,
            mayBeIndeterminate: false,
            safeToRetry: UcliErrorRetryClassValues.ContextDependent,
            inspect: ["payload.readIndex", "payload.readIndex.freshness", "status"],
            nextActions:
            [
                new UcliErrorNextActionDescriptor(
                    When: null,
                    Action: "Refresh the read-index and retry after freshness is reported as fresh."),
            ],
            relatedCodes: [ReadIndexErrorCodes.ReadIndexBootstrapFailed]),
    ];
}
