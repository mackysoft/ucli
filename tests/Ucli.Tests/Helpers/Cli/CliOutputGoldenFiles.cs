namespace MackySoft.Ucli.Tests;

internal static class CliOutputGoldenFiles
{
    private const string Root = "tests/Ucli.Tests/GoldenFiles/Json/CliOutput";

    public static string GetPath (
        string commandName,
        string fileName)
    {
        return Path.Combine(Root, commandName, fileName);
    }
}
