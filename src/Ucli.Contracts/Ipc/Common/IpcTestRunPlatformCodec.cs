using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts test-run platform values between raw literals and canonical IPC literals. </summary>
public static class IpcTestRunPlatformCodec
{
    /// <summary> Gets the canonical test-platform value for EditMode runs. </summary>
    public const string EditMode = "editmode";

    /// <summary> Gets the canonical test-platform value for PlayMode runs. </summary>
    public const string PlayMode = "playmode";

    /// <summary> Gets the Unity command-line value for EditMode runs. </summary>
    public const string UnityEditMode = "EditMode";

    /// <summary> Gets the Unity command-line value for PlayMode runs. </summary>
    public const string UnityPlayMode = "PlayMode";

    private static readonly (IpcTestRunPlatform Value, string Literal)[] CanonicalMappings =
    {
        (IpcTestRunPlatform.EditMode, EditMode),
        (IpcTestRunPlatform.PlayMode, PlayMode),
    };

    private static readonly (IpcTestRunPlatform Value, string Literal)[] UnityMappings =
    {
        (IpcTestRunPlatform.EditMode, UnityEditMode),
        (IpcTestRunPlatform.PlayMode, UnityPlayMode),
    };

    /// <summary> Converts one test-run platform enum value to canonical IPC literal. </summary>
    /// <param name="testPlatform"> The test-run platform enum value. </param>
    /// <returns> The canonical IPC literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="testPlatform" /> is unsupported. </exception>
    public static string ToValue (IpcTestRunPlatform testPlatform)
    {
        return LiteralCodecUtilities.ToValue(
            testPlatform,
            CanonicalMappings,
            nameof(testPlatform),
            "Unsupported test platform.");
    }

    /// <summary> Converts one test-run platform enum value to Unity command-line literal. </summary>
    /// <param name="testPlatform"> The test-run platform enum value. </param>
    /// <returns> The Unity command-line literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="testPlatform" /> is unsupported. </exception>
    public static string ToUnityValue (IpcTestRunPlatform testPlatform)
    {
        return LiteralCodecUtilities.ToValue(
            testPlatform,
            UnityMappings,
            nameof(testPlatform),
            "Unsupported test platform.");
    }

    /// <summary> Tries to parse one test-platform literal to canonical IPC value. </summary>
    /// <param name="value"> The optional raw test-platform literal. </param>
    /// <param name="testPlatform"> The parsed test-platform value when parsing succeeds; otherwise default value. </param>
    /// <returns> <see langword="true" /> when one supported test-platform value is parsed; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out IpcTestRunPlatform testPlatform)
    {
        return LiteralCodecUtilities.TryParseTrimmed(
            value,
            CanonicalMappings,
            StringComparison.OrdinalIgnoreCase,
            out testPlatform);
    }
}