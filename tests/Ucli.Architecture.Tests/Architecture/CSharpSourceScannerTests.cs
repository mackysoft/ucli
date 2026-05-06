namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class CSharpSourceScannerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void StripCommentsAndStringLiterals_preserves_interpolated_expression_code ()
    {
        var sourceText = """"
            internal sealed class Sample
            {
                internal string Normal () => $"{MackySoft.Ucli.Hosting.Sample.Value}";

                internal string Raw () => $$"""
                    {{System.Diagnostics.Process.Start("dotnet")}}
                    """;

                internal string Literal () => "StringOnly.ForbiddenMarker";
            }
            """";

        var strippedText = CSharpSourceScanner.StripCommentsAndStringLiterals(sourceText);

        Assert.Contains("MackySoft.Ucli.Hosting.Sample.Value", strippedText, StringComparison.Ordinal);
        Assert.Contains("System.Diagnostics.Process.Start", strippedText, StringComparison.Ordinal);
        Assert.DoesNotContain("StringOnly.ForbiddenMarker", strippedText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void StripCommentsAndStringLiterals_removes_interpolated_format_strings ()
    {
        var sourceText = """"
            internal sealed class Sample
            {
                internal string Normal (object value) => $"{value:MackySoft.Ucli.Hosting}";

                internal string NullConditional (Sample? value) => $"{value?.Name:MackySoft.Ucli.Shared}";

                internal string Raw (object value) => $$"""
                    {{value:System.Diagnostics.Process}}
                    """;
            }
            """";

        var strippedText = CSharpSourceScanner.StripCommentsAndStringLiterals(sourceText);

        Assert.DoesNotContain("MackySoft.Ucli.Hosting", strippedText, StringComparison.Ordinal);
        Assert.DoesNotContain("MackySoft.Ucli.Shared", strippedText, StringComparison.Ordinal);
        Assert.DoesNotContain("System.Diagnostics.Process", strippedText, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void StripCommentsAndStringLiterals_preserves_raw_interpolation_after_literal_brace ()
    {
        var sourceText = """"
            internal sealed class Sample
            {
                internal string Raw () => $$"""{{{MackySoft.Ucli.Hosting.Sample.Value}}}""";
            }
            """";

        var strippedText = CSharpSourceScanner.StripCommentsAndStringLiterals(sourceText);

        Assert.Contains("MackySoft.Ucli.Hosting.Sample.Value", strippedText, StringComparison.Ordinal);
    }
}
