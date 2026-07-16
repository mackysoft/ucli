using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcExecuteArgumentsContractReaderPreflightTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_AllowsMissingProtocolVersionAndSteps ()
    {
        using var document = JsonDocument.Parse("{}");

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.PermissivePreflight,
            argumentsContract: out var parsedArguments,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.None, error.Kind);
        Assert.Equal(0, parsedArguments.ProtocolVersion);
        Assert.Null(parsedArguments.Steps);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_StoresNullForNonObjectStep ()
    {
        using var document = JsonDocument.Parse("""{"steps":[1,{"kind":"op"}]}""");

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.PermissivePreflight,
            argumentsContract: out var parsedArguments,
            error: out var error);

        Assert.True(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.None, error.Kind);
        Assert.NotNull(parsedArguments.Steps);
        Assert.Equal(2, parsedArguments.Steps.Count);
        Assert.Null(parsedArguments.Steps[0]);
        Assert.NotNull(parsedArguments.Steps[1]);
        Assert.Equal(IpcExecuteStepKind.Op, parsedArguments.Steps[1]!.Kind);
        Assert.Null(parsedArguments.Steps[1]!.Id);
        Assert.Null(parsedArguments.Steps[1]!.OperationName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_ReturnsProtocolVersionTypeMismatch_WhenProtocolVersionIsNonInteger ()
    {
        using var document = JsonDocument.Parse("""{"protocolVersion":"1"}""");

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.PermissivePreflight,
            argumentsContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.ProtocolVersionTypeMismatch, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryRead_PermissivePreflight_ReturnsUnknownArgumentsProperty_WhenRequestIdExists ()
    {
        using var document = JsonDocument.Parse("""{"requestId":1}""");

        var result = IpcExecuteArgumentsContractReader.TryRead(
            argumentsObject: document.RootElement,
            profile: IpcExecuteArgumentsContractReadProfile.PermissivePreflight,
            argumentsContract: out _,
            error: out var error);

        Assert.False(result);
        Assert.Equal(IpcExecuteArgumentsContractReadErrorKind.UnknownArgumentsProperty, error.Kind);
        Assert.Equal("requestId", error.UnknownPropertyName);
    }
}
