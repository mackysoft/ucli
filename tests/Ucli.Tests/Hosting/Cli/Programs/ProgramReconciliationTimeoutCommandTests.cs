using System.Runtime.CompilerServices;
using MackySoft.Ucli.Application.Features.Programs.Persistence;
using MackySoft.Ucli.Application.Features.Programs.Supervision;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Hosting.Cli.Programs;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class ProgramReconciliationTimeoutCommandTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task StatusAsync_WhenReconciliationDeadlineElapses_ReturnsTimeoutWithoutChangingRun ()
    {
        var store = new BlockingStore();
        var command = CreateStatusCommand(store);

        var result = await CommandResultCapture.ExecuteAsync(() => command.StatusAsync(Guid.NewGuid(), timeout: "1"));

        Assert.Equal(4, result.ExitCode);
        using var output = JsonAssert.ParseMultilineObject(result.StdOut);
        Assert.Equal("error", output.RootElement.GetProperty("status").GetString());
        Assert.Equal("PROGRAM_STATUS_TIMEOUT", output.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Same(store.Initial, store.Current);
        Assert.Equal(0, store.CompareExchangeCount);
        Assert.Equal(0, store.TerminalPublicationCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task CancelAsync_WhenReconciliationDeadlineElapses_ReturnsTimeoutWithoutChangingRun ()
    {
        var store = new BlockingStore();
        var command = CreateCancelCommand(store);

        var result = await CommandResultCapture.ExecuteAsync(() => command.CancelAsync(Guid.NewGuid(), timeout: "1"));

        Assert.Equal(4, result.ExitCode);
        using var output = JsonAssert.ParseMultilineObject(result.StdOut);
        Assert.Equal("error", output.RootElement.GetProperty("status").GetString());
        Assert.Equal("PROGRAM_CANCEL_TIMEOUT", output.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Same(store.Initial, store.Current);
        Assert.Equal(0, store.CompareExchangeCount);
        Assert.Equal(0, store.TerminalPublicationCount);
    }

    private static ProgramStatusCommand CreateStatusCommand (BlockingStore store) => new(CreateResolver(), CreateReconciliation(store), store, CommandResultTestWriter.Create());
    private static ProgramCancelCommand CreateCancelCommand (BlockingStore store) => new(CreateResolver(), CreateReconciliation(store), store, CommandResultTestWriter.Create());
    private static ProgramRunStatusCancelReconciliationService CreateReconciliation (BlockingStore store) => new(store, new AliveProcessObserver(), TimeProvider.System);
    private static IProjectContextResolver CreateResolver () => new FixedProjectContextResolver(new ProjectContext(ResolvedUnityProjectContextTestFactory.Create(), UcliConfig.CreateDefault(), ConfigSource.Default));

    private sealed class FixedProjectContextResolver (ProjectContext context) : IProjectContextResolver
    {
        public ValueTask<ProjectContextResolutionResult> ResolveAsync (AbsolutePath? projectPath, CancellationToken cancellationToken = default) => ValueTask.FromResult(ProjectContextResolutionResult.Success(context));
    }

    private sealed class AliveProcessObserver : IProcessIdentityObserver
    {
        public ProcessIdentityStatus Observe (ProcessIdentity process) => ProcessIdentityStatus.Matching;
    }

    private sealed class BlockingStore : IProgramRunStoreFactory, IProgramRunStore
    {
        public ProgramRunRecord Initial { get; } = (ProgramRunRecord)RuntimeHelpers.GetUninitializedObject(typeof(ProgramRunRecord));
        public ProgramRunRecord Current { get; private set; }
        public int CompareExchangeCount { get; private set; }
        public int TerminalPublicationCount { get; private set; }
        public BlockingStore () => Current = Initial;
        public IProgramRunStore ForProject (ResolvedUnityProjectContext project) => this;
        public ValueTask<ArtifactRef> PublishDefinitionSnapshotAsync (Guid runId, ProgramDefinitionSnapshot snapshot, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProgramRunStoreCreateResult> CreateAsync (ProgramRunRecord run, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async ValueTask<ProgramRunRecord?> ReadAsync (Guid runId, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return Current;
        }
        public ValueTask<ProgramRunStoredDefinition?> ReadDefinitionAsync (Guid runId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<ProgramRunStoreCompareExchangeResult> CompareExchangeAsync (ProgramRunRecord expected, ProgramRunRecord replacement, CancellationToken cancellationToken = default) { CompareExchangeCount++; throw new NotSupportedException(); }
        public ValueTask<ProgramRunTerminalPublicationResult> PublishRunTerminalAsync (ProgramRunRecord expected, ProgramRunTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default) { TerminalPublicationCount++; throw new NotSupportedException(); }
        public ValueTask<ProgramRunTerminalPublicationResult> PublishRunTimeoutTerminalAsync (ProgramRunRecord expected, int stepIndex, ProgramRunTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default) { TerminalPublicationCount++; throw new NotSupportedException(); }
        public ValueTask<ProgramRunStepTerminalPublicationResult> PublishStepTerminalAsync (ProgramRunRecord expected, int stepIndex, ProgramStepTerminalRecord terminalRecord, Func<ArtifactRef, ProgramRunRecord> createReplacement, CancellationToken cancellationToken = default) { TerminalPublicationCount++; throw new NotSupportedException(); }
    }
}
