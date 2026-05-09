namespace MackySoft.Ucli.Contracts;

internal static class UcliErrorCodeDescriptorFactory
{
    private static readonly IReadOnlyList<UcliCommand> EmptyCommands = Array.Empty<UcliCommand>();

    private static readonly IReadOnlyList<string> EmptyStrings = Array.Empty<string>();

    private static readonly IReadOnlyList<UcliErrorNextActionDescriptor> EmptyActions = Array.Empty<UcliErrorNextActionDescriptor>();

    private static readonly IReadOnlyList<UcliErrorCode> EmptyCodes = Array.Empty<UcliErrorCode>();

    public static UcliErrorCodeDescriptor Create (
        UcliErrorCode code,
        string category,
        string summary,
        string meaning,
        IReadOnlyList<UcliCommand>? appliesTo,
        IReadOnlyList<string>? possiblePhases,
        bool? impliesNotApplied,
        bool mayBeIndeterminate,
        string safeToRetry,
        IReadOnlyList<string>? inspect,
        IReadOnlyList<UcliErrorNextActionDescriptor>? nextActions,
        IReadOnlyList<UcliErrorCode>? relatedCodes)
    {
        return new UcliErrorCodeDescriptor(
            Code: code,
            Category: category,
            Summary: summary,
            Meaning: meaning,
            AppliesTo: appliesTo ?? EmptyCommands,
            PossiblePhases: possiblePhases ?? EmptyStrings,
            ExecutionSemantics: new UcliErrorExecutionSemantics(
                ImpliesNotApplied: impliesNotApplied,
                MayBeIndeterminate: mayBeIndeterminate,
                SafeToRetry: safeToRetry),
            Inspect: inspect ?? EmptyStrings,
            NextActions: nextActions ?? EmptyActions,
            RelatedCodes: relatedCodes ?? EmptyCodes);
    }
}
