using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Application.Features.Recording.Capability;

/// <summary>Evaluates resolved Recorder versions against the adapter range shipped in Contracts.</summary>
internal static class GameViewRecorderVersionCompatibility
{
    public static bool TryEvaluate (string resolvedVersion, out bool supported)
    {
        supported = false;
        if (!TryParseVersion(resolvedVersion, out var candidate)
            || !TryParseRange(
                GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
                out var minimum,
                out var maximum))
        {
            return false;
        }

        supported = candidate.CompareTo(minimum) >= 0
            && candidate.CompareTo(maximum) < 0;
        return true;
    }

    private static bool TryParseRange (
        string range,
        out Version minimum,
        out Version maximum)
    {
        minimum = default!;
        maximum = default!;
        if (range.Length < 6
            || range[0] != '['
            || range[^1] != ')')
        {
            return false;
        }

        var separator = range.IndexOf(',', StringComparison.Ordinal);
        if (separator <= 1
            || separator >= range.Length - 2
            || !Version.TryParse(range[1..separator], out var parsedMinimum)
            || !Version.TryParse(range[(separator + 1)..^1], out var parsedMaximum))
        {
            return false;
        }

        minimum = parsedMinimum;
        maximum = parsedMaximum;
        return true;
    }

    private static bool TryParseVersion (string value, out Version version)
    {
        version = default!;
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        if (value.IndexOfAny(['-', '+']) >= 0
            || !Version.TryParse(value, out var parsed)
            || parsed.Major < 0
            || parsed.Minor < 0
            || parsed.Build < 0)
        {
            return false;
        }

        version = parsed;
        return true;
    }
}
