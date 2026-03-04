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

    /// <summary> Converts one test-run platform enum value to canonical IPC literal. </summary>
    /// <param name="testPlatform"> The test-run platform enum value. </param>
    /// <returns> The canonical IPC literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="testPlatform" /> is unsupported. </exception>
    public static string ToValue (IpcTestRunPlatform testPlatform)
    {
        return testPlatform switch
        {
            IpcTestRunPlatform.EditMode => EditMode,
            IpcTestRunPlatform.PlayMode => PlayMode,
            _ => throw new ArgumentOutOfRangeException(nameof(testPlatform), testPlatform, "Unsupported test platform."),
        };
    }

    /// <summary> Converts one test-run platform enum value to Unity command-line literal. </summary>
    /// <param name="testPlatform"> The test-run platform enum value. </param>
    /// <returns> The Unity command-line literal. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="testPlatform" /> is unsupported. </exception>
    public static string ToUnityValue (IpcTestRunPlatform testPlatform)
    {
        return testPlatform switch
        {
            IpcTestRunPlatform.EditMode => UnityEditMode,
            IpcTestRunPlatform.PlayMode => UnityPlayMode,
            _ => throw new ArgumentOutOfRangeException(nameof(testPlatform), testPlatform, "Unsupported test platform."),
        };
    }

    /// <summary> Tries to parse one test-platform literal to canonical IPC value. </summary>
    /// <param name="value"> The optional raw test-platform literal. </param>
    /// <param name="testPlatform"> The parsed test-platform value when parsing succeeds; otherwise default value. </param>
    /// <returns> <see langword="true" /> when one supported test-platform value is parsed; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out IpcTestRunPlatform testPlatform)
    {
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalized))
        {
            testPlatform = default;
            return false;
        }

        if (string.Equals(normalized, EditMode, StringComparison.OrdinalIgnoreCase))
        {
            testPlatform = IpcTestRunPlatform.EditMode;
            return true;
        }

        if (string.Equals(normalized, PlayMode, StringComparison.OrdinalIgnoreCase))
        {
            testPlatform = IpcTestRunPlatform.PlayMode;
            return true;
        }

        testPlatform = default;
        return false;
    }
}