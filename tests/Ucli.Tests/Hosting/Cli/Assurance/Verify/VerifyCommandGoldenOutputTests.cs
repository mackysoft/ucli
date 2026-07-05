using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.VerifyCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class VerifyCommandGoldenOutputTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("text")]
    [InlineData("json")]
    [Trait("Size", "Medium")]
    public async Task Verify_WithDefaultOrSupportedFormat_WritesOnlyFinalCommandResult (string? format)
    {
        var service = new RecordingVerifyService((_, _, _) => ValueTask.FromResult(VerifyExecutionResult.Success(CreateOutput())));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.VerifyAsync(
            format: format,
            cancellationToken: CancellationToken.None));

        Assert.True(
            result.ExitCode == (int)CliExitCode.Success,
            $"Verify format `{format ?? "<default>"}` must return success.");
        Assert.Equal(string.Empty, result.StdErr);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("verify", "default-success.json"),
            result.StdOut,
            CreateGoldenNormalization());
    }

    [Theory]
    [InlineData(VerifyVerdictValues.Fail)]
    [InlineData(VerifyVerdictValues.Incomplete)]
    [Trait("Size", "Small")]
    public async Task Verify_WithNonPassVerdict_ReturnsOkEnvelopeWithFailureExitCode (string verdict)
    {
        var service = new RecordingVerifyService((_, _, _) => ValueTask.FromResult(VerifyExecutionResult.Success(CreateOutput(verdict))));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.VerifyAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Verify,
            IpcProtocol.StatusOk,
            1);
        Assert.Equal(verdict, outputJson.RootElement.GetProperty("payload").GetProperty("verdict").GetString());
    }
}
