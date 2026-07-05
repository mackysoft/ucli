using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcRequestContractReaderStrictExecuteTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsFormatError_WhenRequestIdIsNotCanonicalGuid ()
    {
        using var document = JsonDocument.Parse("""{"protocolVersion":1,"requestId":"invalid","steps":[]}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.RequestIdFormatMismatch, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsDuplicatedStepIdError_WhenStepIdIsDuplicated ()
    {
        using var document = JsonDocument.Parse(
            """
            {"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[{"kind":"op","id":"same","op":"__RESOLVE_OP__","args":{}},{"kind":"op","id":"same","op":"__RESOLVE_OP__","args":{}}]}
            """
                .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.DuplicatedStepId, error.Kind);
        Assert.Equal(1, error.StepIndex);
        Assert.Equal("same", error.DuplicatedStepId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_NormalizesRequestIdAndReadsOpStep ()
    {
        using var document = JsonDocument.Parse(
            """
            {"protocolVersion":1,"requestId":"9B0E6D1E-3F55-4A6B-8C66-5B9A3A7C9C62","steps":[{"kind":"op","id":"op-1","op":"__RESOLVE_OP__","args":{}}]}
            """
                .Replace("__RESOLVE_OP__", UcliPrimitiveOperationNames.Resolve, StringComparison.Ordinal));

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out var parsedDocument,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcRequestContractReadErrorKind.None, error.Kind);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", parsedDocument.RequestId);
        Assert.NotNull(parsedDocument.Steps);
        var step = Assert.Single(parsedDocument.Steps!);
        Assert.NotNull(step);
        Assert.Equal(IpcRequestStepKind.Op, step!.Kind);
        Assert.Equal("op-1", step.Id);
        Assert.Equal(UcliPrimitiveOperationNames.Resolve, step.OperationName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsUnknownRequestPropertyError_WhenUnknownPropertyExists ()
    {
        using var document = JsonDocument.Parse(
            """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[],"unknown":true}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.UnknownRequestProperty, error.Kind);
        Assert.Equal("unknown", error.UnknownPropertyName);
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
            """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[__STEP__]}"""
                .Replace("__STEP__", stepJson, StringComparison.Ordinal));

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.UnknownStepProperty, error.Kind);
        Assert.Equal(0, error.StepIndex);
        Assert.Equal(expectedUnknownProperty, error.UnknownPropertyName);
    }
}
