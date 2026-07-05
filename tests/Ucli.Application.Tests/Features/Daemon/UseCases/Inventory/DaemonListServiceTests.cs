using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonListServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenContextResolutionFails_ReturnsFailure ()
    {
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Failure(ExecutionError.InvalidArgument("invalid project")));
        var queryService = new RecordingDaemonListQueryService(CreateSuccessfulListResult());
        var service = new DaemonListService(resolver, queryService);

        var result = await service.GetListAsync(projectPath: "/tmp/project", timeoutMilliseconds: 1000, cancellationToken: CancellationToken.None);

        DaemonListQueryServiceAssert.ContextResolutionFailureStoppedBeforeListQuery(
            result,
            queryService,
            "invalid project");
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task List_WhenSuccessful_PropagatesResolvedProjectAndTimeout ()
    {
        var context = DaemonCommandExecutionContextTestFactory.Create(timeoutMilliseconds: 4321);
        var resolver = new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(context));
        var queryService = new RecordingDaemonListQueryService(DaemonListExecutionResult.Success(new DaemonListExecutionOutput(
                TimeoutMilliseconds: 4321,
                ProjectRelativePath: "UnityProject",
                IsComplete: true,
                CompletionReason: null,
                RemainingWorktreeCount: 0,
                Items: Array.Empty<DaemonListItemOutput>())));
        var service = new DaemonListService(resolver, queryService);
        using var cancellationSource = new CancellationTokenSource();
        var cancellationToken = cancellationSource.Token;

        var result = await service.GetListAsync(
            projectPath: "/tmp/unity-project",
            timeoutMilliseconds: 4321,
            cancellationToken: cancellationToken);

        Assert.True(result.IsSuccess);
        DaemonCommandExecutionContextResolverAssert.ResolvedFor(
            resolver,
            UcliCommandIds.DaemonList,
            expectedProjectPath: "/tmp/unity-project",
            expectedTimeoutMilliseconds: 4321,
            expectedCancellationToken: cancellationToken);
        DaemonListQueryServiceAssert.ListRequestedOnce(
            queryService,
            context,
            cancellationToken);
    }

    private static DaemonListExecutionResult CreateSuccessfulListResult ()
    {
        return DaemonListExecutionResult.Success(new DaemonListExecutionOutput(
            TimeoutMilliseconds: 1000,
            ProjectRelativePath: ".",
            IsComplete: true,
            CompletionReason: null,
            RemainingWorktreeCount: 0,
            Items: Array.Empty<DaemonListItemOutput>()));
    }
}
