using MackySoft.Tests;
using static MackySoft.Ucli.Tests.OpsCliOutputContractTestSupport;

namespace MackySoft.Ucli.Tests;

public sealed class OpsCliOutputValidationContractTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliContractConstants.CliOption.NameRegex, "[", "nameRegex is invalid")]
    [InlineData(UcliContractConstants.CliOption.Kind, "read", "kind must be one of")]
    [InlineData(UcliContractConstants.CliOption.MaxPolicy, "unsafe", "maxPolicy must be one of")]
    public async Task OpsList_WithInvalidFilterOption_ReturnsInvalidArgumentBeforeProjectResolution (
        string optionName,
        string optionValue,
        string expectedMessage)
    {
        var result = optionName switch
        {
            UcliContractConstants.CliOption.NameRegex => await RunOpsListCommandAsync(nameRegex: optionValue),
            UcliContractConstants.CliOption.Kind => await RunOpsListCommandAsync(operationKind: optionValue),
            UcliContractConstants.CliOption.MaxPolicy => await RunOpsListCommandAsync(maxPolicy: optionValue),
            _ => throw new ArgumentOutOfRangeException(nameof(optionName), optionName, "Unsupported ops list option."),
        };

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.OpsList);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains(expectedMessage, outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliContractConstants.Config.ReadIndexModeAllowStale)]
    [InlineData(UcliContractConstants.Config.ReadIndexModeDisabled)]
    public async Task OpsList_WithInvalidTimeout_ReturnsInvalidArgumentBeforeService (string readIndexMode)
    {
        var result = await RunOpsListCommandAsync(
            readIndexMode: readIndexMode,
            timeout: "abc");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.OpsList);
        CommandResultAssert.HasSingleError(
            outputJson.RootElement,
            expectedCode: "INVALID_ARGUMENT");
        Assert.Contains("timeout", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
    }
}
