using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using static MackySoft.Ucli.Tests.CodesCliOutputContractTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class CodesCliOutputListContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task CodesList_ReturnsJsonEnvelopeSuccess ()
    {
        var result = await RunCodesListCommandAsync();

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.CodesList);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        var payload = outputJson.RootElement.GetProperty("payload");
        var codeItems = payload.GetProperty("codes").EnumerateArray().ToArray();
        var codes = GetCodes(outputJson.RootElement);
        Assert.NotEmpty(codes);
        Assert.Equal(codes.Order(StringComparer.Ordinal).ToArray(), codes);
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout.Value, codes);
        Assert.All(codeItems, AssertListCodeItemShape);
        var ipcTimeout = codeItems.Single(static code => code.GetProperty("code").GetString() == IpcTransportErrorCodes.IpcTimeout.Value);
        Assert.Equal(CodeCatalogKindValues.Error, ipcTimeout.GetProperty("kind").GetString());
        Assert.Equal("transport", ipcTimeout.GetProperty("category").GetString());
        Assert.Equal("The command timeout budget was exhausted.", ipcTimeout.GetProperty("summary").GetString());
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasInt32("catalogVersion", 1)
                .HasString("source", "bundled")
                .HasProperty("kinds", static kinds => kinds
                    .HasArrayLength(5)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CodesList_WithKindFilter_ReturnsOnlyKindMatches ()
    {
        var result = await RunCodesListCommandAsync(kind: CodeCatalogKindValues.Error);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.CodesList);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        var payload = outputJson.RootElement.GetProperty("payload");
        var codes = payload.GetProperty("codes").EnumerateArray().ToArray();
        Assert.NotEmpty(codes);
        Assert.All(codes, static code => Assert.Equal(CodeCatalogKindValues.Error, code.GetProperty("kind").GetString()));
        Assert.Contains(codes, static code => code.GetProperty("code").GetString() == IpcTransportErrorCodes.IpcTimeout.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CodesList_WithCommandFilter_ReturnsExpectedCodeFamilies ()
    {
        foreach (CodesListCommandFilterCase testCase in GetCodesListCommandFilterCases())
        {
            await AssertCodesListCommandFilterCaseAsync(testCase);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesList_WithKindAndCommandFilter_MatchesGolden ()
    {
        var result = await RunCodesListCommandAsync(
            kind: CodeCatalogKindValues.Error,
            command: "query.assets.find");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("codes", "list-kind-error-query-assets-find.json"),
            result.StdOut);
    }

    [Theory]
    [InlineData("future-kind", null)]
    [InlineData(null, "query.unknown")]
    [Trait("Size", "Small")]
    public async Task CodesList_WithUnknownFilter_ReturnsEmptyCodes (
        string? kind,
        string? command)
    {
        var result = await RunCodesListCommandAsync(kind, command);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.CodesList);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasArrayLength("codes", 0));
    }

    [Theory]
    [InlineData("--kind", "")]
    [InlineData("--command", "")]
    [InlineData("--command", "query assets")]
    [Trait("Size", "Small")]
    public async Task CodesList_WithInvalidFilter_ReturnsInvalidArgument (
        string optionName,
        string optionValue)
    {
        var result = optionName switch
        {
            "--kind" => await RunCodesListCommandAsync(kind: optionValue),
            "--command" => await RunCodesListCommandAsync(command: optionValue),
            _ => throw new ArgumentOutOfRangeException(nameof(optionName), optionName, "Unsupported codes list option."),
        };

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.CodesList);
    }
}
