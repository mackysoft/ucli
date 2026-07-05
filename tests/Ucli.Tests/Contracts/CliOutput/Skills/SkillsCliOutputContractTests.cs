using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

[Collection(CurrentDirectoryTestCollection.Name)]
public sealed class SkillsCliOutputContractTests
{
    private const string InvalidArgumentCode = "INVALID_ARGUMENT";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Skills_WithoutSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(UcliCommandNames.Skills);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Skills);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Skills_WithUnknownSubcommand_ReturnsJsonEnvelopeError ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(UcliCommandNames.Skills, "unknown");

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Skills);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(UcliCommandNames.ListSubcommand, UcliCommandNames.SkillsList)]
    [InlineData(UcliCommandNames.InstallSubcommand, UcliCommandNames.SkillsInstall)]
    public async Task SkillsRepresentativeSubcommand_WithUnknownOption_ReturnsInvalidArgumentAsSingleJson (
        string subcommand,
        string expectedCommand)
    {
        var result = await CliInProcessRunner.RunCommandAsync(UcliCommandNames.Skills, subcommand, UcliContractConstants.CliOption.Unknown);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentEnvelope(
            outputJson.RootElement,
            expectedCommand);
        CommandResultAssert.HasSingleError(outputJson.RootElement, InvalidArgumentCode);
        CommandResultAssert.ReportsUnrecognizedArgument(
            outputJson.RootElement.GetProperty("message").GetString(),
            UcliContractConstants.CliOption.Unknown);
    }
}
