using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonListServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenContextResolutionFails_ReturnsFailure ()
    {
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(ExecutionError.InvalidArgument("invalid project")));
        var queryService = new StubDaemonListQueryService();
        var service = new DaemonListService(resolver, queryService);

        var result = await service.GetList(projectPath: "/tmp/project", timeoutMilliseconds: 1000, cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        Assert.Equal("invalid project", result.Error!.Message);
        Assert.Equal(0, queryService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenSuccessful_PropagatesResolvedProjectAndTimeout ()
    {
        var context = DaemonServiceTestContext.CreateExecutionContext(timeoutMilliseconds: 4321);
        var resolver = new DaemonServiceTestContext.StubDaemonCommandExecutionContextResolver(
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
        var service = new DaemonListService(resolver, queryService);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await service.GetList(
            projectPath: "/tmp/unity-project",
            timeoutMilliseconds: 4321,
            cancellationToken: cancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(UcliCommandIds.DaemonList, resolver.LastTimeoutCommand);
        Assert.Equal("/tmp/unity-project", resolver.LastProjectPath);
        Assert.Equal(4321, resolver.LastTimeoutMilliseconds);
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
