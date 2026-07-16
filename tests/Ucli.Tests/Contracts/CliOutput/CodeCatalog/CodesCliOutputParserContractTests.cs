namespace MackySoft.Ucli.Tests;

public sealed class CodesCliOutputParserContractTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Codes_WithMissingOrUnknownSubcommand_ReturnsJsonEnvelopeError ()
    {
        foreach (string[] args in new string[][]
        {
            [],
            ["unknown"],
        })
        {
            var result = await CliInProcessRunner.RunCommandAsync([UcliCommandNames.Codes, .. args]);

            Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
            CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.Codes);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ErrorsCommand_IsNotPublicCommand ()
    {
        foreach (string[] args in new string[][]
        {
            ["errors"],
            ["errors", "list"],
        })
        {
            var result = await CliInProcessRunner.RunCommandAsync(args);

            using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
            Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
            CommandResultAssert.HasInvalidArgumentError(outputJson.RootElement, UcliCommandNames.Root);
            Assert.Contains("Command 'errors' is not recognized.", outputJson.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
        }
    }
}
