namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Parses and normalizes test-platform option values. </summary>
internal static class TestRunPlatformParser
{
    /// <summary> Parses one raw test-platform value into <see cref="TestRunPlatform" />. </summary>
    /// <param name="value"> The raw platform value. </param>
    /// <returns> The parsed platform value, or <see cref="TestRunPlatform.Unknown" /> when unsupported. </returns>
    public static TestRunPlatform Parse (string value)
    {
        if (string.Equals(value, "editmode", StringComparison.OrdinalIgnoreCase))
        {
            return TestRunPlatform.EditMode;
        }

        if (string.Equals(value, "playmode", StringComparison.OrdinalIgnoreCase))
        {
            return TestRunPlatform.PlayMode;
        }

        return TestRunPlatform.Unknown;
    }
}