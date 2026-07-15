namespace MackySoft.Ucli.Application.Shared.Execution;

/// <summary> Resolves application outcomes from machine-readable failure codes. </summary>
internal static class ApplicationFailureOutcomeResolver
{
    /// <summary> Resolves the application outcome represented by one failure collection. </summary>
    /// <param name="failures"> The classified failures. </param>
    /// <returns> The application outcome represented by the collection. </returns>
    public static ApplicationOutcome Resolve (IReadOnlyList<ApplicationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        if (failures.Count == 0)
        {
            return ApplicationOutcome.Success;
        }

        var firstFailure = failures[0]
            ?? throw new ArgumentException("Failure collection must not contain null entries.", nameof(failures));
        var outcome = firstFailure.Outcome;
        for (var i = 1; i < failures.Count; i++)
        {
            var failure = failures[i];
            if (failure == null)
            {
                throw new ArgumentException("Failure collection must not contain null entries.", nameof(failures));
            }

            if (failure.Outcome != outcome)
            {
                return ApplicationOutcome.ToolError;
            }
        }

        return outcome;
    }
}
