using System.Text;
using System.Xml;

namespace MackySoft.Ucli.Infrastructure.Xml;

/// <summary> Builds XML property-list documents from typed dictionary entries. </summary>
internal sealed class PropertyListXmlBuilder
{
    private const string PlistPublicId = "-//Apple//DTD PLIST 1.0//EN";

    private const string PlistSystemId = "http://www.apple.com/DTDs/PropertyList-1.0.dtd";

    private readonly XmlWriter writer;

    private PropertyListXmlBuilder (XmlWriter writer)
    {
        this.writer = writer ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary> Builds a plist document whose root value is a dictionary. </summary>
    /// <param name="writeEntries"> The delegate that writes dictionary entries to the root dictionary. </param>
    /// <returns> The complete plist XML document without a trailing newline. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="writeEntries" /> is <see langword="null" />. </exception>
    public static string BuildRootDictionary (Action<PropertyListXmlBuilder> writeEntries)
    {
        if (writeEntries == null)
        {
            throw new ArgumentNullException(nameof(writeEntries));
        }

        using var output = new MemoryStream();
        using (var writer = XmlWriter.Create(output, CreateWriterSettings()))
        {
            writer.WriteStartDocument();
            writer.WriteDocType("plist", PlistPublicId, PlistSystemId, null);
            writer.WriteStartElement("plist");
            writer.WriteAttributeString("version", "1.0");
            writer.WriteStartElement("dict");

            writeEntries(new PropertyListXmlBuilder(writer));

            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
        }

        return Encoding.UTF8.GetString(output.ToArray());
    }

    /// <summary> Writes one string value to the current dictionary. </summary>
    /// <param name="key"> The plist dictionary key. </param>
    /// <param name="value"> The string value. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="key" /> is empty or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="value" /> is <see langword="null" />. </exception>
    public void WriteString (
        string key,
        string value)
    {
        ThrowIfInvalidKey(key);
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        writer.WriteElementString("key", key);
        writer.WriteElementString("string", value);
    }

    /// <summary> Writes one boolean value to the current dictionary. </summary>
    /// <param name="key"> The plist dictionary key. </param>
    /// <param name="value"> The boolean value. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="key" /> is empty or whitespace. </exception>
    public void WriteBoolean (
        string key,
        bool value)
    {
        ThrowIfInvalidKey(key);

        writer.WriteElementString("key", key);
        writer.WriteStartElement(value ? "true" : "false");
        writer.WriteEndElement();
    }

    /// <summary> Writes one string array value to the current dictionary. </summary>
    /// <param name="key"> The plist dictionary key. </param>
    /// <param name="values"> The ordered string values. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="key" /> is empty or whitespace. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="values" /> or any element is <see langword="null" />. </exception>
    public void WriteStringArray (
        string key,
        IReadOnlyList<string> values)
    {
        ThrowIfInvalidKey(key);
        if (values == null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        writer.WriteElementString("key", key);
        writer.WriteStartElement("array");
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i] ?? throw new ArgumentNullException(nameof(values), "Array values must not contain null.");
            writer.WriteElementString("string", value);
        }

        writer.WriteEndElement();
    }

    private static XmlWriterSettings CreateWriterSettings ()
    {
        return new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            NewLineChars = "\n",
            OmitXmlDeclaration = false,
        };
    }

    private static void ThrowIfInvalidKey (string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Property list key must not be empty.", nameof(key));
        }
    }
}
