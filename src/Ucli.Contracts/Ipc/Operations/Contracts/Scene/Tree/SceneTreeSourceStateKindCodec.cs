using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts scene-tree source-state kinds between enum and contract literals. </summary>
public static class SceneTreeSourceStateKindCodec
{
    private static readonly (SceneTreeSourceStateKind Value, string Literal)[] Mappings =
    {
        (SceneTreeSourceStateKind.TemporaryScene, SceneTreeSourceStateKindValues.TemporaryScene),
        (SceneTreeSourceStateKind.LoadedScene, SceneTreeSourceStateKindValues.LoadedScene),
        (SceneTreeSourceStateKind.PersistedPreview, SceneTreeSourceStateKindValues.PersistedPreview),
        (SceneTreeSourceStateKind.ReadIndex, SceneTreeSourceStateKindValues.ReadIndex),
    };

    /// <summary> Converts one source-state kind enum value to its contract literal. </summary>
    /// <param name="sourceStateKind"> The source-state kind enum value. </param>
    /// <returns> The contract literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="sourceStateKind" /> is unsupported. </exception>
    public static string ToValue (SceneTreeSourceStateKind sourceStateKind)
    {
        return LiteralCodecUtilities.ToValue(
            sourceStateKind,
            Mappings,
            nameof(sourceStateKind),
            "Unsupported scene-tree source-state kind.");
    }

    /// <summary> Tries to parse one contract literal to source-state kind enum value. </summary>
    /// <param name="value"> The contract literal value. </param>
    /// <param name="sourceStateKind"> The parsed source-state kind enum value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out SceneTreeSourceStateKind sourceStateKind)
    {
        return LiteralCodecUtilities.TryParse(
            value,
            Mappings,
            StringComparison.Ordinal,
            out sourceStateKind);
    }
}
