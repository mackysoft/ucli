namespace MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;

/// <summary> Provides invariant checks for request service result models. </summary>
internal static class RequestServiceResultInvariants
{
    private static readonly IReadOnlyList<ApplicationFailure> EmptyFailureList = Array.AsReadOnly(Array.Empty<ApplicationFailure>());

    /// <summary> Gets the canonical empty failure collection for successful results. </summary>
    public static IReadOnlyList<ApplicationFailure> EmptyErrors => EmptyFailureList;

    /// <summary> Validates one failure result and returns an immutable failure snapshot. </summary>
    public static IReadOnlyList<ApplicationFailure> RequireFailureErrors (IReadOnlyList<ApplicationFailure> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (errors.Count == 0)
        {
            throw new ArgumentException("Failure errors must not be empty.", nameof(errors));
        }

        var snapshot = new ApplicationFailure[errors.Count];
        for (var i = 0; i < errors.Count; i++)
        {
            var error = errors[i];
            if (error == null)
            {
                throw new ArgumentException("Failure errors must not contain null entries.", nameof(errors));
            }

            snapshot[i] = error;
        }

        return Array.AsReadOnly(snapshot);
    }
}
