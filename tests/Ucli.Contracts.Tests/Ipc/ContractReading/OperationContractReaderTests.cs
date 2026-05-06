using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class OperationContractReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOperationArgs_ReturnsMissingError_WhenArgsIsAbsent ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            id = "op-1",
            op = UcliPrimitiveOperationNames.SceneOpen,
        });

        var result = OperationContractReader.TryReadOperationArgs(operationElement, out _, out var errorKind);

        Assert.False(result);
        Assert.Equal(OperationObjectReadErrorKind.Missing, errorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOperationArgs_ReturnsTypeMismatch_WhenArgsIsNotObject ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            args = new[] { 1, 2, 3 },
        });

        var result = OperationContractReader.TryReadOperationArgs(operationElement, out _, out var errorKind);

        Assert.False(result);
        Assert.Equal(OperationObjectReadErrorKind.TypeMismatch, errorKind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void FindUnknownOperationProperty_FlagsAliasPropertyAsUnknownInCoreOperationContract ()
    {
        using var parsed = JsonDocument.Parse("""{"id":"op-1","op":"ucli.scene.open","args":{},"as":"alias"}""");

        var unknownPropertyName = OperationContractReader.FindUnknownOperationProperty(parsed.RootElement);

        Assert.Equal("as", unknownPropertyName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOperationName_AllowsMissingOrNonStringOperationName_WhenPolicyIsPermissivePreflight ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            op = 100,
        });

        var result = OperationContractReader.TryReadOperationName(
            operationElement,
            OperationContractReadPolicy.PermissivePreflight,
            out var operationName,
            out var error);

        Assert.True(result);
        Assert.Null(operationName);
        JsonStringReadErrorAssert.Equal(error, JsonStringReadErrorKind.None, string.Empty);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOperationId_RejectsEmptyOperationId_WhenPolicyIsStrictExecute ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            id = "",
        });

        var result = OperationContractReader.TryReadOperationId(
            operationElement,
            OperationContractReadPolicy.StrictExecute,
            out _,
            out var error);

        Assert.False(result);
        JsonStringReadErrorAssert.Equal(error, JsonStringReadErrorKind.EmptyOrWhitespace, "id");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryReadOperationName_ReturnsMissingError_WhenOperationNameIsMissingInStrictMode ()
    {
        var operationElement = JsonSerializer.SerializeToElement(new
        {
            id = "op-1",
        });

        var result = OperationContractReader.TryReadOperationName(
            operationElement,
            OperationContractReadPolicy.StrictExecute,
            out _,
            out var error);

        Assert.False(result);
        JsonStringReadErrorAssert.Equal(error, JsonStringReadErrorKind.Missing, "op");
    }
}
