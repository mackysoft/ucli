namespace MackySoft.Ucli.Application.Features.Testing.Run.Results;

/// <summary> Represents one Unity results conversion result. </summary>
internal abstract record UnityResultsConversionResult
{
    /// <summary> Creates a successful conversion result. </summary>
    /// <param name="verdictEvaluation">
    /// The normalized result, policy input, and verdict established by the conversion.
    /// </param>
    /// <returns> The successful conversion result. </returns>
    public static UnityResultsConversionSuccess Success (TestRunVerdictEvaluation verdictEvaluation)
    {
        return new UnityResultsConversionSuccess(verdictEvaluation);
    }

    public static UnityResultsConversionFailure InvalidResultsXml (string errorMessage)
    {
        return UnityResultsConversionFailure.CreateInvalidResultsXml(errorMessage);
    }

    public static UnityResultsConversionFailure ResultsXmlReadFailed (string errorMessage)
    {
        return UnityResultsConversionFailure.CreateResultsXmlReadFailed(errorMessage);
    }

    public static UnityResultsConversionFailure OutputWriteFailed (string errorMessage)
    {
        return UnityResultsConversionFailure.CreateOutputWriteFailed(errorMessage);
    }

    public static UnityResultsConversionFailure Canceled (string errorMessage)
    {
        return UnityResultsConversionFailure.CreateCanceled(errorMessage);
    }
}
