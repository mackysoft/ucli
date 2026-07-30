namespace MackySoft.Ucli.Application.Features.Testing.Run.Results;

/// <summary> Represents a failed Unity results conversion. </summary>
internal sealed record UnityResultsConversionFailure : UnityResultsConversionResult
{
    /// <summary> Initializes one failed Unity results conversion. </summary>
    /// <param name="failureKind"> The failure kind. </param>
    /// <param name="errorMessage"> The user-facing failure message. </param>
    private UnityResultsConversionFailure (
        UnityResultsConversionFailureKind failureKind,
        string errorMessage)
    {
        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureKind),
                failureKind,
                "Unity results conversion failure kind must be a defined value.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);

        FailureKind = failureKind;
        ErrorMessage = errorMessage;
    }

    /// <summary> Gets the conversion failure kind. </summary>
    public UnityResultsConversionFailureKind FailureKind { get; }

    /// <summary> Gets the user-facing conversion failure message. </summary>
    public string ErrorMessage { get; }

    internal static UnityResultsConversionFailure CreateInvalidResultsXml (string errorMessage)
    {
        return new UnityResultsConversionFailure(
            UnityResultsConversionFailureKind.InvalidResultsXml,
            errorMessage);
    }

    internal static UnityResultsConversionFailure CreateResultsXmlReadFailed (string errorMessage)
    {
        return new UnityResultsConversionFailure(
            UnityResultsConversionFailureKind.ResultsXmlReadFailed,
            errorMessage);
    }

    internal static UnityResultsConversionFailure CreateOutputWriteFailed (string errorMessage)
    {
        return new UnityResultsConversionFailure(
            UnityResultsConversionFailureKind.OutputWriteFailed,
            errorMessage);
    }

    internal static UnityResultsConversionFailure CreateCanceled (string errorMessage)
    {
        return new UnityResultsConversionFailure(
            UnityResultsConversionFailureKind.Canceled,
            errorMessage);
    }
}
