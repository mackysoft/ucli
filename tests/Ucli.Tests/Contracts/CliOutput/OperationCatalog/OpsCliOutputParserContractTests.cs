namespace MackySoft.Ucli.Tests;

public sealed class OpsCliOutputParserContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Ops_WithMissingOrUnknownSubcommand_ReturnsJsonEnvelopeError ()
    {
        string[][] argumentCases =
        [
            [],
            ["unknown"],
        ];

        foreach (string[] args in argumentCases)
        {
            var result = await CliInProcessRunner.RunCommandAsync([UcliCommandNames.Ops, .. args]);

            using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
            Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
            CommandResultAssert.HasInvalidArgumentEnvelope(
                outputJson.RootElement,
                UcliCommandNames.Ops);
            CommandResultAssert.HasSingleError(
                outputJson.RootElement,
                expectedCode: "INVALID_ARGUMENT");
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WithUnknownOption_ReturnsInvalidArgumentErrorAsSingleJson ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.OpsList);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        CommandResultAssert.ReportsUnrecognizedArgument(
            outputJson.RootElement.GetProperty("message").GetString(),
            UcliContractConstants.CliOption.Unknown);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task OpsList_WithFailFastCamelCaseAlias_IsAcceptedByParser ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Ops,
            UcliCommandNames.ListSubcommand,
            UcliContractConstants.CliOption.FailFast,
            UcliContractConstants.CliOption.ReadIndexMode,
            "unsupported");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(result.StdErr, UcliContractConstants.CliOption.FailFast);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.OpsList);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: UcliCoreErrorCodes.InvalidArgument);
    }
}
