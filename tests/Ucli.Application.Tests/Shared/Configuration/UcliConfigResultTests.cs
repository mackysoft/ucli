using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Tests.Configuration;

public sealed class UcliConfigResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void DiagnosticCreate_WithUntrustedText_EscapesControlCharactersAndLimitsLength ()
    {
        var diagnostic = UcliConfigDiagnostic.Create(
            "config.test",
            "path\nname\u2028\u202e\U000e0001",
            "source\rpath\u2029",
            "first\nsecond\u2028third\u2029fourth\u202e\U000e0001" + new string('x', 600));

        Assert.Equal("path\\u000Aname\\u2028\\u202E\\uDB40\\uDC01", diagnostic.PropertyPath);
        Assert.Equal("source\\u000Dpath\\u2029", diagnostic.SourcePath);
        Assert.Contains("first\\u000Asecond\\u2028third\\u2029fourth\\u202E\\uDB40\\uDC01", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\n", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2028", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\u2029", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202e", diagnostic.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("\U000e0001", diagnostic.Message, StringComparison.Ordinal);
        Assert.True(diagnostic.Message.Length <= 512);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FormatFragment_WithLongUntrustedText_EscapesBeforeInterpolation ()
    {
        var fragment = UcliConfigDiagnostic.FormatFragment("first\nsecond\u2028third\u2029fourth\u202e\U000e0001" + new string('x', 600));

        Assert.Contains("first\\u000Asecond\\u2028third\\u2029fourth\\u202E\\uDB40\\uDC01", fragment, StringComparison.Ordinal);
        Assert.True(fragment.Length <= UcliConfigDiagnostic.MaxTextLength);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WithEmptyDiagnostics_ThrowsArgumentException ()
    {
        var diagnostics = Array.Empty<UcliConfigDiagnostic>();

        Assert.Throws<ArgumentException>(() => UcliConfigLoadResult.Failure(diagnostics));
        Assert.Throws<ArgumentException>(() => UcliConfigSaveResult.Failure(diagnostics));
        Assert.Throws<ArgumentException>(() => UcliConfigBuildResult.Failure(diagnostics));
        Assert.Throws<ArgumentException>(() => UcliConfigDocumentBuildResult.Failure(diagnostics));
        Assert.Throws<ArgumentException>(() => UcliConfigSchemaValidationResult.Failure(diagnostics));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToExecutionError_WithManyDiagnostics_LimitsFormattedMessage ()
    {
        var diagnostics = Enumerable
            .Range(0, UcliConfigDiagnosticList.MaxDetailedDiagnostics + 20)
            .Select(static index => UcliConfigDiagnostic.Create(
                "config.test",
                $"property{index}",
                "config.json",
                $"Diagnostic {index}. {new string('x', 500)}"))
            .ToArray();
        var result = UcliConfigLoadResult.Failure(diagnostics);

        var error = UcliConfigDiagnosticErrorMapper.ToExecutionError(result, "fallback");

        Assert.Contains("Diagnostic 0.", error.Message, StringComparison.Ordinal);
        Assert.Contains("Additional config diagnostics were omitted.", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain($"Diagnostic {UcliConfigDiagnosticList.MaxDetailedDiagnostics + 10}.", error.Message, StringComparison.Ordinal);
        Assert.Equal(8192, error.Message.Length);
        Assert.EndsWith(UcliConfigDiagnosticList.OmittedDiagnosticsMessage, error.Message, StringComparison.Ordinal);
    }
}
