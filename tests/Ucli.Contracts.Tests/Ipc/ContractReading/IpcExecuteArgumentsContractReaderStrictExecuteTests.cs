using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcExecuteArgumentsContractReaderStrictExecuteTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsDuplicatedStepIdError_WhenStepIdIsDuplicated ()
    {
        using var document = JsonDocument.Parse(
            """
            {"protocolVersion":1,"steps":[{"kind":"op","id":"same","op":"__RESOLVE_OP__","args":{}},{"kind":"op","id":"same","op":"__RESOLVE_OP__","args":{}}]}
            """
                .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.StrictExecute,
            argumentsContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.DuplicatedStepId, error.Kind);
        Assert.Equal(1, error.StepIndex);
        Assert.Equal("same", error.DuplicatedStepId!.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReadsOpStep ()
    {
        using var document = JsonDocument.Parse(
            """
            {"protocolVersion":1,"steps":[{"kind":"op","id":"op-1","op":"__RESOLVE_OP__","args":{}}]}
            """
                .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.StrictExecute,
            argumentsContract: out var parsedArguments,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.None, error.Kind);
        Assert.NotNull(parsedArguments.Steps);
        var step = Assert.Single(parsedArguments.Steps!);
        Assert.NotNull(step);
        Assert.Equal(IpcExecuteStepKind.Op, step!.Kind);
        Assert.Equal("op-1", step.Id!.Value);
        Assert.Equal(UcliPrimitiveOperationNames.Resolve, step.OperationName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("requestId")]
    [InlineData("unknown")]
    public void TryRead_StrictExecute_ReturnsUnknownArgumentsPropertyError_WhenUnknownPropertyExists (string propertyName)
    {
        using var document = JsonDocument.Parse(
            $$"""{"protocolVersion":1,"steps":[],"{{propertyName}}":true}""");

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.StrictExecute,
            argumentsContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.UnknownArgumentsProperty, error.Kind);
        Assert.Equal(propertyName, error.UnknownPropertyName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("""{"kind":"op","id":"op-1","op":"ucli.resolve","args":{},"commit":"context"}""", "commit")]
    [InlineData("""{"kind":"edit","id":"edit-1","op":"ucli.resolve","on":{"scene":"Assets/Scenes/Main.unity"},"select":{"gameObject":"Root","cardinality":"one"},"actions":[{"kind":"delete"}],"commit":"context"}""", "op")]
    [InlineData("""{"kind":"edit","id":"edit-1","args":{},"on":{"scene":"Assets/Scenes/Main.unity"},"select":{"gameObject":"Root","cardinality":"one"},"actions":[{"kind":"delete"}],"commit":"context"}""", "args")]
    public void TryRead_StrictExecute_ReturnsUnknownStepPropertyError_WhenStepKindDisallowsProperty (
        string stepJson,
        string expectedUnknownProperty)
    {
        using var document = JsonDocument.Parse(
            """{"protocolVersion":1,"steps":[__STEP__]}"""
                .Replace("__STEP__", stepJson, StringComparison.Ordinal));

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.StrictExecute,
            argumentsContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.UnknownStepProperty, error.Kind);
        Assert.Equal(0, error.StepIndex);
        Assert.Equal(expectedUnknownProperty, error.UnknownPropertyName);
    }
}
