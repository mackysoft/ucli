namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Projects a previously established Lifecycle Execution identity into the reconnectable
/// publishing state retained when terminal delivery cannot be trusted.
/// </summary>
internal static class LifecycleExecutionFailureReferenceProjection
{
    public static RecoveryExecutionRef CreatePublishing (
        ExecutionRef establishedReference)
    {
        if (establishedReference is null)
        {
            throw new ArgumentNullException(nameof(establishedReference));
        }
        if (establishedReference.StatusLocator is null)
        {
            throw new ArgumentException(
                "A Lifecycle Execution publishing failure must retain its durable status locator.",
                nameof(establishedReference));
        }

        return new RecoveryExecutionRef(
            establishedReference.Kind,
            establishedReference.Id,
            establishedReference.DefinitionDigest,
            new ExecutionState(
                TextVocabulary.GetText(
                    LifecycleExecutionState.Publishing)),
            establishedReference.StatusLocator);
    }
}
