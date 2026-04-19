using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Shared.Preparation;

/// <summary> Represents the result of preparing one request for command execution. </summary>
/// <param name="PreparedRequest"> The prepared request context on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured preparation error on failure; otherwise <see langword="null" />. </param>
internal sealed record RequestPreparationResult (
    PreparedRequestContext? PreparedRequest,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether request preparation succeeded. </summary>
    public bool IsSuccess => PreparedRequest is not null && Error is null;

    /// <summary> Creates a successful request-preparation result. </summary>
    /// <param name="preparedRequest"> The prepared request context. </param>
    /// <returns> The successful result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="preparedRequest" /> is <see langword="null" />. </exception>
    public static RequestPreparationResult Success (PreparedRequestContext preparedRequest)
    {
        ArgumentNullException.ThrowIfNull(preparedRequest);
        return new RequestPreparationResult(preparedRequest, null);
    }

    /// <summary> Creates a failed request-preparation result. </summary>
    /// <param name="error"> The structured preparation error. </param>
    /// <returns> The failed result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static RequestPreparationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new RequestPreparationResult(null, error);
    }
}