namespace MackySoft.Ucli.Contracts;

internal static class UcliErrorDescriptorFactory
{
    private static readonly IReadOnlyList<UcliCommand> EmptyCommands = Array.Empty<UcliCommand>();

    private static readonly IReadOnlyList<string> EmptyStrings = Array.Empty<string>();

    private static readonly IReadOnlyList<UcliErrorNextActionDescriptor> EmptyActions = Array.Empty<UcliErrorNextActionDescriptor>();

    private static readonly IReadOnlyList<UcliCode> EmptyCodes = Array.Empty<UcliCode>();

    public static UcliErrorDescriptor Create (
        UcliCode code,
        string category,
        string summary,
        string meaning,
        IReadOnlyList<UcliCommand>? appliesTo,
        IReadOnlyList<string>? possiblePhases,
        bool? impliesNotApplied,
        bool mayBeIndeterminate,
        UcliErrorRetryClass safeToRetry,
        IReadOnlyList<string>? inspect,
        IReadOnlyList<UcliErrorNextActionDescriptor>? nextActions,
        IReadOnlyList<UcliCode>? relatedCodes)
    {
        var lists = ResolveLists(appliesTo, possiblePhases, inspect, nextActions, relatedCodes);
        return new UcliErrorDescriptor(
            Code: code,
            Category: category,
            Summary: summary,
            Meaning: meaning,
            AppliesTo: lists.AppliesTo,
            PossiblePhases: lists.PossiblePhases,
            ExecutionSemantics: CreateExecutionSemantics(impliesNotApplied, mayBeIndeterminate, safeToRetry),
            Inspect: lists.Inspect,
            NextActions: lists.NextActions,
            RelatedCodes: lists.RelatedCodes);
    }

    private static UcliErrorExecutionSemantics CreateExecutionSemantics (
        bool? impliesNotApplied,
        bool mayBeIndeterminate,
        UcliErrorRetryClass safeToRetry)
    {
        return new UcliErrorExecutionSemantics(
            ImpliesNotApplied: impliesNotApplied,
            MayBeIndeterminate: mayBeIndeterminate,
            SafeToRetry: safeToRetry);
    }

    private static DescriptorLists ResolveLists (
        IReadOnlyList<UcliCommand>? appliesTo,
        IReadOnlyList<string>? possiblePhases,
        IReadOnlyList<string>? inspect,
        IReadOnlyList<UcliErrorNextActionDescriptor>? nextActions,
        IReadOnlyList<UcliCode>? relatedCodes)
    {
        return new DescriptorLists(
            AppliesTo: appliesTo ?? EmptyCommands,
            PossiblePhases: possiblePhases ?? EmptyStrings,
            Inspect: inspect ?? EmptyStrings,
            NextActions: nextActions ?? EmptyActions,
            RelatedCodes: relatedCodes ?? EmptyCodes);
    }

    private readonly record struct DescriptorLists (
        IReadOnlyList<UcliCommand> AppliesTo,
        IReadOnlyList<string> PossiblePhases,
        IReadOnlyList<string> Inspect,
        IReadOnlyList<UcliErrorNextActionDescriptor> NextActions,
        IReadOnlyList<UcliCode> RelatedCodes);
}
