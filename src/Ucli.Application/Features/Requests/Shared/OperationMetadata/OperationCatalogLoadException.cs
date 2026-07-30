namespace MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;

/// <summary> Represents one operation-catalog discovery failure that retains its application failure classification. </summary>
internal sealed class OperationCatalogLoadException : InvalidOperationException
{
    private OperationCatalogLoadException (ApplicationFailure error)
        : base(error?.Message)
    {
        Error = error ?? throw new ArgumentNullException(nameof(error));
    }

    /// <summary> Gets the classified application failure associated with this catalog load. </summary>
    public ApplicationFailure Error { get; }

    /// <summary> Creates one catalog-load exception while adding boundary context to the failure message. </summary>
    public static OperationCatalogLoadException Create (
        ApplicationFailure error,
        string messagePrefix)
    {
        ArgumentNullException.ThrowIfNull(error);
        ArgumentException.ThrowIfNullOrWhiteSpace(messagePrefix);
        return new OperationCatalogLoadException(WithMessage(
            error,
            $"{messagePrefix} {error.Message}"));
    }

    /// <summary> Creates one application failure from the retained failure while prefixing the message. </summary>
    /// <param name="messagePrefix"> The prefix to prepend to the original error message. </param>
    /// <returns> The prefixed application failure. </returns>
    public ApplicationFailure CreatePrefixedFailure (string messagePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messagePrefix);

        return WithMessage(
            Error,
            $"{messagePrefix} {Error.Message}");
    }

    private static ApplicationFailure WithMessage (
        ApplicationFailure error,
        string message)
    {
        return ApplicationFailure.Create(
            error.Kind,
            message,
            error.Code,
            error.InstancePath,
            error.Outcome,
            error.StartupFailure);
    }
}
