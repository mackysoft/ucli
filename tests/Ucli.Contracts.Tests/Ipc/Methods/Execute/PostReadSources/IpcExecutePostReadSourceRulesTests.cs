using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Execute;

public sealed class IpcExecutePostReadSourceRulesTests
{
    private static readonly PostReadSourceCompatibilityCase[] CompatibleSourceCases =
    [
        new("edit", "edit", PlayModeMutation: false, Commit: "none", PersistenceExpected: false, ExpectedPostState: "deterministic"),
        new("edit", "edit", PlayModeMutation: false, Commit: "context", PersistenceExpected: true, ExpectedPostState: "deterministic"),
        new("edit", "edit", PlayModeMutation: false, Commit: "project", PersistenceExpected: true, ExpectedPostState: "deterministic"),
        new("edit", "edit", PlayModeMutation: true, Commit: "none", PersistenceExpected: false, ExpectedPostState: "unavailable"),
        new("edit", "edit", PlayModeMutation: true, Commit: "none", PersistenceExpected: true, ExpectedPostState: "unavailable"),
        new("ucli.scene.open", "operation", PlayModeMutation: false, Commit: null, PersistenceExpected: false, ExpectedPostState: "unavailable"),
        new("ucli.scene.open", "operation", PlayModeMutation: false, Commit: null, PersistenceExpected: true, ExpectedPostState: "unavailable"),
        new("ucli.project.refresh", "refresh", PlayModeMutation: false, Commit: null, PersistenceExpected: true, ExpectedPostState: "unavailable"),
    ];

    private static readonly PostReadSourceCompatibilityCase[] IncompatibleSourceCases =
    [
        new("edit", "edit", PlayModeMutation: true, Commit: "none", PersistenceExpected: false, ExpectedPostState: "deterministic"),
        new("edit", "edit", PlayModeMutation: false, Commit: "context", PersistenceExpected: false, ExpectedPostState: "deterministic"),
        new("edit", "edit", PlayModeMutation: false, Commit: null, PersistenceExpected: true, ExpectedPostState: "deterministic"),
        new("edit", "edit", PlayModeMutation: false, Commit: "invalid", PersistenceExpected: true, ExpectedPostState: "deterministic"),
        new("edit", "edit", PlayModeMutation: false, Commit: "none", PersistenceExpected: false, ExpectedPostState: "unavailable"),
        new("edit", "edit", PlayModeMutation: true, Commit: "context", PersistenceExpected: true, ExpectedPostState: "unavailable"),
        new("ucli.scene.open", "operation", PlayModeMutation: true, Commit: null, PersistenceExpected: false, ExpectedPostState: "unavailable"),
        new("ucli.scene.open", "operation", PlayModeMutation: false, Commit: "none", PersistenceExpected: false, ExpectedPostState: "unavailable"),
        new("ucli.scene.open", "operation", PlayModeMutation: false, Commit: null, PersistenceExpected: false, ExpectedPostState: "deterministic"),
        new("ucli.project.refresh", "operation", PlayModeMutation: false, Commit: null, PersistenceExpected: false, ExpectedPostState: "unavailable"),
        new("ucli.scene.open", "refresh", PlayModeMutation: false, Commit: null, PersistenceExpected: true, ExpectedPostState: "unavailable"),
        new("ucli.project.refresh", "refresh", PlayModeMutation: true, Commit: null, PersistenceExpected: true, ExpectedPostState: "unavailable"),
        new("ucli.project.refresh", "refresh", PlayModeMutation: false, Commit: null, PersistenceExpected: false, ExpectedPostState: "unavailable"),
        new("ucli.project.refresh", "refresh", PlayModeMutation: false, Commit: "none", PersistenceExpected: true, ExpectedPostState: "unavailable"),
        new("ucli.project.refresh", "refresh", PlayModeMutation: false, Commit: null, PersistenceExpected: true, ExpectedPostState: "deterministic"),
    ];

    private static readonly DeterministicMutationSourceCase[] DeterministicMutationSourceCases =
    [
        new("edit", "deterministic", ExpectedResult: true),
        new("edit", "unavailable", ExpectedResult: false),
        new("operation", "unavailable", ExpectedResult: false),
        new("refresh", "unavailable", ExpectedResult: false),
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
        string SourceKind,
        bool PlayModeMutation,
        string? Commit,
        bool PersistenceExpected,
        string ExpectedPostState);

    private sealed record DeterministicMutationSourceCase (
        string SourceKind,
        string ExpectedPostState,
        bool ExpectedResult);
}
