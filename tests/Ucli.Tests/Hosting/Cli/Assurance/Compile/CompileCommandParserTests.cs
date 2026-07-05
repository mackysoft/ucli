using MackySoft.Tests;

namespace MackySoft.Ucli.Tests;

public sealed class CompileCommandParserTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Compile_ProcessWithInvalidFormat_ReturnsInvalidArgument ()
    {
        var result = await CliInProcessRunner.RunCommandAsync(
            UcliCommandNames.Compile,
            "--format",
            "yaml");

        Assert.Equal((int)CliExitCode.InvalidArgument, result.ExitCode);
        Assert.True(string.IsNullOrEmpty(result.StdErr), result.StdErr);
        CommandResultAssert.HasInvalidArgumentOutput(result.StdOut, UcliCommandNames.Compile);
        CommandResultAssert.DoesNotReportUnrecognizedArguments(result.StdErr, "--format");
    }
}
