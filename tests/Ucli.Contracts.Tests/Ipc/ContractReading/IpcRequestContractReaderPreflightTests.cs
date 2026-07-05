using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcRequestContractReaderPreflightTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_AllowsMissingHeadersAndSteps ()
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
        Assert.Null(parsedDocument.Steps);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_StoresNullForNonObjectStep ()
    {
        using var document = JsonDocument.Parse("""{"steps":[1,{"kind":"op"}]}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.PermissivePreflight,
            requestContract: out var parsedDocument,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcRequestContractReadErrorKind.None, error.Kind);
        Assert.NotNull(parsedDocument.Steps);
        Assert.Equal(2, parsedDocument.Steps.Count);
        Assert.Null(parsedDocument.Steps[0]);
        Assert.NotNull(parsedDocument.Steps[1]);
        Assert.Equal(IpcRequestStepKind.Op, parsedDocument.Steps[1]!.Kind);
        Assert.Null(parsedDocument.Steps[1]!.Id);
        Assert.Null(parsedDocument.Steps[1]!.OperationName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_ReturnsProtocolVersionTypeMismatch_WhenProtocolVersionIsNonInteger ()
    {
        using var document = JsonDocument.Parse("""{"protocolVersion":"1"}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.PermissivePreflight,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.ProtocolVersionTypeMismatch, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_ReturnsRequestIdTypeMismatch_WhenRequestIdIsNonString ()
    {
        using var document = JsonDocument.Parse("""{"requestId":1}""");

        var result = IpcRequestContractReader.TryRead(
            requestObject: document.RootElement,
            profile: IpcRequestContractReadProfile.PermissivePreflight,
            requestContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcRequestContractReadErrorKind.RequestIdContractViolation, error.Kind);
        Assert.Equal(JsonStringReadErrorKind.TypeMismatch, error.JsonStringReadError.Kind);
    }
}
