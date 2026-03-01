using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Validation;

public sealed class IpcRequestContractReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_AllowsMissingHeadersAndOperations ()
    {
        using var document = JsonDocument.Parse("{}");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.PermissivePreflight,
            requestContract: out var parsedDocument,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcRequestContractReadErrorKind.None, error.Kind);
        Assert.Equal(0, parsedDocument.ProtocolVersion);
        Assert.Null(parsedDocument.RequestId);
        Assert.Null(parsedDocument.Operations);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_StoresNullForNonObjectOperation ()
    {
        using var document = JsonDocument.Parse("""{"ops":[1,{"args":{}}]}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.PermissivePreflight,
            requestContract: out var parsedDocument,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcRequestContractReadErrorKind.None, error.Kind);
        Assert.NotNull(parsedDocument.Operations);
        Assert.Equal(2, parsedDocument.Operations.Count);
        Assert.Null(parsedDocument.Operations[0]);
        Assert.NotNull(parsedDocument.Operations[1]);
        Assert.Null(parsedDocument.Operations[1]!.Id);
        Assert.Null(parsedDocument.Operations[1]!.Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsFormatError_WhenRequestIdIsNotCanonicalGuid ()
    {
        using var document = JsonDocument.Parse("""{"protocolVersion":1,"requestId":"invalid","ops":[]}""");

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
    public void TryRead_StrictExecute_ReturnsDuplicatedOperationIdError_WhenOperationIdIsDuplicated ()
    {
        using var document = JsonDocument.Parse(
            """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","ops":[{"id":"same","op":"ucli.resolve","args":{}},{"id":"same","op":"ucli.resolve","args":{}}]}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.DuplicatedOperationId, error.Kind);
        Assert.Equal(1, error.OperationIndex);
        Assert.Equal("same", error.DuplicatedOperationId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_NormalizesRequestIdAndReadsExpectation ()
    {
        using var document = JsonDocument.Parse(
            """{"protocolVersion":1,"requestId":"9B0E6D1E-3F55-4A6B-8C66-5B9A3A7C9C62","ops":[{"id":"op-1","op":"ucli.resolve","args":{},"expect":{"nonNull":true,"min":1,"max":2}}]}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out var parsedDocument,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcRequestContractReadErrorKind.None, error.Kind);
        Assert.Equal("9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62", parsedDocument.RequestId);
        Assert.NotNull(parsedDocument.Operations);
        var operation = Assert.Single(parsedDocument.Operations);
        Assert.NotNull(operation);
        Assert.Equal("op-1", operation!.Id);
        Assert.Equal("ucli.resolve", operation.Name);
        Assert.True(operation.Expectation.HasValue);
        Assert.Equal(1, operation.Expectation.Value.Min);
        Assert.Equal(2, operation.Expectation.Value.Max);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_StrictExecute_ReturnsUnknownRequestPropertyError_WhenUnknownPropertyExists ()
    {
        using var document = JsonDocument.Parse(
            """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","ops":[],"unknown":true}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.StrictExecute,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.UnknownRequestProperty, error.Kind);
        Assert.Equal("unknown", error.UnknownPropertyName);
    }
}
