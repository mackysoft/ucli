using System.Xml.Linq;
using MackySoft.Ucli.Infrastructure.Xml;

namespace MackySoft.Ucli.Infrastructure.Tests.Xml;

public sealed class PropertyListXmlBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildRootDictionary_WritesTypedPlistValues ()
    {
        var plist = PropertyListXmlBuilder.BuildRootDictionary(builder =>
        {
            builder.WriteString("Label", "dev.mackysoft.ucli.test");
            builder.WriteBoolean("RunAtLoad", true);
            builder.WriteStringArray("ProgramArguments", ["ucli", "--flag"]);
        });
        var document = XDocument.Parse(plist);

        Assert.Equal("1.0", document.Declaration?.Version);
        Assert.Equal("utf-8", document.Declaration?.Encoding);
        Assert.False(plist.EndsWith('\n'));
        Assert.Equal("plist", document.DocumentType?.Name);
        Assert.Equal("-//Apple//DTD PLIST 1.0//EN", document.DocumentType?.PublicId);
        Assert.Equal("http://www.apple.com/DTDs/PropertyList-1.0.dtd", document.DocumentType?.SystemId);
        Assert.Equal("plist", document.Root?.Name.LocalName);
        Assert.Equal("1.0", document.Root?.Attribute("version")?.Value);
        Assert.Equal("dev.mackysoft.ucli.test", GetValueElement(document, "Label").Value);
        Assert.Equal("true", GetValueElement(document, "RunAtLoad").Name.LocalName);
        Assert.Equal(
            ["ucli", "--flag"],
            GetValueElement(document, "ProgramArguments").Elements("string").Select(static element => element.Value).ToArray());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildRootDictionary_EscapesXmlSpecialCharacters ()
    {
        var plist = PropertyListXmlBuilder.BuildRootDictionary(builder =>
        {
            builder.WriteString("Label<&>", "value<&>");
            builder.WriteStringArray("Arguments", ["ucli<&>"]);
        });
        var document = XDocument.Parse(plist);

        Assert.Equal("value<&>", GetValueElement(document, "Label<&>").Value);
        Assert.Equal("ucli<&>", GetValueElement(document, "Arguments").Element("string")?.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildRootDictionary_WhenWriteEntriesIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => PropertyListXmlBuilder.BuildRootDictionary(null!));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("")]
    [InlineData(" ")]
    public void WriteString_WhenKeyIsEmpty_ThrowsArgumentException (string key)
    {
        Assert.Throws<ArgumentException>(() =>
            PropertyListXmlBuilder.BuildRootDictionary(builder => builder.WriteString(key, "value")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void WriteString_WhenValueIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PropertyListXmlBuilder.BuildRootDictionary(builder => builder.WriteString("Label", null!)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void WriteStringArray_WhenValuesIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PropertyListXmlBuilder.BuildRootDictionary(builder => builder.WriteStringArray("Arguments", null!)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void WriteStringArray_WhenValueContainsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PropertyListXmlBuilder.BuildRootDictionary(builder => builder.WriteStringArray("Arguments", ["ucli", null!])));
    }

    private static XElement GetValueElement (
        XDocument document,
        string key)
    {
        var elements = document.Root?.Element("dict")?.Elements().ToArray()
            ?? throw new InvalidOperationException("plist dict element was not found.");
        for (var i = 0; i < elements.Length - 1; i += 2)
        {
            if (string.Equals(elements[i].Name.LocalName, "key", StringComparison.Ordinal)
                && string.Equals(elements[i].Value, key, StringComparison.Ordinal))
            {
                return elements[i + 1];
            }
        }

        throw new InvalidOperationException($"plist key was not found: {key}");
    }
}
