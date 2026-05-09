using System.Text.RegularExpressions;

namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Provides reusable validation and matching helpers for regex pattern strings. </summary>
internal static class RegexPatternUtilities
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    /// <summary> Validates whether a string can be compiled as a regular expression pattern. </summary>
    /// <param name="pattern"> The regex pattern string. </param>
    /// <param name="errorMessage"> The parser error message when validation fails. </param>
    /// <returns> <see langword="true" /> when pattern is valid; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="pattern" /> is <see langword="null" />. </exception>
    public static bool TryValidatePattern (
        string pattern,
        out string? errorMessage)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        return TryCompilePattern(pattern, out _, out errorMessage);
    }

    /// <summary> Attempts to evaluate regex matching with parser error detection. </summary>
    /// <param name="input"> The input text. </param>
    /// <param name="pattern"> The regex pattern text. </param>
    /// <param name="isMatch"> The match result when evaluation succeeds. </param>
    /// <returns> <see langword="true" /> when evaluation succeeds; otherwise <see langword="false" />. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="input" /> or <paramref name="pattern" /> is <see langword="null" />. </exception>
    public static bool TryIsMatch (
        string input,
        string pattern,
        out bool isMatch)
    {
        if (input == null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        if (!TryCompilePattern(pattern, out var regex, out _))
        {
            isMatch = false;
            return false;
        }

        try
        {
            isMatch = regex!.IsMatch(input);
            return true;
        }
        catch (RegexMatchTimeoutException)
        {
            isMatch = false;
            return false;
        }
    }

    /// <summary> Attempts to compile one regex pattern with a fixed option set. </summary>
    /// <param name="pattern"> The regex pattern text. </param>
    /// <param name="regex"> The compiled regex instance when compilation succeeds. </param>
    /// <param name="errorMessage"> The parser error message when compilation fails. </param>
    /// <returns> <see langword="true" /> when compile succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryCompilePattern (
        string pattern,
        out Regex? regex,
        out string? errorMessage)
    {
        if (pattern == null)
        {
            throw new ArgumentNullException(nameof(pattern));
        }

        try
        {
            regex = new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
            errorMessage = null;
            return true;
        }
        catch (ArgumentException exception)
        {
            regex = null;
            errorMessage = exception.Message;
            return false;
        }
    }
}
