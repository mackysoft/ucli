namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents authorization result for one operation execution attempt. </summary>
internal sealed record OperationAuthorizationResult
{
    private OperationAuthorizationResult (
        UcliCode? errorCode,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ErrorCode = errorCode;
        Message = message;
    }

    public bool IsAllowed => ErrorCode is null;

    public UcliCode? ErrorCode { get; }

    public string Message { get; }

    /// <summary> Creates an allowed authorization result. </summary>
    /// <returns> The allowed authorization result. </returns>
    public static OperationAuthorizationResult Allowed ()
    {
        return new OperationAuthorizationResult(
            errorCode: null,
            "Operation is allowed.");
    }

    /// <summary> Creates a denied authorization result. </summary>
    /// <param name="errorCode"> The machine-readable denial code. </param>
    /// <param name="message"> The human-readable denial message. </param>
    /// <returns> The denied authorization result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="errorCode" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static OperationAuthorizationResult Denied (
        UcliCode errorCode,
        string message)
    {
        ArgumentNullException.ThrowIfNull(errorCode);

        return new OperationAuthorizationResult(
            errorCode,
            message);
    }
}
