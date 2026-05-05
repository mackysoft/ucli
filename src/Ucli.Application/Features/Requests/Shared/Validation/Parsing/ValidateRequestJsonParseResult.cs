using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;

/// <summary> Represents the result of parsing request JSON into <see cref="ValidateRequest" />. </summary>
/// <param name="Request"> The parsed request model on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The parse error on failure; otherwise <see langword="null" />. </param>
internal sealed record ValidateRequestJsonParseResult (
    ValidateRequest? Request,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether parsing succeeded. </summary>
    public bool IsSuccess => Request is not null && Error is null;

    /// <summary> Creates a successful parse result. </summary>
    /// <param name="request"> The parsed request model. </param>
    /// <returns> The successful parse result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> is <see langword="null" />. </exception>
    public static ValidateRequestJsonParseResult Success (ValidateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ValidateRequestJsonParseResult(request, null);
    }

    /// <summary> Creates a failed parse result. </summary>
    /// <param name="error"> The parse error. </param>
    /// <returns> The failed parse result. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="error" /> is <see langword="null" />. </exception>
    public static ValidateRequestJsonParseResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ValidateRequestJsonParseResult(null, error);
    }
}
