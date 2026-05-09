using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Tests;

public sealed class ErrorsCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Errors_WithoutSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Errors);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Errors,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Errors_WithUnknownSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Errors, "unknown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Errors,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ErrorsList_ReturnsJsonEnvelopeSuccess ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Errors,
            UcliCommandNames.ListSubcommand);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.ErrorsList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        var codes = GetCodes(outputJson.RootElement);
        Assert.NotEmpty(codes);
        Assert.Equal(codes.Order(StringComparer.Ordinal).ToArray(), codes);
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout.Value, codes);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasInt32("catalogVersion", 1)
                .HasString("source", "bundled"));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ErrorsList_WithCategoryFilter_ReturnsOnlyCategoryMatches ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Errors,
            UcliCommandNames.ListSubcommand,
            "--category",
            "transport");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.ErrorsList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        var payload = outputJson.RootElement.GetProperty("payload");
        var codes = payload.GetProperty("codes").EnumerateArray().ToArray();
        Assert.NotEmpty(codes);
        Assert.All(codes, static code => Assert.Equal("transport", code.GetProperty("category").GetString()));
        Assert.Contains(codes, static code => code.GetProperty("code").GetString() == IpcTransportErrorCodes.IpcTimeout.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ErrorsList_WithCommandLeafFilter_ReturnsFamilyMatches ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Errors,
            UcliCommandNames.ListSubcommand,
            "--command",
            "query.assets.find");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.ErrorsList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        var payload = outputJson.RootElement.GetProperty("payload");
        var codes = payload.GetProperty("codes").EnumerateArray().ToArray();
        Assert.NotEmpty(codes);
        Assert.Contains(codes, static code => code.GetProperty("code").GetString() == IpcTransportErrorCodes.IpcTimeout.Value);
        Assert.All(codes, static code => Assert.Contains(
            code.GetProperty("appliesTo").EnumerateArray(),
            static appliesTo => IsSameOrRelatedCommand(appliesTo.GetString()!, "query.assets.find")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ErrorsList_WithUnknownCategory_ReturnsEmptyCodes ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Errors,
            UcliCommandNames.ListSubcommand,
            "--category",
            "unknown-category");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.ErrorsList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasArrayLength("codes", 0));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ErrorsDescribe_WithKnownCode_ReturnsJsonEnvelopeSuccess ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Errors,
            UcliCommandNames.DescribeSubcommand,
            IpcTransportErrorCodes.IpcTimeout.Value);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.ErrorsDescribe,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("code", IpcTransportErrorCodes.IpcTimeout.Value)
                .HasBoolean("known", true)
                .HasString("category", "transport")
                .HasProperty("executionSemantics")
                .HasArrayLength("nextActions", 3));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ErrorsDescribe_WithUnknownCode_ReturnsFallbackSuccess ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Errors,
            UcliCommandNames.DescribeSubcommand,
            "SOME_FUTURE_CODE");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.ErrorsDescribe,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasString("code", "SOME_FUTURE_CODE")
                .HasBoolean("known", false)
                .HasString("category", "unknown")
                .HasArrayLength("appliesTo", 0));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ErrorsDescribe_WithUnknownCodeAndRequireKnown_ReturnsInvalidArgument ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Errors,
            UcliCommandNames.DescribeSubcommand,
            "SOME_FUTURE_CODE",
            "--requireKnown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.ErrorsDescribe,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    private static string[] GetCodes (JsonElement root)
    {
        return root
            .GetProperty("payload")
            .GetProperty("codes")
            .EnumerateArray()
            .Select(static code => code.GetProperty("code").GetString()!)
            .ToArray();
    }

    private static bool IsSameOrRelatedCommand (
        string appliesTo,
        string commandFilter)
    {
        return string.Equals(appliesTo, commandFilter, StringComparison.Ordinal)
            || IsDotSegmentChild(appliesTo, commandFilter)
            || IsDotSegmentChild(commandFilter, appliesTo);
    }

    private static bool IsDotSegmentChild (
        string candidate,
        string parent)
    {
        return candidate.Length > parent.Length
            && candidate[parent.Length] == '.'
            && candidate.StartsWith(parent, StringComparison.Ordinal);
    }
}
