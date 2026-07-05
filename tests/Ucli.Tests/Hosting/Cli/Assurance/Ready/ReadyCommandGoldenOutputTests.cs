using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ReadyCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ReadyCommandGoldenOutputTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Ready_WithAutoOneshotOutput_MatchesGolden ()
    {
        var service = new RecordingReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateOutput())));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "execution",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ready", "auto-oneshot-success.json"),
            result.StdOut,
            CreateReadyGoldenNormalization());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Ready_WithReadIndexOutput_MatchesGolden ()
    {
        var service = new RecordingReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Success(CreateReadIndexOutput())));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "readIndex",
            readIndexMode: "allowStale",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("ready", "read-index-success.json"),
            result.StdOut,
            CreateReadyGoldenNormalization());
    }
}
