using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts Play Mode transition values between typed values and IPC literals. </summary>
public static class IpcPlayModeTransitionCodec
{
    private static readonly (IpcPlayModeTransition Value, string Literal)[] Mappings =
    {
        (IpcPlayModeTransition.None, IpcPlayModeTransitionNames.None),
        (IpcPlayModeTransition.Entering, IpcPlayModeTransitionNames.Entering),
        (IpcPlayModeTransition.Exiting, IpcPlayModeTransitionNames.Exiting),
    };

    /// <summary> Converts one Play Mode transition value to its IPC literal. </summary>
    /// <param name="transition"> The typed Play Mode transition. </param>
    /// <returns> The canonical IPC literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="transition" /> is unsupported. </exception>
    public static string ToValue (IpcPlayModeTransition transition)
    {
        return LiteralCodecUtilities.ToValue(
            transition,
            Mappings,
            nameof(transition),
            "Unsupported Play Mode transition.");
    }

    /// <summary> Tries to parse one IPC literal to a typed Play Mode transition. </summary>
    /// <param name="value"> The IPC literal value. </param>
    /// <param name="transition"> The parsed Play Mode transition on success; otherwise the default value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out IpcPlayModeTransition transition)
    {
        return LiteralCodecUtilities.TryParseTrimmed(
            value,
            Mappings,
            StringComparison.Ordinal,
            out transition);
    }
}
