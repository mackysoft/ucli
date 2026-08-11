using System.Text.RegularExpressions;

namespace MackySoft.Ucli.Application.Shared.Configuration;

/// <summary> Defines the shared semantic constraints of project Program Preset registrations. </summary>
internal static class UcliProgramPresetValidator
{
    private static readonly Regex IdPattern = new(
        "^[a-z][a-z0-9]*(?:[.-][a-z0-9]+)*$",
        RegexOptions.CultureInvariant);

    /// <summary> Gets whether a preset ID satisfies the closed configuration contract. </summary>
    public static bool IsValidId (string value)
    {
        return value.Length is >= 1 and <= 128 && IdPattern.IsMatch(value);
    }

    /// <summary> Gets whether a preset description satisfies the closed configuration contract. </summary>
    public static bool IsValidDescription (string? value)
    {
        return value is { Length: >= 1 and <= 1024 } && !string.IsNullOrWhiteSpace(value);
    }

    /// <summary> Gets whether a preset path is a non-empty slash-separated relative JSON path without dot segments. </summary>
    public static bool IsValidProgramPath (string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.EndsWith(".json", StringComparison.Ordinal)
            && !Path.IsPathRooted(value)
            && !value.Contains('\\')
            && value.Split('/', StringSplitOptions.None).All(static segment => segment is not ("" or "." or ".."));
    }
}
