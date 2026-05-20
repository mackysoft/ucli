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
        string sourceKind,
        bool playModeMutation,
        string? commit,
        bool persistenceExpected,
        string expectedPostState)
    {
        ThrowIfNullOrWhiteSpace(operationName, nameof(operationName));
        ThrowIfNullOrWhiteSpace(sourceKind, nameof(sourceKind));
        ThrowIfNullOrWhiteSpace(expectedPostState, nameof(expectedPostState));

        return sourceKind switch
        {
            IpcExecutePostReadSourceKindNames.Edit =>
                string.Equals(operationName, EditOperationName, StringComparison.Ordinal)
                && IsKnownPostReadCommit(commit)
                && (string.Equals(commit, IpcExecutePostReadCommitNames.None, StringComparison.Ordinal) || persistenceExpected)
                && ((playModeMutation
                        && !persistenceExpected
                        && string.Equals(commit, IpcExecutePostReadCommitNames.None, StringComparison.Ordinal)
                        && string.Equals(expectedPostState, IpcExecuteExpectedPostStateNames.Unavailable, StringComparison.Ordinal))
                    || (!playModeMutation
                        && string.Equals(expectedPostState, IpcExecuteExpectedPostStateNames.Deterministic, StringComparison.Ordinal))),
            IpcExecutePostReadSourceKindNames.Operation =>
                !string.Equals(operationName, EditOperationName, StringComparison.Ordinal)
                && !string.Equals(operationName, UcliPrimitiveOperationNames.ProjectRefresh, StringComparison.Ordinal)
                && !playModeMutation
                && commit is null
                && string.Equals(expectedPostState, IpcExecuteExpectedPostStateNames.Unavailable, StringComparison.Ordinal),
            IpcExecutePostReadSourceKindNames.Refresh =>
                string.Equals(operationName, UcliPrimitiveOperationNames.ProjectRefresh, StringComparison.Ordinal)
                && !playModeMutation
                && commit is null
                && persistenceExpected
                && string.Equals(expectedPostState, IpcExecuteExpectedPostStateNames.Unavailable, StringComparison.Ordinal),
            _ => false,
        };
    }

    /// <summary> Returns whether the source fact can support deterministic post-mutation observation claims. </summary>
    /// <param name="sourceKind"> The post-read source kind. </param>
    /// <param name="expectedPostState"> The expected post-state availability. </param>
    /// <returns> <see langword="true" /> when the source can support deterministic post-mutation claims. </returns>
    public static bool IsDeterministicMutationSource (
        string sourceKind,
        string expectedPostState)
    {
        ThrowIfNullOrWhiteSpace(sourceKind, nameof(sourceKind));
        ThrowIfNullOrWhiteSpace(expectedPostState, nameof(expectedPostState));

        return string.Equals(sourceKind, IpcExecutePostReadSourceKindNames.Edit, StringComparison.Ordinal)
            && string.Equals(expectedPostState, IpcExecuteExpectedPostStateNames.Deterministic, StringComparison.Ordinal);
    }

    private static void ThrowIfNullOrWhiteSpace (
        string? value,
        string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be null or whitespace.", paramName);
        }
    }

    private static bool IsKnownPostReadCommit (string? commit)
    {
        return commit is IpcExecutePostReadCommitNames.None
            or IpcExecutePostReadCommitNames.Context
            or IpcExecutePostReadCommitNames.Project;
    }
}
