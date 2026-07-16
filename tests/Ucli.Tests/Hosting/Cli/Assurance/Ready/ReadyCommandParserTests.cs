namespace MackySoft.Ucli.Tests;

public sealed class ReadyCommandParserTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Ready_WithReadIndexModeOptionAliases_AreAcceptedByParser ()
    {
        await using var serviceProvider = UcliServiceProviderTestFactory.CreateCore();

        await ConsoleAppRunner.RunWithRegisteredAppAsync(serviceProvider, async app =>
        {
            foreach (var readIndexModeOption in ReadyCommandTestData.GetReadIndexModeOptionSpellings())
            {
                var result = await ConsoleAppRunner.RunAsync(
                    app,
                    UcliCommandNames.Ready,
                    "--for",
                    "readIndex",
                    readIndexModeOption,
                    "allowStale",
                    UcliContractConstants.CliOption.Timeout,
                    "abc");

                Assert.True(
                    result.ExitCode == (int)CliExitCode.InvalidArgument,
                    $"{readIndexModeOption} must dispatch to ready command validation.");
                CommandResultAssert.DoesNotReportUnrecognizedArguments(result.StdErr, readIndexModeOption);
                using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
                CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Ready);
                Assert.Contains("timeout must be a positive integer", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
            }
        });
    }
}
