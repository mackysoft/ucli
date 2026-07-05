using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartServiceSuccessTests
{
    public static TheoryData<string, int> PreservedRunningStatusCases => new()
    {
        {
            "already-running",
            (int)DaemonStartStatus.AlreadyRunning
        },
        {
            "attached",
            (int)DaemonStartStatus.Attached
        },
    };

    [Fact]
    [Trait("Size", "Small")]
    public async Task Start_WhenSupervisorReturnsStarted_ReturnsRunningOutputWithMappedSession ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create();
        var lifecycleSnapshot = new DaemonStartLifecycleSnapshot(
            IpcEditorLifecycleStateCodec.Compiling,
            IpcEditorBlockingReasonCodec.Compile,
            CanAcceptExecutionRequests: false);
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(session, lifecycleSnapshot),
        };
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway);

        var result = await service.StartAsync(projectPath: null, timeoutMilliseconds: null, editorMode: null, onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        var output = Assert.IsType<DaemonStartExecutionOutput>(result.Output);
        Assert.Equal(DaemonStartStatus.Started, output.StartStatus);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
        Assert.Equal(1200, output.TimeoutMilliseconds);
        DaemonServiceOutputAssert.SessionMatches(session, output.Session);
        Assert.Equal(IpcEditorLifecycleStateCodec.Compiling, output.LifecycleState);
        Assert.Equal(IpcEditorBlockingReasonCodec.Compile, output.BlockingReason);
        Assert.False(output.CanAcceptExecutionRequests);
        var invocation = DaemonProjectLifecycleGatewayAssert.EnsureRunningRequested(
            supervisorProjectGateway,
            context.Context.UnityProject,
            context.Timeout);
        Assert.Null(invocation.SupervisorProgressSink);
    }

    [Theory]
    [InlineData(DaemonEditorMode.Gui, DaemonStartupBlockedProcessPolicy.Auto)]
    [InlineData(null, DaemonStartupBlockedProcessPolicy.Terminate)]
    [Trait("Size", "Small")]
    public async Task Start_WithLifecycleGatewayOptions_PropagatesOptionsToLifecycleGateway (
        DaemonEditorMode? editorMode,
        DaemonStartupBlockedProcessPolicy onStartupBlocked)
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            EnsureRunningResult = DaemonStartResult.Started(DaemonSessionTestFactory.Create()),
        };
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway);

        var result = await service.StartAsync(
            projectPath: null,
            timeoutMilliseconds: null,
            editorMode: editorMode,
            onStartupBlocked: onStartupBlocked,
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        DaemonProjectLifecycleGatewayAssert.EnsureRunningRequested(
            supervisorProjectGateway,
            context.Context.UnityProject,
            context.Timeout,
            editorMode,
            onStartupBlocked);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(PreservedRunningStatusCases))]
    public async Task Start_WhenSupervisorReturnsRunningStatus_PreservesStartStatus (
        string caseName,
        int expectedStartStatus)
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(
            timeoutMilliseconds: 1200,
            repositoryRoot: "/tmp/repo-root");
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var session = DaemonSessionTestFactory.Create();
        var supervisorProjectGateway = new RecordingDaemonProjectLifecycleGateway
        {
            EnsureRunningResult = caseName switch
            {
                "already-running" => DaemonStartResult.AlreadyRunning(session),
                "attached" => DaemonStartResult.Attached(session),
                _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unsupported running status case."),
            },
        };
        var service = DaemonStartServiceTestSupport.CreateService(resolver, supervisorProjectGateway);

        var result = await service.StartAsync(projectPath: null, timeoutMilliseconds: null, editorMode: null, onStartupBlocked: DaemonStartupBlockedProcessPolicy.Auto, cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess, $"{caseName} must return success.");
        var output = Assert.IsType<DaemonStartExecutionOutput>(result.Output);
        Assert.Equal((DaemonStartStatus)expectedStartStatus, output.StartStatus);
        Assert.Equal(DaemonStatusKind.Running, output.DaemonStatus);
    }
}
