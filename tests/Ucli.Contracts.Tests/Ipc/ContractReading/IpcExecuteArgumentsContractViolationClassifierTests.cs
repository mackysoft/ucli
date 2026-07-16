using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.ContractReading;

public sealed class IpcExecuteArgumentsContractViolationClassifierTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Classify_StepArgsTypeMismatch_RetainsStepContext ()
    {
        var readError = IpcExecuteArgumentsContractReadError.StepArgsContractViolation(
            stepIndex: 2,
            stepId: new IpcExecuteStepId("step-2"),
            propertyReadErrorKind: StepPropertyReadErrorKind.TypeMismatch);

        var violation = IpcExecuteArgumentsContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcExecuteArgumentsContractViolationKind.StepArgsTypeMismatch, violation.Kind);
        Assert.Equal(2, violation.StepIndex);
        Assert.Equal("step-2", violation.StepId!.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_UnknownStepProperty_RetainsUnknownPropertyName ()
    {
        var readError = IpcExecuteArgumentsContractReadError.UnknownStepProperty(
            stepIndex: 1,
            unknownPropertyName: "unknownField");

        var violation = IpcExecuteArgumentsContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcExecuteArgumentsContractViolationKind.UnknownStepProperty, violation.Kind);
        Assert.Equal(1, violation.StepIndex);
        Assert.Equal("unknownField", violation.UnknownPropertyName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_DuplicatedStepId_RetainsDuplicatedValue ()
    {
        var readError = IpcExecuteArgumentsContractReadError.DuplicatedStepIdError(
            stepIndex: 3,
            duplicatedStepId: new IpcExecuteStepId("duplicate-step"));

        var violation = IpcExecuteArgumentsContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcExecuteArgumentsContractViolationKind.DuplicatedStepId, violation.Kind);
        Assert.Equal("duplicate-step", violation.DuplicatedStepId!.Value);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_StepSelectMissing_ReturnsNormalizedKind ()
    {
        var readError = IpcExecuteArgumentsContractReadError.StepSelectContractViolation(
            stepIndex: 0,
            stepId: new IpcExecuteStepId("edit-1"),
            propertyReadErrorKind: StepPropertyReadErrorKind.Missing);

        var violation = IpcExecuteArgumentsContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcExecuteArgumentsContractViolationKind.StepSelectMissing, violation.Kind);
        Assert.Equal("edit-1", violation.StepId!.Value);
        Assert.Equal(0, violation.StepIndex);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_StepCommitTypeMismatch_ReturnsNormalizedKind ()
    {
        var readError = IpcExecuteArgumentsContractReadError.StepCommitContractViolation(
            stepIndex: 1,
            stepId: new IpcExecuteStepId("edit-2"),
            jsonStringReadError: new JsonStringReadError(JsonStringReadErrorKind.TypeMismatch, "commit"));

        var violation = IpcExecuteArgumentsContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcExecuteArgumentsContractViolationKind.StepCommitTypeMismatch, violation.Kind);
        Assert.Equal("edit-2", violation.StepId!.Value);
        Assert.Equal(1, violation.StepIndex);
        Assert.Equal("commit", violation.PropertyPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_StepCommitMissing_ReturnsNormalizedKind ()
    {
        var readError = IpcExecuteArgumentsContractReadError.StepCommitContractViolation(
            stepIndex: 2,
            stepId: new IpcExecuteStepId("edit-3"),
            jsonStringReadError: new JsonStringReadError(JsonStringReadErrorKind.Missing, "commit"));

        var violation = IpcExecuteArgumentsContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcExecuteArgumentsContractViolationKind.StepCommitMissing, violation.Kind);
        Assert.Equal("edit-3", violation.StepId!.Value);
        Assert.Equal(2, violation.StepIndex);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_StepActionMustBeObject_RetainsStepContext ()
    {
        var readError = IpcExecuteArgumentsContractReadError.StepActionMustBeObject(
            stepIndex: 4,
            stepId: new IpcExecuteStepId("edit-4"));

        var violation = IpcExecuteArgumentsContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcExecuteArgumentsContractViolationKind.StepActionMustBeObject, violation.Kind);
        Assert.Equal("edit-4", violation.StepId!.Value);
        Assert.Equal(4, violation.StepIndex);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Classify_StepOnMissing_ReturnsNormalizedKind ()
    {
        var readError = IpcExecuteArgumentsContractReadError.StepOnContractViolation(
            stepIndex: 5,
            stepId: new IpcExecuteStepId("edit-5"),
            propertyReadErrorKind: StepPropertyReadErrorKind.Missing);

        var violation = IpcExecuteArgumentsContractViolationClassifier.Classify(readError);

        Assert.Equal(IpcExecuteArgumentsContractViolationKind.StepOnMissing, violation.Kind);
        Assert.Equal("edit-5", violation.StepId!.Value);
        Assert.Equal(5, violation.StepIndex);
    }
}
