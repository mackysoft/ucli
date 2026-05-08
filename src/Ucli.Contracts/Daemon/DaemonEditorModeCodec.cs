using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Daemon;

/// <summary> Converts daemon Editor mode values to canonical contract literals. </summary>
public static class DaemonEditorModeCodec
{
    private static readonly (DaemonEditorMode Value, string Literal)[] Mappings =
    {
        (DaemonEditorMode.Batchmode, DaemonEditorModeValues.Batchmode),
        (DaemonEditorMode.Gui, DaemonEditorModeValues.Gui),
    };

    /// <summary> Converts one Editor mode enum value to a canonical contract literal. </summary>
    /// <param name="editorMode"> The Editor mode enum value. </param>
    /// <returns> The canonical contract literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="editorMode" /> is unsupported. </exception>
    public static string ToValue (DaemonEditorMode editorMode)
    {
        return LiteralCodecUtilities.ToValue(
            editorMode,
            Mappings,
            nameof(editorMode),
            "Unsupported daemon editorMode.");
    }

    /// <summary> Tries to parse one raw Editor mode literal. </summary>
    /// <param name="value"> The optional raw literal. </param>
    /// <param name="editorMode"> The parsed Editor mode enum value when parsing succeeds; otherwise default value. </param>
    /// <returns> <see langword="true" /> when one supported literal is parsed; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out DaemonEditorMode editorMode)
    {
        return LiteralCodecUtilities.TryParseTrimmed(
            value,
            Mappings,
            StringComparison.Ordinal,
            out editorMode);
    }
}
