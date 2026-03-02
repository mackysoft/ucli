namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Parses and normalizes test-platform option values. </summary>
internal static class TestRunPlatformCodec
{
    private const string EditModeValue = "editmode";

    private const string PlayModeValue = "playmode";

    private const string UnityEditModeValue = "EditMode";

    private const string UnityPlayModeValue = "PlayMode";

    /// <summary> Converts test-platform enum value to contract literal. </summary>
    /// <param name="testPlatform"> The test-platform enum value. </param>
    /// <returns> The contract literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="testPlatform" /> is unsupported. </exception>
    public static string ToValue (TestRunPlatform testPlatform)
    {
        return testPlatform switch
        {
            TestRunPlatform.EditMode => EditModeValue,
            TestRunPlatform.PlayMode => PlayModeValue,
            _ => throw new ArgumentOutOfRangeException(nameof(testPlatform), testPlatform, "Unsupported testPlatform."),
        };
    }

    /// <summary> Converts test-platform enum value to Unity command-line literal. </summary>
    /// <param name="testPlatform"> The test-platform enum value. </param>
    /// <returns> The Unity command-line literal value. </returns>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="testPlatform" /> is unsupported. </exception>
    public static string ToUnityValue (TestRunPlatform testPlatform)
    {
        return testPlatform switch
        {
            TestRunPlatform.EditMode => UnityEditModeValue,
            TestRunPlatform.PlayMode => UnityPlayModeValue,
            _ => throw new ArgumentOutOfRangeException(nameof(testPlatform), testPlatform, "Unsupported testPlatform."),
        };
    }

    /// <summary> Tries to parse one raw test-platform literal into <see cref="TestRunPlatform" />. </summary>
    /// <param name="value"> The raw platform literal. </param>
    /// <param name="testPlatform"> The parsed platform when parsing succeeds; otherwise default value. </param>
    /// <returns> <see langword="true" /> when parsing succeeds; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out TestRunPlatform testPlatform)
    {
        if (string.Equals(value, EditModeValue, StringComparison.OrdinalIgnoreCase))
        {
            testPlatform = TestRunPlatform.EditMode;
            return true;
        }

        if (string.Equals(value, PlayModeValue, StringComparison.OrdinalIgnoreCase))
        {
            testPlatform = TestRunPlatform.PlayMode;
            return true;
        }

        testPlatform = default;
        return false;
    }
}