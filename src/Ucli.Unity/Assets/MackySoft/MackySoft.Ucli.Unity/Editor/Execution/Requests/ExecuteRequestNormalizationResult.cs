using System;

namespace MackySoft.Ucli.Unity.Execution.Requests;

/// <summary> Represents the result of execute-request normalization. </summary>
/// <param name="Request"> The normalized request on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The normalization error on failure; otherwise <see langword="null" />. </param>
internal sealed record ExecuteRequestNormalizationResult (
    NormalizedExecuteRequest? Request,
    ExecuteRequestNormalizationError? Error)
{
    /// <summary> Gets a value indicating whether normalization succeeded. </summary>
    public bool IsSuccess => Request is not null && Error is null;

    /// <summary> Creates a successful normalization result. </summary>
    /// <param name="request"> The normalized request. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    public static ExecuteRequestNormalizationResult Success (NormalizedExecuteRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new ExecuteRequestNormalizationResult(
            Request: request,
            Error: null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    /// <param name="error"> The normalization error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static ExecuteRequestNormalizationResult Failure (ExecuteRequestNormalizationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new ExecuteRequestNormalizationResult(
            Request: null,
            Error: error);
    }
}
