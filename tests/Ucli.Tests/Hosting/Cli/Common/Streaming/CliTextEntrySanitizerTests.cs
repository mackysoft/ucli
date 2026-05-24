using MackySoft.Ucli.Hosting.Cli.Common.Streaming;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Common.Streaming;

public sealed class CliTextEntrySanitizerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Sanitize_EscapesLineControlAnsiAndUnicodeFormatCharacters ()
    {
        var sanitized = CliTextEntrySanitizer.Sanitize("a\r\n\t\u001Bb\u202Ec\u2028d\u2029e");

        Assert.Equal("a\\r\\n\\t\\u001Bb\\u202Ec\\u2028d\\u2029e", sanitized);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Sanitize_PreservesPrintableSupplementaryCharacters ()
    {
        var sanitized = CliTextEntrySanitizer.Sanitize("case \U0001F600 passed");

        Assert.Equal("case \U0001F600 passed", sanitized);
    }
}
