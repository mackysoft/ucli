using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts Play Mode state values between typed values and IPC literals. </summary>
public static class IpcPlayModeStateCodec
{
    private static readonly (IpcPlayModeState Value, string Literal)[] Mappings =
    {
        (IpcPlayModeState.Stopped, IpcPlayModeStateNames.Stopped),
        (IpcPlayModeState.Entering, IpcPlayModeStateNames.Entering),
        (IpcPlayModeState.Playing, IpcPlayModeStateNames.Playing),
        (IpcPlayModeState.Exiting, IpcPlayModeStateNames.Exiting),
        (IpcPlayModeState.Unknown, IpcPlayModeStateNames.Unknown),
    };

    /// <summary> Converts one Play Mode state value to its IPC literal. </summary>
    /// <param name="state"> The typed Play Mode state. </param>
    /// <returns> The canonical IPC literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="state" /> is unsupported. </exception>
    public static string ToValue (IpcPlayModeState state)
    {
        return LiteralCodecUtilities.ToValue(
            state,
            Mappings,
            nameof(state),
            "Unsupported Play Mode state.");
    }

    /// <summary> Tries to parse one IPC literal to a typed Play Mode state. </summary>
    /// <param name="value"> The IPC literal value. </param>
    /// <param name="state"> The parsed Play Mode state on success; otherwise the default value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out IpcPlayModeState state)
    {
        return LiteralCodecUtilities.TryParseTrimmed(
            value,
            Mappings,
            StringComparison.Ordinal,
            out state);
    }
}
