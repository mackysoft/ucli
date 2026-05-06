using System.Text;
using System.Text.Json;

namespace MackySoft.Ucli.Contracts.Json;

/// <summary> Provides common formatting for public JSON contract writers. </summary>
/// <typeparam name="TContract"> The contract type written by this writer. </typeparam>
internal abstract class JsonContractWriter<TContract> : IJsonContractWriter<TContract>
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
    };

    /// <inheritdoc />
    public string Write (TContract contract)
    {
        if (contract == null)
        {
            throw new ArgumentNullException(nameof(contract));
        }

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            WriteCore(writer, contract);
        }

        var json = NormalizeLineEndings(Encoding.UTF8.GetString(stream.ToArray()));
        return json.EndsWith("\n", StringComparison.Ordinal) ? json : json + "\n";
    }

    /// <summary> Writes the JSON body for one contract. </summary>
    /// <param name="writer"> The JSON writer. </param>
    /// <param name="contract"> The contract instance. </param>
    protected abstract void WriteCore (
        Utf8JsonWriter writer,
        TContract contract);

    /// <summary> Writes a string property and preserves <see langword="null" /> as JSON null. </summary>
    protected static void WriteNullableString (
        Utf8JsonWriter writer,
        string propertyName,
        string? value)
    {
        if (value == null)
        {
            writer.WriteNull(propertyName);
            return;
        }

        writer.WriteString(propertyName, value);
    }

    /// <summary> Writes a string array property and preserves a <see langword="null" /> collection as JSON null. </summary>
    protected static void WriteStringArray (
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyList<string>? values)
    {
        writer.WritePropertyName(propertyName);
        if (values == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartArray();
        for (var i = 0; i < values.Count; i++)
        {
            writer.WriteStringValue(values[i]);
        }

        writer.WriteEndArray();
    }

    private static string NormalizeLineEndings (string text)
    {
        return text
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");
    }
}
