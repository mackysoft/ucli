using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Methods.Execute;

public sealed class IpcExecutePostReadSourceRulesTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("edit", "edit", false, "none", false, "deterministic")]
    [InlineData("edit", "edit", false, "context", true, "deterministic")]
    [InlineData("edit", "edit", false, "project", true, "deterministic")]
    [InlineData("edit", "edit", true, "none", false, "unavailable")]
    [InlineData("edit", "edit", true, "none", true, "unavailable")]
    [InlineData("ucli.scene.open", "operation", false, null, false, "unavailable")]
    [InlineData("ucli.scene.open", "operation", false, null, true, "unavailable")]
    [InlineData("ucli.project.refresh", "refresh", false, null, true, "unavailable")]
    public void IsCompatibleWithOperation_WithValidSourceFacts_ReturnsTrue (
        string operationName,
        string sourceKind,
        bool playModeMutation,
        string? commit,
        bool persistenceExpected,
        string expectedPostState)
    {
        var result = IpcExecutePostReadSourceRules.IsCompatibleWithOperation(
            operationName,
            sourceKind,
            playModeMutation,
            commit,
            persistenceExpected,
            expectedPostState);

        Assert.True(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("edit", "edit", true, "none", false, "deterministic")]
    [InlineData("edit", "edit", false, "context", false, "deterministic")]
    [InlineData("edit", "edit", false, null, true, "deterministic")]
    [InlineData("edit", "edit", false, "invalid", true, "deterministic")]
    [InlineData("edit", "edit", false, "none", false, "unavailable")]
    [InlineData("edit", "edit", true, "context", true, "unavailable")]
    [InlineData("ucli.scene.open", "operation", true, null, false, "unavailable")]
    [InlineData("ucli.scene.open", "operation", false, "none", false, "unavailable")]
    [InlineData("ucli.scene.open", "operation", false, null, false, "deterministic")]
    [InlineData("ucli.project.refresh", "operation", false, null, false, "unavailable")]
    [InlineData("ucli.scene.open", "refresh", false, null, true, "unavailable")]
    [InlineData("ucli.project.refresh", "refresh", true, null, true, "unavailable")]
    [InlineData("ucli.project.refresh", "refresh", false, null, false, "unavailable")]
    [InlineData("ucli.project.refresh", "refresh", false, "none", true, "unavailable")]
    [InlineData("ucli.project.refresh", "refresh", false, null, true, "deterministic")]
    public void IsCompatibleWithOperation_WithInvalidSourceFacts_ReturnsFalse (
        string operationName,
        string sourceKind,
        bool playModeMutation,
        string? commit,
        bool persistenceExpected,
        string expectedPostState)
    {
        var result = IpcExecutePostReadSourceRules.IsCompatibleWithOperation(
            operationName,
            sourceKind,
            playModeMutation,
            commit,
            persistenceExpected,
            expectedPostState);

        Assert.False(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("edit", "deterministic", true)]
    [InlineData("edit", "unavailable", false)]
    [InlineData("operation", "unavailable", false)]
    [InlineData("refresh", "unavailable", false)]
    public void IsDeterministicMutationSource_ReturnsExpectedResult (
        string sourceKind,
        string expectedPostState,
        bool expectedResult)
    {
        var result = IpcExecutePostReadSourceRules.IsDeterministicMutationSource(
            sourceKind,
            expectedPostState);

        Assert.Equal(expectedResult, result);
    }
}
