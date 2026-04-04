using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Cli;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Execution.OperationExecute;
using MackySoft.Ucli.Refresh;

namespace MackySoft.Ucli.Tests;

public sealed class RefreshCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Refresh_UsesRefreshServiceAndWritesCommandResult ()
    {
        var service = new StubRefreshService((_, _, _) => ValueTask.FromResult(new OperationExecuteResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            OpResults:
            [
                new IpcExecuteOperationResult(
                    OpId: "refresh",
                    Op: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh,
                    Phase: IpcExecuteOperationPhaseNames.Call,
                    Applied: true,
                    Changed: true,
                    Touched:
                    [
                        new IpcExecuteTouchedResource(
                            Kind: IpcExecuteTouchedResourceKindNames.Asset,
                            Path: "Assets/Example.txt",
                            Guid: null),
                    ]),
            ],
            Errors: [],
            ExitCode: (int)CliExitCode.Success)));
        var command = new RefreshCommand(service);
        using var cancellationTokenSource = new CancellationTokenSource();

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Refresh(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        Assert.Equal("/repo/UnityProject", service.CapturedProjectPath);
        Assert.Equal("oneshot", service.CapturedMode);
        Assert.Equal("1234", service.CapturedTimeout);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Refresh,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI refresh completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasArrayLength("opResults", 1)
                .HasProperty("opResults", 0, op => op
                    .HasString("opId", "refresh")
                    .HasString("op", MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh)
                    .HasString("phase", "call")
                    .HasBoolean("applied", true)
                    .HasBoolean("changed", true)
                    .HasArrayLength("touched", 1)));
    }

    private sealed class StubRefreshService : IRefreshService
    {
        private readonly Func<string?, string?, string?, ValueTask<OperationExecuteResult>> handler;

        public StubRefreshService (Func<string?, string?, string?, ValueTask<OperationExecuteResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public string? CapturedProjectPath { get; private set; }

        public string? CapturedMode { get; private set; }

        public string? CapturedTimeout { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<OperationExecuteResult> Execute (
            string? projectPath,
            string? mode,
            string? timeout,
            CancellationToken cancellationToken = default)
        {
            CapturedProjectPath = projectPath;
            CapturedMode = mode;
            CapturedTimeout = timeout;
            CapturedCancellationToken = cancellationToken;
            return handler(projectPath, mode, timeout);
        }
    }
}