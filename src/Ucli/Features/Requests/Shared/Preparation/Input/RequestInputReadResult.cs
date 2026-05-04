using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Shared.Preparation.Input;

/// <summary> Represents the result of reading JSON request input. </summary>
/// <param name="Json"> The raw JSON request content, or <see langword="null" /> when reading fails. </param>
/// <param name="Error"> The structured read error, or <see langword="null" /> on success. </param>
internal sealed record RequestInputReadResult (
    string? Json,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether request input was read successfully. </summary>
    public bool IsSuccess => Json is not null && Error is null;

    /// <summary> Creates a successful request-input read result. </summary>
    /// <param name="json"> The raw JSON request content. </param>
    /// <returns> The successful read result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="json" /> is empty or whitespace. </exception>
    public static RequestInputReadResult Success (string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Request JSON must not be empty.", nameof(json));
        }

        return new RequestInputReadResult(
            Json: json,
            Error: null);
    }

    /// <summary> Creates a failed request-input read result. </summary>
    /// <param name="error"> The structured read error. </param>
    /// <returns> The failed read result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static RequestInputReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return new RequestInputReadResult(
            Json: null,
            Error: error);
    }
}
