using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Tests;

public sealed class CodesCliOutputContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Codes_WithoutSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Codes);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Codes,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Codes_WithUnknownSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliProcessRunner.RunCommandAsync(UcliCommandNames.Codes, "unknown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Codes,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Theory]
    [InlineData("errors")]
    [InlineData("errors", "list")]
    [Trait("Size", "Medium")]
    public async Task ErrorsCommand_IsNotPublicCommand (params string[] args)
    {
        var result = await CliProcessRunner.RunCommandAsync(args);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.Root,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        Assert.Contains("Command 'errors' is not recognized.", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesList_ReturnsJsonEnvelopeSuccess ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.ListSubcommand);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
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
    [Trait("Size", "Medium")]
    public async Task CodesList_WithKindFilter_ReturnsOnlyKindMatches ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.ListSubcommand,
            "--kind",
            CodeCatalogKindValues.Error);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        var payload = outputJson.RootElement.GetProperty("payload");
        var codes = payload.GetProperty("codes").EnumerateArray().ToArray();
        Assert.NotEmpty(codes);
        Assert.All(codes, static code => Assert.Equal(CodeCatalogKindValues.Error, code.GetProperty("kind").GetString()));
        Assert.Contains(codes, static code => code.GetProperty("code").GetString() == IpcTransportErrorCodes.IpcTimeout.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesList_WithCommandLeafFilter_ReturnsFamilyMatches ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.ListSubcommand,
            "--command",
            "query.assets.find");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        var payload = outputJson.RootElement.GetProperty("payload");
        var codes = payload.GetProperty("codes").EnumerateArray().ToArray();
        Assert.NotEmpty(codes);
        Assert.All(codes, AssertListCodeItemShape);
        Assert.Contains(codes, static code => code.GetProperty("code").GetString() == IpcTransportErrorCodes.IpcTimeout.Value);
        Assert.DoesNotContain(codes, static code => code.GetProperty("code").GetString() == PlanTokenErrorCodes.PlanTokenExpired.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesList_WithKindAndCommandFilter_MatchesGolden ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.ListSubcommand,
            "--kind",
            CodeCatalogKindValues.Error,
            "--command",
            "query.assets.find");

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("codes", "list-kind-error-query-assets-find.json"),
            result.StdOut);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesList_WithUnknownKind_ReturnsEmptyCodes ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.ListSubcommand,
            "--kind",
            "future-kind");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasArrayLength("codes", 0));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesList_WithUnknownCommandFilter_ReturnsEmptyCodes ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.ListSubcommand,
            "--command",
            "query.unknown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesList,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasArrayLength("codes", 0));
    }

    [Theory]
    [InlineData("--kind", "")]
    [InlineData("--command", "")]
    [InlineData("--command", "query assets")]
    [Trait("Size", "Medium")]
    public async Task CodesList_WithInvalidFilter_ReturnsInvalidArgument (
        string optionName,
        string optionValue)
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.ListSubcommand,
            optionName,
            optionValue);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesList,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesDescribe_WithKnownCode_ReturnsJsonEnvelopeSuccess ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.DescribeSubcommand,
            IpcTransportErrorCodes.IpcTimeout.Value);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesDescribe,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasString("code", IpcTransportErrorCodes.IpcTimeout.Value)
            .HasBoolean("known", true)
            .HasString("kind", CodeCatalogKindValues.Error)
            .HasString("category", "transport")
            .HasString("summary", "The command timeout budget was exhausted.")
            .HasProperty("meaning")
            .HasProperty("appearsIn", static appearsIn => appearsIn
                .HasArrayLength(1))
            .HasProperty("appliesTo")
            .HasProperty("executionSemantics", static semantics => semantics
                .HasString("safeToRetry", UcliErrorRetryClassValues.ContextDependent))
            .HasProperty("inspect")
            .HasProperty("relatedCodes");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesDescribe_WithKnownCode_MatchesGolden ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.DescribeSubcommand,
            IpcTransportErrorCodes.IpcTimeout.Value);

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("codes", "describe-ipc-timeout.json"),
            result.StdOut);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesDescribe_WithMatchingKindAlias_ReturnsKnownCode ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.DescribeSubcommand,
            "error:IPC_TIMEOUT");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesDescribe,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasString("code", "IPC_TIMEOUT")
                .HasBoolean("known", true)
                .HasString("kind", CodeCatalogKindValues.Error));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesDescribe_WithMismatchedKindAlias_ReturnsInvalidArgument ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.DescribeSubcommand,
            "claim:IPC_TIMEOUT");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesDescribe,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        Assert.Contains(
            "not 'claim'",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesDescribe_WithUnknownCode_ReturnsFallbackSuccess ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.DescribeSubcommand,
            "SOME_FUTURE_CODE");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesDescribe,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasString("code", "SOME_FUTURE_CODE")
                .HasBoolean("known", false)
                .HasString("kind", CodeCatalogKindValues.Unknown)
                .HasString("category", CodeCatalogKindValues.Unknown)
                .HasArrayLength("appearsIn", 0));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesDescribe_WithUnknownKindQualifiedCode_ReturnsFallbackSuccess ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.DescribeSubcommand,
            "error:SOME_FUTURE_CODE");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesDescribe,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasString("code", "SOME_FUTURE_CODE")
                .HasBoolean("known", false)
                .HasString("kind", CodeCatalogKindValues.Unknown));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesDescribe_WithUnknownDotSegmentCode_ReturnsFallbackSuccess ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.DescribeSubcommand,
            "SOME.FUTURE_CODE");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesDescribe,
            status: "ok",
            exitCode: (int)CliExitCode.Success);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", static payload => payload
                .HasString("code", "SOME.FUTURE_CODE")
                .HasBoolean("known", false)
                .HasString("kind", CodeCatalogKindValues.Unknown));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task CodesDescribe_WithUnknownCodeAndRequireKnown_ReturnsInvalidArgument ()
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.DescribeSubcommand,
            "SOME_FUTURE_CODE",
            "--requireKnown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesDescribe,
            status: "error",
            exitCode: (int)CliExitCode.InvalidArgument);
        Assert.Contains(
            "not known",
            outputJson.RootElement.GetProperty("message").GetString(),
            StringComparison.Ordinal);
        CommandResultAssert.HasSingleError(outputJson.RootElement, UcliCoreErrorCodes.InvalidArgument);
    }

    [Theory]
    [InlineData("not a code")]
    [InlineData("future:IPC_TIMEOUT")]
    [Trait("Size", "Medium")]
    public async Task CodesDescribe_WithInvalidCodeReference_ReturnsInvalidArgument (string code)
    {
        var result = await CliProcessRunner.RunCommandAsync(
            UcliCommandNames.Codes,
            UcliCommandNames.DescribeSubcommand,
            code);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            command: UcliCommandNames.CodesDescribe,
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

    private static void AssertListCodeItemShape (JsonElement code)
    {
        var propertyNames = code
            .EnumerateObject()
            .Select(static property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(["category", "code", "kind", "summary"], propertyNames);
        Assert.False(string.IsNullOrWhiteSpace(code.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(code.GetProperty("kind").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(code.GetProperty("category").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(code.GetProperty("summary").GetString()));
    }
}
