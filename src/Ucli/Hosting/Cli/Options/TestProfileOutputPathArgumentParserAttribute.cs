using ConsoleAppFramework;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary>Binds a test-profile output option to its normalized absolute file path at the CLI parse boundary.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class TestProfileOutputPathArgumentParserAttribute : Attribute, IArgumentParser<AbsolutePath?>
{
    private const string DefaultOutputPath = "test.profile.json";
    private const string JsonExtension = ".json";

    public static bool TryParse (ReadOnlySpan<char> value, out AbsolutePath? result)
    {
        var pathValue = StringValueNormalizer.TrimToNull(value.ToString()) ?? DefaultOutputPath;
        if (Path.EndsInDirectorySeparator(pathValue) || pathValue.EndsWith("\\", StringComparison.Ordinal))
        {
            result = null;
            return false;
        }

        if (!pathValue.EndsWith(JsonExtension, StringComparison.OrdinalIgnoreCase))
        {
            pathValue += JsonExtension;
        }

        return AbsolutePath.TryResolve(
            AbsolutePath.Parse(Environment.CurrentDirectory),
            pathValue,
            out result,
            out _);
    }
}
