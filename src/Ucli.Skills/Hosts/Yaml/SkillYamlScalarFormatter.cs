namespace MackySoft.Ucli.Skills.Hosts.Yaml;

/// <summary> Formats minimal deterministic YAML scalar values for generated host metadata. </summary>
internal static class SkillYamlScalarFormatter
{
    /// <summary> Formats one value as a double-quoted YAML scalar. </summary>
    /// <param name="value"> The value to format. </param>
    /// <returns> The double-quoted scalar. </returns>
    public static string DoubleQuoted (string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return "\"" + value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal) + "\"";
    }
}
