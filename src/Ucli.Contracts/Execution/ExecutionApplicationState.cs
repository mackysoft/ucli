namespace MackySoft.Ucli.Contracts.Execution;

/// <summary> Identifies how much of a requested execution is known to have been applied. </summary>
[VocabularyDefinition]
public enum ExecutionApplicationState
{
    /// <summary> Indicates that the requested execution is known not to have been applied. </summary>
    [VocabularyText("notApplied")]
    NotApplied = 1,

    /// <summary> Indicates that the requested execution is known to have been applied. </summary>
    [VocabularyText("applied")]
    Applied = 2,

    /// <summary> Indicates that only a confirmed part of the requested execution was applied. </summary>
    [VocabularyText("partiallyApplied")]
    PartiallyApplied = 3,

    /// <summary> Indicates that reliable evidence cannot determine whether the requested execution was applied. </summary>
    [VocabularyText("indeterminate")]
    Indeterminate = 4,

    /// <summary> Indicates that no reliable result envelope exists for the requested execution. </summary>
    [VocabularyText("unknown")]
    Unknown = 5,
}

internal static class ExecutionApplicationStateSemantics
{
    internal static bool IsOperationState (
        ExecutionApplicationState applicationState)
    {
        return TextVocabulary.IsDefined(applicationState)
            && applicationState
                != ExecutionApplicationState.PartiallyApplied;
    }

    internal static ExecutionApplicationState RequireOperationState (
        ExecutionApplicationState applicationState,
        string parameterName)
    {
        if (!IsOperationState(applicationState))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                applicationState,
                "Operation application state must be defined and cannot be partially applied.");
        }

        return applicationState;
    }
}
