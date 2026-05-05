using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;

/// <summary> Represents the result of normalizing user-authored request JSON. </summary>
internal sealed class UserRequestJsonNormalizationResult
{
    private UserRequestJsonNormalizationResult (
        string? requestJson,
        ExecutionError? error)
    {
        RequestJson = requestJson;
        Error = error;
    }

    /// <summary> Gets a value indicating whether normalization succeeded. </summary>
    public bool IsSuccess => Error is null;

    /// <summary> Gets the normalized request JSON when normalization succeeded. </summary>
    public string? RequestJson { get; }

    /// <summary> Gets the normalization error when normalization failed. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Creates a successful normalization result. </summary>
    /// <param name="requestJson"> The normalized request JSON. </param>
    /// <returns> The successful result. </returns>
    public static UserRequestJsonNormalizationResult Success (string requestJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        return new UserRequestJsonNormalizationResult(requestJson, null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    public static UserRequestJsonNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new UserRequestJsonNormalizationResult(null, error);
    }
}
