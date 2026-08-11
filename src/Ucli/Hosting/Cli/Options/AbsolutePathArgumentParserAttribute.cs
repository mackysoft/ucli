using ConsoleAppFramework;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary>Binds a path option to its guarded absolute-path value at the CLI parse boundary.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class AbsolutePathArgumentParserAttribute : Attribute, IArgumentParser<AbsolutePath?>
{
    public static bool TryParse (ReadOnlySpan<char> value, out AbsolutePath? result)
    {
        var currentDirectory = AbsolutePath.Parse(Environment.CurrentDirectory);
        if (AbsolutePath.TryResolve(
            currentDirectory,
            value.ToString(),
            out var path,
            out _))
        {
            result = path;
            return true;
        }

        result = null;
        return false;
    }
}
