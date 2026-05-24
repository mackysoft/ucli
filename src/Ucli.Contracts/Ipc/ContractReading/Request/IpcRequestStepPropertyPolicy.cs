namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary> Provides the allowed property vocabulary for public request steps. </summary>
internal static class IpcRequestStepPropertyPolicy
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

    public static HashSet<string> ResolveAllowedStepProperties (IpcRequestStepKind? stepKind)
    {
        if (stepKind == IpcRequestStepKind.Op)
        {
            return OpStepProperties;
        }

        return stepKind == IpcRequestStepKind.Edit
            ? EditStepProperties
            : AnyStepProperties;
    }
}
