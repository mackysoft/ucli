using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using static MackySoft.Ucli.Tests.CodesCliOutputContractTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class CodesCliOutputDescribeContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesDescribe_WithKnownCode_MatchesGolden ()
    {
        var result = await RunCodesDescribeCommandAsync(IpcTransportErrorCodes.IpcTimeout.Value);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("codes", "describe-ipc-timeout.json"),
            result.StdOut);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CodesDescribe_WithPlayModeControlCode_ReturnsKnownCode ()
    {
        var result = await RunCodesDescribeCommandAsync(PlayModeErrorCodes.PlayModeTransitionTimeout.Value);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.CodesDescribe);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasString("code", PlayModeErrorCodes.PlayModeTransitionTimeout.Value)
                .HasBoolean("known", true)
                .HasString("kind", CodeCatalogKindValues.Error)
                .HasString("category", "playMode"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CodesDescribe_WithMatchingKindAlias_ReturnsKnownCode ()
    {
        var result = await RunCodesDescribeCommandAsync("error:IPC_TIMEOUT");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.CodesDescribe);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasString("code", "IPC_TIMEOUT")
                .HasBoolean("known", true)
                .HasString("kind", CodeCatalogKindValues.Error));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CodesDescribe_WithMismatchedKindAlias_ReturnsInvalidArgument ()
    {
        var result = await RunCodesDescribeCommandAsync("claim:IPC_TIMEOUT");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.CodesDescribe);
        Assert.Contains(
            "not 'claim'",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SOME_FUTURE_CODE", "SOME_FUTURE_CODE")]
    [InlineData("error:SOME_FUTURE_CODE", "SOME_FUTURE_CODE")]
    [InlineData("future-kind:SOME_FUTURE_CODE", "SOME_FUTURE_CODE")]
    [InlineData("SOME.FUTURE_CODE", "SOME.FUTURE_CODE")]
    [Trait("Size", "Small")]
    public async Task CodesDescribe_WithUnknownValidCodeReference_ReturnsFallbackSuccess (
        string code,
        string expectedCode)
    {
        var result = await RunCodesDescribeCommandAsync(code);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.CodesDescribe);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("code", expectedCode)
                .HasBoolean("known", false)
                .HasString("kind", CodeCatalogKindValues.Unknown)
                .HasString("category", CodeCatalogKindValues.Unknown)
                .HasArrayLength("appearsIn", 0));
    }

    [Theory]
    [InlineData("SOME_FUTURE_CODE")]
    [InlineData("future-kind:SOME_FUTURE_CODE")]
    [Trait("Size", "Small")]
    public async Task CodesDescribe_WithUnknownValidCodeReferenceAndRequireKnown_ReturnsInvalidArgument (string code)
    {
        var result = await RunCodesDescribeCommandAsync(code, requireKnown: true);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.CodesDescribe);
        Assert.Contains(
            "not known",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not a code")]
    [InlineData("future:IPC_TIMEOUT")]
    [Trait("Size", "Small")]
    public async Task CodesDescribe_WithInvalidCodeReference_ReturnsInvalidArgument (string code)
    {
        var result = await RunCodesDescribeCommandAsync(code);

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.CodesDescribe);
    }
}
