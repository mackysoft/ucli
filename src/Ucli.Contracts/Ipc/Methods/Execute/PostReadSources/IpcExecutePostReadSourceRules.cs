using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines semantic rules for post-read source facts carried by execute results. </summary>
public static class IpcExecutePostReadSourceRules
{
    /// <summary> Gets the public operation name used for normalized edit steps. </summary>
    public const string EditOperationName = "edit";

    /// <summary> Returns whether one source fact is compatible with the matching operation result. </summary>
    /// <param name="operationName"> The matching <c>opResults[].op</c> value. </param>
    /// <param name="sourceKind"> The post-read source kind. </param>
    /// <param name="playModeMutation"> Whether the source mutates Play Mode state. </param>
    /// <param name="commit"> The edit commit kind, or <see langword="null" /> for non-edit sources. </param>
    /// <param name="persistenceExpected"> Whether the source is expected to touch persisted units when it changes. </param>
    /// <param name="expectedPostState"> The expected post-state availability. </param>
    /// <returns> <see langword="true" /> when the values match the current source-fact contract. </returns>
    public static bool IsCompatibleWithOperation (
        string operationName,
        IpcExecutePostReadSourceKind sourceKind,
        bool playModeMutation,
        IpcExecutePostReadCommit? commit,
        bool persistenceExpected,
        IpcExecuteExpectedPostState expectedPostState)
    {
        ContractArgumentGuard.RequireValue(operationName, nameof(operationName));
        if (!ContractLiteralCodec.IsDefined(sourceKind)
            || !ContractLiteralCodec.IsDefined(expectedPostState)
            || (commit.HasValue && !ContractLiteralCodec.IsDefined(commit.Value)))
        {
            return false;
        }

        return sourceKind switch
        {
            IpcExecutePostReadSourceKind.Edit => IsCompatibleEditSource(operationName, playModeMutation, commit, persistenceExpected, expectedPostState),
            IpcExecutePostReadSourceKind.Operation => IsCompatibleOperationSource(operationName, playModeMutation, commit, expectedPostState),
            IpcExecutePostReadSourceKind.Refresh => IsCompatibleRefreshSource(operationName, playModeMutation, commit, persistenceExpected, expectedPostState),
            _ => false,
        };
    }

    /// <summary> Returns whether the source fact can support deterministic post-mutation observation claims. </summary>
    /// <param name="sourceKind"> The post-read source kind. </param>
    /// <param name="expectedPostState"> The expected post-state availability. </param>
    /// <returns> <see langword="true" /> when the source can support deterministic post-mutation claims. </returns>
    public static bool IsDeterministicMutationSource (
        IpcExecutePostReadSourceKind sourceKind,
        IpcExecuteExpectedPostState expectedPostState)
    {
        return sourceKind == IpcExecutePostReadSourceKind.Edit
            && expectedPostState == IpcExecuteExpectedPostState.Deterministic;
    }

    private static bool IsCompatibleEditSource (
        string operationName,
        bool playModeMutation,
        IpcExecutePostReadCommit? commit,
        bool persistenceExpected,
        IpcExecuteExpectedPostState expectedPostState)
    {
        return string.Equals(operationName, EditOperationName, StringComparison.Ordinal)
            && commit.HasValue
            && (playModeMutation
                ? IsCompatiblePlayModeEditSource(commit, expectedPostState)
                : IsCompatibleDeterministicEditSource(commit, persistenceExpected, expectedPostState));
    }

    private static bool IsCompatibleOperationSource (
        string operationName,
        bool playModeMutation,
        IpcExecutePostReadCommit? commit,
        IpcExecuteExpectedPostState expectedPostState)
    {
        return !string.Equals(operationName, EditOperationName, StringComparison.Ordinal)
            && !string.Equals(operationName, UcliPrimitiveOperationNames.ProjectRefresh, StringComparison.Ordinal)
            && !playModeMutation
            && commit is null
            && expectedPostState == IpcExecuteExpectedPostState.Unavailable;
    }

    private static bool IsCompatibleRefreshSource (
        string operationName,
        bool playModeMutation,
        IpcExecutePostReadCommit? commit,
        bool persistenceExpected,
        IpcExecuteExpectedPostState expectedPostState)
    {
        return string.Equals(operationName, UcliPrimitiveOperationNames.ProjectRefresh, StringComparison.Ordinal)
            && !playModeMutation
            && commit is null
            && persistenceExpected
            && expectedPostState == IpcExecuteExpectedPostState.Unavailable;
    }

    private static bool IsCompatiblePlayModeEditSource (
        IpcExecutePostReadCommit? commit,
        IpcExecuteExpectedPostState expectedPostState)
    {
        return commit == IpcExecutePostReadCommit.None
            && expectedPostState == IpcExecuteExpectedPostState.Unavailable;
    }

    private static bool IsCompatibleDeterministicEditSource (
        IpcExecutePostReadCommit? commit,
        bool persistenceExpected,
        IpcExecuteExpectedPostState expectedPostState)
    {
        return (commit == IpcExecutePostReadCommit.None || persistenceExpected)
            && expectedPostState == IpcExecuteExpectedPostState.Deterministic;
    }
}
