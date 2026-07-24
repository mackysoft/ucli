namespace MackySoft.Tests;

internal static class TestShellPaths
{
    public static string QuoteBashArgument (string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return "'" + value.Replace("'", "'\"'\"'", StringComparison.Ordinal) + "'";
    }

}
