namespace MackySoft.Ucli.Tests;

public sealed class GameViewRecordingCommandParserTests
{
    [Theory]
    [InlineData(UcliCommandNames.StartSubcommand)]
    [InlineData(UcliCommandNames.Status)]
    [InlineData(UcliCommandNames.StopSubcommand)]
    [Trait("Size", "Medium")]
    public async Task RecordingCommand_WithNonCanonicalRecordingId_ReturnsInvalidArgument (string subcommand)
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Recording,
            subcommand,
            "--recordingId",
            "00000000-0000-0000-0000-000000000000");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        CommandResultAssert.HasInvalidArgumentOutput(
            result.StdOut,
            subcommand switch
            {
                UcliCommandNames.StartSubcommand => UcliCommandNames.RecordingStart,
                UcliCommandNames.Status => UcliCommandNames.RecordingStatus,
                UcliCommandNames.StopSubcommand => UcliCommandNames.RecordingStop,
                _ => throw new ArgumentOutOfRangeException(nameof(subcommand)),
            });
        CommandResultAssert.DoesNotReportUnrecognizedArguments(result.StdErr, "--recordingId");
    }
}
