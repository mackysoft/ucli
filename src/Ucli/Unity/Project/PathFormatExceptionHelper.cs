namespace MackySoft.Ucli.UnityProject;

/// <summary> Classifies exceptions that represent invalid path formatting from user input. </summary>
internal static class PathFormatExceptionHelper
{
    /// <summary> Determines whether one exception indicates invalid path formatting. </summary>
    /// <param name="exception"> The exception to inspect. </param>
    /// <returns> <see langword="true" /> when the exception represents invalid path formatting; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="exception" /> is <see langword="null" />. </exception>
    public static bool IsPathFormatException (Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is ArgumentException
            or NotSupportedException
            or PathTooLongException;
    }
}