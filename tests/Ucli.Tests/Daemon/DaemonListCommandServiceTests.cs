using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonListCommandServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenContextResolutionFails_ReturnsFailure ()
    {
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(ExecutionError.InvalidArgument("invalid project")));
        var queryService = new StubDaemonListQueryService();
        var service = new DaemonListCommandService(resolver, queryService);

        var result = await service.GetList(projectPath: "/tmp/project", timeout: "1000", cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal("invalid project", result.Error!.Message);
        Assert.Equal(0, queryService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenSuccessful_PropagatesResolvedProjectAndTimeout ()
    {
        var context = DaemonCommandServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 4321);
        var resolver = new DaemonCommandServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var queryService = new StubDaemonListQueryService
        {
            Result = DaemonListExecutionResult.Success(new DaemonListExecutionOutput(
                TimeoutMilliseconds: 4321,
                ProjectRelativePath: "UnityProject",
                IsComplete: true,
                CompletionReason: null,
                RemainingWorktreeCount: 0,
                Items: Array.Empty<DaemonListItemOutput>())),
        };
        var service = new DaemonListCommandService(resolver, queryService);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await service.GetList(
            projectPath: "/tmp/unity-project",
            timeout: "4321",
            cancellationToken: cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(UcliCommandIds.DaemonList, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/unity-project", resolver.LastProjectPath);
        Assert.Equal("4321", resolver.LastTimeoutOption);
        Assert.Equal(cancellationToken, resolver.LastCancellationToken);
        Assert.Equal(1, queryService.CallCount);
        Assert.Equal(context.Context.UnityProject, queryService.LastUnityProject);
        Assert.Equal(context.Timeout, queryService.LastTimeout);
        Assert.Equal(cancellationToken, queryService.LastCancellationToken);
    }

    private sealed class StubDaemonListQueryService : IDaemonListQueryService
    {
        public DaemonListExecutionResult Result { get; set; } = DaemonListExecutionResult.Success(new DaemonListExecutionOutput(
            TimeoutMilliseconds: 1000,
            ProjectRelativePath: ".",
            IsComplete: true,
            CompletionReason: null,
            RemainingWorktreeCount: 0,
            Items: Array.Empty<DaemonListItemOutput>()));

        public int CallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonListExecutionResult> GetList (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(Result);
        }
    }
}