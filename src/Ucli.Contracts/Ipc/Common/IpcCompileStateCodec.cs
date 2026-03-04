using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts IPC compile-state values between runtime booleans and canonical literals. </summary>
public static class IpcCompileStateCodec
{
    /// <summary> Gets the compile-state value used when editor is ready. </summary>
    public const string Ready = "ready";

    /// <summary> Gets the compile-state value used when editor is compiling. </summary>
    public const string Compiling = "compiling";

    /// <summary> Converts one compile-activity flag to the IPC compile-state literal. </summary>
    /// <param name="isCompiling"> Whether the editor is currently compiling. </param>
    /// <returns> <see cref="Compiling" /> when <paramref name="isCompiling" /> is <see langword="true" />; otherwise <see cref="Ready" />. </returns>
    public static string ToValue (bool isCompiling)
    {
        return isCompiling
            ? Compiling
            : Ready;
    }

    /// <summary> Tries to parse one compile-state literal to a canonical IPC value. </summary>
    /// <param name="value"> The optional compile-state literal. </param>
    /// <param name="compileState"> The normalized compile-state literal when operation succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when one supported compile-state value is normalized; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out string? compileState)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalized))
        {
            compileState = null;
            return false;
        }

        if (string.Equals(normalized, Ready, StringComparison.Ordinal))
        {
            compileState = Ready;
            return true;
        }

        if (string.Equals(normalized, Compiling, StringComparison.Ordinal))
        {
            compileState = Compiling;
            return true;
        }

        compileState = null;
        return false;
    }
}