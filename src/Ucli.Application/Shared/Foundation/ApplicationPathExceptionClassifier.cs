namespace MackySoft.Ucli.Application.Shared.Foundation;

/// <summary> Classifies exceptions that represent invalid path formatting inside application policies. </summary>
internal static class ApplicationPathExceptionClassifier
{
    /// <summary> Determines whether one exception indicates invalid path formatting. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception indicates invalid path formatting; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    public static bool IsPathFormatException (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is ArgumentException
            or NotSupportedException
            or PathTooLongException;
    }
}
