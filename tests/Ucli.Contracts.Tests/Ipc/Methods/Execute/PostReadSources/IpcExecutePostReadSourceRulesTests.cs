using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Execute;

public sealed class IpcExecutePostReadSourceRulesTests
{
    private static readonly PostReadSourceCompatibilityCase[] CompatibleSourceCases =
    [
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: false, Commit: IpcExecutePostReadCommit.None, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: false, Commit: IpcExecutePostReadCommit.Context, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: false, Commit: IpcExecutePostReadCommit.Project, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: true, Commit: IpcExecutePostReadCommit.None, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: true, Commit: IpcExecutePostReadCommit.None, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.scene.open", IpcExecutePostReadSourceKind.Operation, PlayModeMutation: false, Commit: null, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.scene.open", IpcExecutePostReadSourceKind.Operation, PlayModeMutation: false, Commit: null, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.project.refresh", IpcExecutePostReadSourceKind.Refresh, PlayModeMutation: false, Commit: null, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
    ];

    private static readonly PostReadSourceCompatibilityCase[] IncompatibleSourceCases =
    [
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: true, Commit: IpcExecutePostReadCommit.None, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: false, Commit: IpcExecutePostReadCommit.Context, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: false, Commit: null, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: false, Commit: (IpcExecutePostReadCommit)int.MaxValue, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: false, Commit: IpcExecutePostReadCommit.None, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("edit", IpcExecutePostReadSourceKind.Edit, PlayModeMutation: true, Commit: IpcExecutePostReadCommit.Context, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.scene.open", IpcExecutePostReadSourceKind.Operation, PlayModeMutation: true, Commit: null, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.scene.open", IpcExecutePostReadSourceKind.Operation, PlayModeMutation: false, Commit: IpcExecutePostReadCommit.None, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.scene.open", IpcExecutePostReadSourceKind.Operation, PlayModeMutation: false, Commit: null, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
        new("ucli.project.refresh", IpcExecutePostReadSourceKind.Operation, PlayModeMutation: false, Commit: null, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.scene.open", IpcExecutePostReadSourceKind.Refresh, PlayModeMutation: false, Commit: null, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.project.refresh", IpcExecutePostReadSourceKind.Refresh, PlayModeMutation: true, Commit: null, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.project.refresh", IpcExecutePostReadSourceKind.Refresh, PlayModeMutation: false, Commit: null, PersistenceExpected: false, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.project.refresh", IpcExecutePostReadSourceKind.Refresh, PlayModeMutation: false, Commit: IpcExecutePostReadCommit.None, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Unavailable),
        new("ucli.project.refresh", IpcExecutePostReadSourceKind.Refresh, PlayModeMutation: false, Commit: null, PersistenceExpected: true, ExpectedPostState: IpcExecuteExpectedPostState.Deterministic),
    ];

    private static readonly DeterministicMutationSourceCase[] DeterministicMutationSourceCases =
    [
        new(IpcExecutePostReadSourceKind.Edit, IpcExecuteExpectedPostState.Deterministic, ExpectedResult: true),
        new(IpcExecutePostReadSourceKind.Edit, IpcExecuteExpectedPostState.Unavailable, ExpectedResult: false),
        new(IpcExecutePostReadSourceKind.Operation, IpcExecuteExpectedPostState.Unavailable, ExpectedResult: false),
        new(IpcExecutePostReadSourceKind.Refresh, IpcExecuteExpectedPostState.Unavailable, ExpectedResult: false),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void IsCompatibleWithOperation_WithValidSourceFacts_ReturnsTrue ()
    {
        foreach (var testCase in CompatibleSourceCases)
        {
            var result = IpcExecutePostReadSourceRules.IsCompatibleWithOperation(
                testCase.OperationName,
                testCase.SourceKind,
                testCase.PlayModeMutation,
                testCase.Commit,
                testCase.PersistenceExpected,
                testCase.ExpectedPostState);

            Assert.True(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsCompatibleWithOperation_WithInvalidSourceFacts_ReturnsFalse ()
    {
        foreach (var testCase in IncompatibleSourceCases)
        {
            var result = IpcExecutePostReadSourceRules.IsCompatibleWithOperation(
                testCase.OperationName,
                testCase.SourceKind,
                testCase.PlayModeMutation,
                testCase.Commit,
                testCase.PersistenceExpected,
                testCase.ExpectedPostState);

            Assert.False(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsDeterministicMutationSource_ReturnsExpectedResult ()
    {
        foreach (var testCase in DeterministicMutationSourceCases)
        {
            var result = IpcExecutePostReadSourceRules.IsDeterministicMutationSource(
                testCase.SourceKind,
                testCase.ExpectedPostState);

            Assert.Equal(testCase.ExpectedResult, result);
        }
    }

    private sealed record PostReadSourceCompatibilityCase (
        string OperationName,
        IpcExecutePostReadSourceKind SourceKind,
        bool PlayModeMutation,
        IpcExecutePostReadCommit? Commit,
        bool PersistenceExpected,
        IpcExecuteExpectedPostState ExpectedPostState);

    private sealed record DeterministicMutationSourceCase (
        IpcExecutePostReadSourceKind SourceKind,
        IpcExecuteExpectedPostState ExpectedPostState,
        bool ExpectedResult);
}
