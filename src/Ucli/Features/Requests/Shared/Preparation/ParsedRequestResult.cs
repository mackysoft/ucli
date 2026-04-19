using MackySoft.Ucli.Shared.Foundation;

namespace MackySoft.Ucli.Features.Requests.Shared.Preparation;

/// <summary> Represents the result of reading and parsing one request without project binding. </summary>
/// <param name="ParsedRequest"> The parsed request context on success; otherwise <see langword="null" />. </param>
/// <param name="Error"> The structured read-or-parse error on failure; otherwise <see langword="null" />. </param>
internal sealed record ParsedRequestResult (
    ParsedRequestContext? ParsedRequest,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether request parsing succeeded. </summary>
    public bool IsSuccess => ParsedRequest is not null && Error is null;

    /// <summary> Creates a successful parsed-request result. </summary>
    /// <param name="parsedRequest"> The parsed request context. </param>
    /// <returns> The successful result. </returns>
    public static ParsedRequestResult Success (ParsedRequestContext parsedRequest)
    {
        ArgumentNullException.ThrowIfNull(parsedRequest);
        return new ParsedRequestResult(parsedRequest, null);
    }

    /// <summary> Creates a failed parsed-request result. </summary>
    /// <param name="error"> The structured error. </param>
    /// <returns> The failed result. </returns>
    public static ParsedRequestResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new ParsedRequestResult(null, error);
    }
}