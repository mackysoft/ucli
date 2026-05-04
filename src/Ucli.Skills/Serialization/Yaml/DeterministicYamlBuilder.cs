using System.Text;

namespace MackySoft.Ucli.Skills.Serialization.Yaml;

/// <summary> Builds deterministic minimal YAML for generated SKILL artifacts. </summary>
internal sealed class DeterministicYamlBuilder
{
    private const int SpacesPerIndentLevel = 2;

    private readonly StringBuilder builder = new();

    /// <summary> Appends a YAML document marker. </summary>
    /// <returns> This builder. </returns>
    public DeterministicYamlBuilder DocumentMarker ()
    {
        builder.Append("---\n");
        return this;
    }

    /// <summary> Appends a mapping whose value is a double-quoted scalar. </summary>
    /// <param name="key"> The mapping key. </param>
    /// <param name="value"> The mapping value. </param>
    /// <param name="indentationLevel"> The indentation level. </param>
    /// <returns> This builder. </returns>
    public DeterministicYamlBuilder Mapping (
        string key,
        string value,
        int indentationLevel = 0)
    {
        AppendKey(key, indentationLevel);
        builder.Append(' ');
        builder.Append(YamlScalarFormatter.DoubleQuoted(value));
        builder.Append('\n');
        return this;
    }

    /// <summary> Appends a mapping whose value is a boolean scalar. </summary>
    /// <param name="key"> The mapping key. </param>
    /// <param name="value"> The mapping value. </param>
    /// <param name="indentationLevel"> The indentation level. </param>
    /// <returns> This builder. </returns>
    public DeterministicYamlBuilder Mapping (
        string key,
        bool value,
        int indentationLevel = 0)
    {
        AppendKey(key, indentationLevel);
        builder.Append(' ');
        builder.Append(value ? "true" : "false");
        builder.Append('\n');
        return this;
    }

    /// <summary> Appends a nested mapping section. </summary>
    /// <param name="key"> The section key. </param>
    /// <param name="indentationLevel"> The indentation level. </param>
    /// <returns> This builder. </returns>
    public DeterministicYamlBuilder Section (
        string key,
        int indentationLevel = 0)
    {
        AppendKey(key, indentationLevel);
        builder.Append('\n');
        return this;
    }

    /// <summary> Appends an empty line. </summary>
    /// <returns> This builder. </returns>
    public DeterministicYamlBuilder BlankLine ()
    {
        builder.Append('\n');
        return this;
    }

    /// <summary> Builds the YAML text. </summary>
    /// <returns> The YAML text with LF line endings. </returns>
    public string Build ()
    {
        return builder.ToString();
    }

    private void AppendKey (
        string key,
        int indentationLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfNegative(indentationLevel);

        if (key.Contains('\n', StringComparison.Ordinal) || key.Contains('\r', StringComparison.Ordinal))
        {
            throw new ArgumentException("YAML key must not contain line breaks.", nameof(key));
        }

        builder.Append(' ', indentationLevel * SpacesPerIndentLevel);
        builder.Append(key);
        builder.Append(':');
    }
}
