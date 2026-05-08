using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts IPC editor runtime values to canonical literals. </summary>
public static class IpcEditorRuntimeCodec
{
    /// <summary> Gets the runtime value used by Unity batchmode hosts. </summary>
    public const string Batchmode = DaemonEditorModeValues.Batchmode;

    /// <summary> Gets the runtime value used by Unity GUI hosts. </summary>
    public const string Gui = DaemonEditorModeValues.Gui;

    private static readonly string[] CanonicalLiterals =
    {
        Batchmode,
        Gui,
    };

    /// <summary> Tries to normalize one raw runtime literal. </summary>
    /// <param name="value"> The optional raw literal. </param>
    /// <param name="runtime"> The canonical literal on success; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when one supported literal is normalized; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out string? runtime)
    {
        return LiteralCodecUtilities.TryNormalizeLiteral(
            value,
            CanonicalLiterals,
            StringComparison.Ordinal,
            out runtime);
    }
}
