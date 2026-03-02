namespace MackySoft.Ucli.Contracts.Paths;

/// <summary> Classifies exceptions that represent invalid path formatting. </summary>
internal static class PathFormatExceptionClassifier
{
    /// <summary> Determines whether one exception indicates invalid path formatting. </summary>
    /// <param name="exception"> The exception to classify. </param>
    /// <returns> <see langword="true" /> when exception indicates invalid path formatting; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    public static bool IsPathFormatException (Exception exception)
    {
        if (exception == null)
        {
            throw new ArgumentNullException(nameof(exception));
        }

        return exception is ArgumentException
            or NotSupportedException
            or PathTooLongException;
    }
}