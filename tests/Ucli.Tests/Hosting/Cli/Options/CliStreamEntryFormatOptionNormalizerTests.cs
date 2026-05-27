using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Streaming;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Tests.Hosting.Cli.Options;

public sealed class CliStreamEntryFormatOptionNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [Trait("Size", "Small")]
    public void Normalize_WhenFormatIsOmittedOrWhitespace_ReturnsText (string? format)
    {
        var result = CliStreamEntryFormatOptionNormalizer.Normalize(format);

        Assert.True(result.IsSuccess);
        Assert.Equal(CliStreamEntryFormat.Text, result.Format);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("text", 0)]
    [InlineData("TEXT", 0)]
    [InlineData(" json ", 1)]
    [InlineData("JSON", 1)]
    [Trait("Size", "Small")]
    public void Normalize_WhenFormatIsSupported_ReturnsParsedFormat (
        string format,
        int expectedFormat)
    {
        var result = CliStreamEntryFormatOptionNormalizer.Normalize(format);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedFormat, (int)result.Format);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Normalize_WhenFormatIsUnsupported_ReturnsInvalidArgument ()
    {
        var result = CliStreamEntryFormatOptionNormalizer.Normalize("yaml");

        Assert.False(result.IsSuccess);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal("format must be one of: text, json. Actual: yaml.", result.Error.Message);
    }
}
