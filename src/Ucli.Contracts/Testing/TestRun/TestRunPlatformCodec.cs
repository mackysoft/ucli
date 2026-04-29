using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Converts Unity-equivalent test-run platform values between literals and typed values. </summary>
public static class TestRunPlatformCodec
{
    /// <summary> Gets the canonical literal for EditMode runs. </summary>
    public const string EditMode = "editmode";

    /// <summary> Gets the canonical literal for PlayMode runs. </summary>
    public const string PlayMode = "playmode";

    /// <summary> Gets the Unity command-line literal for EditMode runs. </summary>
    public const string UnityEditMode = "EditMode";

    /// <summary> Gets the Unity command-line literal for PlayMode runs. </summary>
    public const string UnityPlayMode = "PlayMode";

    /// <summary> Converts one platform value to canonical contract literal. </summary>
    /// <param name="testPlatform"> The test-run platform value. </param>
    /// <returns> The canonical contract literal. </returns>
    public static string ToValue (TestRunPlatform testPlatform)
    {
        return testPlatform.Kind switch
        {
            TestRunPlatformKind.EditMode => EditMode,
            TestRunPlatformKind.PlayMode => PlayMode,
            TestRunPlatformKind.Player => testPlatform.PlayerBuildTargetLiteral!,
            _ => throw new ArgumentOutOfRangeException(nameof(testPlatform), "Unsupported test platform."),
        };
    }

    /// <summary> Converts one platform value to Unity command-line literal. </summary>
    /// <param name="testPlatform"> The test-run platform value. </param>
    /// <returns> The Unity command-line literal. </returns>
    public static string ToUnityValue (TestRunPlatform testPlatform)
    {
        return testPlatform.Kind switch
        {
            TestRunPlatformKind.EditMode => UnityEditMode,
            TestRunPlatformKind.PlayMode => UnityPlayMode,
            TestRunPlatformKind.Player => testPlatform.PlayerBuildTargetLiteral!,
            _ => throw new ArgumentOutOfRangeException(nameof(testPlatform), "Unsupported test platform."),
        };
    }

    /// <summary> Tries to parse one literal into a typed platform value. </summary>
    /// <param name="value"> The optional raw platform literal. </param>
    /// <param name="testPlatform"> The parsed platform value when parsing succeeds; otherwise default value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out TestRunPlatform testPlatform)
    {
        testPlatform = default;
        if (!StringValueNormalizer.TryTrimToNonEmpty(value, out var normalizedValue))
        {
            return false;
        }

        if (string.Equals(normalizedValue, EditMode, StringComparison.OrdinalIgnoreCase))
        {
            testPlatform = TestRunPlatform.EditMode;
            return true;
        }

        if (string.Equals(normalizedValue, PlayMode, StringComparison.OrdinalIgnoreCase))
        {
            testPlatform = TestRunPlatform.PlayMode;
            return true;
        }

        testPlatform = TestRunPlatform.Player(normalizedValue);
        return true;
    }
}
