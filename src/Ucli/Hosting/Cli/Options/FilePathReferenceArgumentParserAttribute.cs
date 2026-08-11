using ConsoleAppFramework;
using MackySoft.Ucli.Application.Shared.Paths;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary>Binds an absolute or root-relative file path at the CLI parse boundary.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class FilePathReferenceArgumentParserAttribute : Attribute, IArgumentParser<FilePathReference?>
{
    public static bool TryParse (ReadOnlySpan<char> value, out FilePathReference? result)
    {
        return FilePathReference.TryParse(value.ToString(), out result);
    }
}
