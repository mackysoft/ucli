namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Provides the allowed property vocabulary for public <c>execute</c> steps. </summary>
internal static class IpcExecuteStepPropertyPolicy
{
    private static readonly HashSet<string> AnyStepProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "id",
        "op",
        "args",
        "on",
        "select",
        "actions",
        "commit",
    };

    private static readonly HashSet<string> OpStepProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "id",
        "op",
        "args",
    };

    private static readonly HashSet<string> EditStepProperties = new(StringComparer.Ordinal)
    {
        "kind",
        "id",
        "on",
        "select",
        "actions",
        "commit",
    };

    public static HashSet<string> ResolveAllowedStepProperties (IpcExecuteStepKind? stepKind)
    {
        if (stepKind == IpcExecuteStepKind.Op)
        {
            return OpStepProperties;
        }

        return stepKind == IpcExecuteStepKind.Edit
            ? EditStepProperties
            : AnyStepProperties;
    }
}
