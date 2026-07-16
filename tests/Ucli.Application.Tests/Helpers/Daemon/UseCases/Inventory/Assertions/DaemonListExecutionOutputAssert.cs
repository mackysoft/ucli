using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Inventory;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonListExecutionOutputAssert
{
    public static void CompleteRunningWorktreesSortedByPath (
        DaemonListExecutionOutput output,
        int expectedTimeoutMilliseconds,
        string expectedProjectRelativePath,
        params RunningWorktreeItem[] expectedItems)
    {
        Assert.Equal(expectedTimeoutMilliseconds, output.TimeoutMilliseconds);
        Assert.Equal(expectedProjectRelativePath, output.ProjectRelativePath);
        Assert.True(output.IsComplete);
        Assert.Null(output.CompletionReason);
        Assert.Equal(0, output.RemainingWorktreeCount);
        Assert.Equal(
            output.Items.Select(static item => item.WorktreePath).Order(StringComparer.Ordinal).ToArray(),
            output.Items.Select(static item => item.WorktreePath).ToArray());
        Assert.Collection(
            output.Items,
            expectedItems
                .Select<RunningWorktreeItem, Action<DaemonListItemOutput>>(expected => item => RunningWorktreeItemMatches(item, expected))
                .ToArray());
    }

    private static void RunningWorktreeItemMatches (
        DaemonListItemOutput item,
        RunningWorktreeItem expected)
    {
        Assert.Equal(expected.WorktreePath, item.WorktreePath);
        Assert.Equal(expected.BranchRef, item.BranchRef);
        Assert.Equal(expected.Head, item.Head);
        Assert.Equal(expected.ProjectPath, item.ProjectPath);
        Assert.Equal(expected.ProjectFingerprint, item.ProjectFingerprint);
        Assert.Equal(DaemonListItemState.Running, item.State);
        Assert.Null(item.Reason);
        Assert.Equal(expected.ProcessId, item.ProcessId);
        Assert.Equal(expected.EditorMode, item.EditorMode);
        Assert.Equal(expected.OwnerKind, item.OwnerKind);
        Assert.Equal(expected.CanShutdownProcess, item.CanShutdownProcess);
        Assert.Equal(expected.EndpointAddress, item.EndpointAddress);
        Assert.Null(item.Diagnosis);
    }

    internal readonly record struct RunningWorktreeItem (
        string WorktreePath,
        string? BranchRef,
        string Head,
        string ProjectPath,
        ProjectFingerprint ProjectFingerprint,
        int ProcessId,
        DaemonEditorMode EditorMode,
        DaemonSessionOwnerKind OwnerKind,
        bool CanShutdownProcess,
        string EndpointAddress);
}
