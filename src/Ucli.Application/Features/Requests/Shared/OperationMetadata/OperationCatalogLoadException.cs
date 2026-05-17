using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents one operation-catalog discovery failure that retains structured execution-error classification. </summary>
internal sealed class OperationCatalogLoadException : InvalidOperationException
{
    /// <summary> Initializes a new instance of the <see cref="OperationCatalogLoadException" /> class. </summary>
    /// <param name="error"> The structured execution error associated with the catalog-load failure. </param>
    /// <param name="errorCode"> The original machine-readable error code associated with the catalog-load failure. </param>
    public OperationCatalogLoadException (
        ExecutionError error,
        UcliCodeValue? errorCode = null)
        : base(error?.Message)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
        ErrorCode = !errorCode.HasValue || !errorCode.Value.IsValid
            ? ExecutionErrorCodeMapper.ToCode(Error)
            : errorCode.Value;
    }

    /// <summary> Gets the structured execution error associated with this failure. </summary>
    public ExecutionError Error { get; }

    /// <summary> Gets the original machine-readable error code associated with this failure. </summary>
    public UcliCodeValue ErrorCode { get; }

    /// <summary> Creates one execution error that preserves the original kind while prefixing the message. </summary>
    /// <param name="messagePrefix"> The prefix to prepend to the original error message. </param>
    /// <returns> The prefixed execution error. </returns>
    public ExecutionError CreatePrefixedError (string messagePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messagePrefix);

        var message = $"{messagePrefix} {Error.Message}";
        return Error.Kind switch
        {
            ExecutionErrorKind.InvalidArgument => ExecutionError.InvalidArgument(message, ErrorCode),
            ExecutionErrorKind.Timeout => ExecutionError.Timeout(message, ErrorCode),
            _ => ExecutionError.InternalError(message, ErrorCode),
        };
    }
}
