namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Classifies whether a supplied start binding denotes the exact durable Lifecycle Execution.
/// </summary>
internal static class LifecycleExecutionStartBindingMatcher
{
    internal static LifecycleExecutionStartBindingMatch Match (
        LifecycleExecutionStartBinding requested,
        LifecycleExecutionStartBinding established)
    {
        if (requested is null)
        {
            throw new ArgumentNullException(nameof(requested));
        }
        if (established is null)
        {
            throw new ArgumentNullException(nameof(established));
        }

        var requestedReference = requested.LifecycleExecutionRef;
        var establishedReference = established.LifecycleExecutionRef;
        if (requestedReference.Id != establishedReference.Id
            || requestedReference.Kind != establishedReference.Kind
            || requestedReference.DefinitionDigest
                != establishedReference.DefinitionDigest
            || requestedReference.StatusLocator
                != establishedReference.StatusLocator)
        {
            return LifecycleExecutionStartBindingMatch.DefinitionConflict;
        }

        if (requested.Project != established.Project)
        {
            return LifecycleExecutionStartBindingMatch.ProjectMismatch;
        }

        if (requested.Host.Process != established.Host.Process
            || requested.Host.EditorInstanceId
                != established.Host.EditorInstanceId
            || requested.Host.FirstEndpointRegistrationGenerationId
                != established.Host.FirstEndpointRegistrationGenerationId
            || requested.Host.CurrentEndpointRegistrationGenerationId
                != established.Host.CurrentEndpointRegistrationGenerationId)
        {
            return LifecycleExecutionStartBindingMatch.HostMismatch;
        }

        return requested.StartedGeneration != established.StartedGeneration
                || requested.DeadlineUtc != established.DeadlineUtc
                || requested.StartedAtUtc != established.StartedAtUtc
            ? LifecycleExecutionStartBindingMatch.GenerationMismatch
            : LifecycleExecutionStartBindingMatch.Exact;
    }

    internal static UcliCode GetMismatchErrorCode (
        LifecycleExecutionStartBindingMatch match)
    {
        return match switch
        {
            LifecycleExecutionStartBindingMatch.DefinitionConflict =>
                LifecycleExecutionErrorCodes.DefinitionConflict,
            LifecycleExecutionStartBindingMatch.ProjectMismatch =>
                LifecycleExecutionErrorCodes.ProjectMismatch,
            LifecycleExecutionStartBindingMatch.HostMismatch =>
                LifecycleExecutionErrorCodes.HostMismatch,
            LifecycleExecutionStartBindingMatch.GenerationMismatch =>
                LifecycleExecutionErrorCodes.GenerationMismatch,
            LifecycleExecutionStartBindingMatch.Exact =>
                throw new ArgumentException(
                    "An exact Lifecycle Execution start binding has no mismatch error code.",
                    nameof(match)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(match),
                match,
                "Unsupported Lifecycle Execution start binding match."),
        };
    }
}

/// <summary>
/// Identifies which durable start fact prevents an exact Lifecycle Execution match.
/// </summary>
internal enum LifecycleExecutionStartBindingMatch
{
    Exact = 1,
    DefinitionConflict,
    ProjectMismatch,
    HostMismatch,
    GenerationMismatch,
}
