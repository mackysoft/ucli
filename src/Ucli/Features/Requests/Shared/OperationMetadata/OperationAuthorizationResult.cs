namespace MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents authorization result for one operation execution attempt. </summary>
/// <param name="IsAllowed"> Whether execution is allowed under current configuration. </param>
/// <param name="ErrorCode"> The machine-readable denial code when <paramref name="IsAllowed" /> is <see langword="false" />. </param>
/// <param name="Message"> The human-readable authorization detail. </param>
internal sealed record OperationAuthorizationResult (
    bool IsAllowed,
    string? ErrorCode,
    string Message)
{
    /// <summary> Creates an allowed authorization result. </summary>
    /// <returns> The allowed authorization result. </returns>
    public static OperationAuthorizationResult Allowed ()
    {
        return new OperationAuthorizationResult(
            IsAllowed: true,
            ErrorCode: null,
            Message: "Operation is allowed.");
    }

    /// <summary> Creates a denied authorization result. </summary>
    /// <param name="errorCode"> The machine-readable denial code. </param>
    /// <param name="message"> The human-readable denial message. </param>
    /// <returns> The denied authorization result. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="errorCode" /> is <see langword="null" />, empty, or whitespace. </exception>
    public static OperationAuthorizationResult Denied (
        string errorCode,
        string message)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ArgumentException("Error code must not be null, empty, or whitespace.", nameof(errorCode));
        }

        return new OperationAuthorizationResult(
            IsAllowed: false,
            ErrorCode: errorCode,
            Message: message);
    }
}