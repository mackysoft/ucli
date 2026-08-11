using ConsoleAppFramework;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary>Binds a required recording identifier to its canonical non-zero UUID value at the CLI parse boundary.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class RecordingIdArgumentParserAttribute : Attribute, IArgumentParser<Guid>
{
    public static bool TryParse (ReadOnlySpan<char> value, out Guid result)
    {
        return RecordingIdArgumentParser.TryParse(value, out result);
    }
}

/// <summary>Binds an optional recording identifier to its canonical non-zero UUID value at the CLI parse boundary.</summary>
[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class OptionalRecordingIdArgumentParserAttribute : Attribute, IArgumentParser<Guid?>
{
    public static bool TryParse (ReadOnlySpan<char> value, out Guid? result)
    {
        if (RecordingIdArgumentParser.TryParse(value, out var recordingId))
        {
            result = recordingId;
            return true;
        }

        result = null;
        return false;
    }
}

internal static class RecordingIdArgumentParser
{
    public static bool TryParse (ReadOnlySpan<char> value, out Guid result)
    {
        if (value.Length == 36
            && Guid.TryParseExact(value, "D", out result)
            && result != Guid.Empty)
        {
            return true;
        }

        result = Guid.Empty;
        return false;
    }
}
