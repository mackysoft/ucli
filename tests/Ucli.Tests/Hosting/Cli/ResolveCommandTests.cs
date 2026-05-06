using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class ResolveCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_UsesResolveServiceAndWritesCommandResult ()
    {
        var service = new StubResolveService((_, _) => ValueTask.FromResult(CreateSuccessResult()));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());
        using var cancellationTokenSource = new CancellationTokenSource();

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Resolve(
            projectPath: "/repo/UnityProject",
            mode: "oneshot",
            timeout: "1234",
            readIndexMode: "allowStale",
            failFast: true,
            scene: "Assets/Scenes/Main.unity",
            hierarchyPath: "Root/Child",
            cancellationToken: cancellationTokenSource.Token));

        Assert.Equal((int)CliExitCode.Success, exitCode);
        Assert.Equal(cancellationTokenSource.Token, service.CapturedCancellationToken);
        Assert.NotNull(service.CapturedInput);
        Assert.Equal("/repo/UnityProject", service.CapturedInput!.ProjectPath);
        Assert.Equal(UnityExecutionMode.Oneshot, service.CapturedInput.Mode);
        Assert.Equal(1234, service.CapturedInput.TimeoutMilliseconds);
        Assert.Equal(ReadIndexMode.AllowStale, service.CapturedInput.ReadIndexMode);
        Assert.True(service.CapturedInput.FailFast);
        var selector = Assert.IsType<ResolveSceneHierarchySelectorInput>(service.CapturedInput.Selector);
        Assert.Equal("Assets/Scenes/Main.unity", selector.Scene);
        Assert.Equal("Root/Child", selector.HierarchyPath);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Resolve,
            IpcProtocol.StatusOk,
            (int)CliExitCode.Success);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "uCLI resolve completed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasArrayLength("opResults", 1)
                .HasProperty("opResults", 0, op => op
                    .HasString("opId", "resolve")
                    .HasString("op", UcliPrimitiveOperationNames.Resolve)
                    .HasString("phase", IpcExecuteOperationPhaseNames.Plan)
                    .HasBoolean("applied", false)
                    .HasBoolean("changed", false)
                    .HasProperty("result", result => result
                        .HasString("globalObjectId", "GlobalObjectId_V1-1-2-3-4-5-6")))
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", true)
                    .HasString("source", "index")
                    .HasString("freshness", "fresh")));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenServiceFails_PreservesFailurePayloadAndErrors ()
    {
        var service = new StubResolveService((_, _) => ValueTask.FromResult(CreateFailureResult()));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Resolve(
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, exitCode);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Resolve,
            IpcProtocol.StatusError,
            (int)CliExitCode.ToolError);
        JsonAssert.For(outputJson.RootElement)
            .HasString("message", "Unity execution failed.")
            .HasProperty("payload", payload => payload
                .HasString("requestId", "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62")
                .HasArrayLength("opResults", 0)
                .HasProperty("readIndex", readIndex => readIndex
                    .HasBoolean("used", true)
                    .HasString("source", "index")
                    .HasString("freshness", "fresh")))
            .HasArrayLength("errors", 1)
            .HasProperty("errors", 0, error => error
                .HasString("code", IpcErrorCodes.InternalError)
                .HasString("message", "Unity execution failed.")
                .HasString("opId", "resolve"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenSelectorIsNotExactlyOne_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Resolve(
            assetGuid: "11111111111111111111111111111111",
            assetPath: "Assets/Example.asset",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Resolve,
            IpcProtocol.StatusError,
            (int)CliExitCode.InvalidArgument);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenReadIndexModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Resolve(
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
            readIndexMode: "unsupported",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenModeIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Resolve(
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
            mode: "unsupported",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenTimeoutIsInvalid_ReturnsInvalidArgumentWithoutCallingService ()
    {
        var service = new StubResolveService((_, _) => throw new InvalidOperationException("Service should not be called."));
        var command = new ResolveCommand(service, CommandResultTestWriter.Create());

        var (exitCode, standardOutput) = await StandardOutputCapture.Execute(() => command.Resolve(
            globalObjectId: "GlobalObjectId_V1-1-2-3-4-5-6",
            timeout: "abc",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.InvalidArgument, exitCode);
        Assert.Null(service.CapturedInput);

        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(standardOutput);
        CommandResultAssert.HasSingleError(outputJson.RootElement, IpcErrorCodes.InvalidArgument);
    }

    private static ResolveServiceResult CreateSuccessResult ()
    {
        return ResolveServiceResultFactory.Success(
            "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            [
                new OperationExecutionOperationResult(
                    OpId: "resolve",
                    Op: UcliPrimitiveOperationNames.Resolve,
                    Phase: IpcExecuteOperationPhaseNames.Plan,
                    Applied: false,
                    Changed: false,
                    Touched: [])
                {
                    Result = JsonSerializer.SerializeToElement(new
                    {
                        globalObjectId = "GlobalObjectId_V1-1-2-3-4-5-6",
                    }),
                },
            ],
            new ReadIndexInfo(
                Used: true,
                Hit: true,
                Source: ReadIndexInfoSource.Index,
                Freshness: IndexFreshness.Fresh,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                FallbackReason: null));
    }

    private static ResolveServiceResult CreateFailureResult ()
    {
        return ResolveServiceResultFactory.Failure(
            "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
            [],
            [
                new OperationExecutionError(
                    Code: IpcErrorCodes.InternalError,
                    Message: "Unity execution failed.",
                    OpId: "resolve"),
            ],
            ApplicationOutcome.ToolError,
            new ReadIndexInfo(
                Used: true,
                Hit: true,
                Source: ReadIndexInfoSource.Index,
                Freshness: IndexFreshness.Fresh,
                GeneratedAtUtc: DateTimeOffset.Parse("2026-04-25T00:00:00+00:00"),
                FallbackReason: null));
    }

    private sealed class StubResolveService : IResolveService
    {
        private readonly Func<ResolveCommandInput, CancellationToken, ValueTask<ResolveServiceResult>> handler;

        public StubResolveService (Func<ResolveCommandInput, CancellationToken, ValueTask<ResolveServiceResult>> handler)
        {
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public ResolveCommandInput? CapturedInput { get; private set; }

        public CancellationToken CapturedCancellationToken { get; private set; }

        public ValueTask<ResolveServiceResult> Execute (
            ResolveCommandInput input,
            CancellationToken cancellationToken = default)
        {
            CapturedInput = input;
            CapturedCancellationToken = cancellationToken;
            return handler(input, cancellationToken);
        }
    }
}
