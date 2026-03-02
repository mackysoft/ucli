using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Validation;

public sealed class IpcRequestContractViolationClassifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Classify_RequestIdMissing_ReturnsNormalizedKind ()
    {
        var readError = IpcRequestContractReadError.RequestIdContractViolation(
            new JsonStringReadError(JsonStringReadErrorKind.Missing, "requestId"));

        var violation = IpcRequestContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcRequestContractViolationKind.RequestIdMissing, violation.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_OperationArgsTypeMismatch_RetainsOperationContext ()
    {
        var readError = IpcRequestContractReadError.OperationArgsContractViolation(
            operationIndex: 2,
            operationId: "op-2",
            operationObjectReadErrorKind: OperationObjectReadErrorKind.TypeMismatch);

        var violation = IpcRequestContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcRequestContractViolationKind.OperationArgsTypeMismatch, violation.Kind);
        Assert.Equal(2, violation.OperationIndex);
        Assert.Equal("op-2", violation.OperationId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_ExpectationUnknownProperty_RetainsUnknownPropertyName ()
    {
        var readError = IpcRequestContractReadError.OperationExpectationContractViolation(
            operationIndex: 1,
            operationId: "op-1",
            expectationReadError: new ExpectationConstraintReadError(
                Kind: ExpectationConstraintReadErrorKind.ExpectationContainsUnknownProperty,
                PropertyPath: "expect",
                UnknownPropertyName: "unknownField"));

        var violation = IpcRequestContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcRequestContractViolationKind.ExpectationContainsUnknownProperty, violation.Kind);
        Assert.Equal("unknownField", violation.UnknownPropertyName);
        Assert.Equal("expect", violation.PropertyPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_DuplicatedOperationId_RetainsDuplicatedValue ()
    {
        var readError = IpcRequestContractReadError.DuplicatedOperationIdError(
            operationIndex: 3,
            duplicatedOperationId: "duplicate-op");

        var violation = IpcRequestContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcRequestContractViolationKind.DuplicatedOperationId, violation.Kind);
        Assert.Equal("duplicate-op", violation.DuplicatedOperationId);
    }
}