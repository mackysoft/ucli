using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationDescribePolicyValidatorTests
{
    private static readonly QueryAssuranceMutationRiskCase[] QueryAssuranceMutationRiskCases =
    [
        new(
            nameof(UcliOperationAssuranceContract.MayDirty),
            assurance => assurance.MayDirty = true),
        new(
            nameof(UcliOperationAssuranceContract.MayPersist),
            assurance => assurance.MayPersist = true),
        new(
            nameof(UcliOperationAssuranceContract.TouchedKinds),
            assurance => assurance.TouchedKinds = [UcliTouchedResourceKindNames.Scene]),
        new(
            nameof(UcliOperationAssuranceContract.SideEffects),
            assurance => assurance.SideEffects = ["sceneContentMutation"]),
    ];

    private static readonly RiskyPolicyWithoutDangerousNotesCase[] RiskyPolicyWithoutDangerousNotesCases =
    [
        new("advanced", ["editorStateChange"]),
        new("dangerous", ["externalProcess"]),
    ];

    private static readonly string[] DerivedRiskySideEffectsWithoutDangerousNotes =
    [
        "editorStateChange",
        "externalProcess",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenAssurancePlanModeCreatesPreviewState_ReturnsFalse ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.PlanMode = "mayCreatePreviewState";
        describe.Assurance.DangerousNotes = ["Preview-state planning is not public raw safe."];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
            describe,
            operationKind: "command",
            operationPolicy: "advanced",
            ownerName: "Test contract",
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract public raw assurance metadata must not use planMode 'mayCreatePreviewState'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateRegisteredOperationDescribeContractAndDerivePolicy_WhenPreviewStateIsAllowedForEditLoweringOnlyExposure_ReturnsAdvancedPolicy ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.PlanMode = "mayCreatePreviewState";
        describe.Assurance.DangerousNotes = ["Preview-state planning is allowed for edit-lowering-only operations."];

        var isValid = UcliOperationDescribeContractValidator.TryValidateRegisteredOperationDescribeContractAndDerivePolicy(
            describe,
            operationKind: "command",
            ownerName: "Test contract",
            exposure: UcliOperationExposure.EditLoweringOnly,
            out var derivedPolicy,
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(OperationPolicy.Advanced, derivedPolicy);
        Assert.Empty(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateRegisteredOperationDescribeContractAndDerivePolicy_WhenPreviewStateUsesUnsupportedExposure_ReturnsFalse ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.PlanMode = "mayCreatePreviewState";
        describe.Assurance.DangerousNotes = ["Preview-state planning requires an edit-lowering-only operation."];

        var isValid = UcliOperationDescribeContractValidator.TryValidateRegisteredOperationDescribeContractAndDerivePolicy(
            describe,
            operationKind: "command",
            ownerName: "Test contract",
            exposure: (UcliOperationExposure)42,
            out var derivedPolicy,
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal(OperationPolicy.Safe, derivedPolicy);
        Assert.Equal("Test contract public raw assurance metadata must not use planMode 'mayCreatePreviewState'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenQueryObservesUnityState_ReturnsTrue ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.SideEffects = ["observesUnityState"];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
            describe,
            operationKind: "query",
            operationPolicy: "safe",
            ownerName: "Test contract",
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Empty(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenQueryAssuranceHasMutationRisk_ReturnsFalse ()
    {
        foreach (var testCase in QueryAssuranceMutationRiskCases)
        {
            var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
            testCase.Apply(describe.Assurance!);

            var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
                describe,
                operationKind: "query",
                operationPolicy: "safe",
                ownerName: "Test contract",
                out var errorMessage);

            Assert.False(isValid, $"{testCase.FieldName} must be rejected for query operations.");
            Assert.Equal("Test contract has query assurance metadata with mutation or side-effect risk.", errorMessage);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenRiskyPolicyHasNoDangerousNotes_ReturnsFalse ()
    {
        foreach (var testCase in RiskyPolicyWithoutDangerousNotesCases)
        {
            var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
            describe.Assurance!.SideEffects = testCase.SideEffects;

            var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
                describe,
                operationKind: "command",
                operationPolicy: testCase.Policy,
                ownerName: "Test contract",
                out var errorMessage);

            Assert.False(isValid);
            Assert.Equal("Test contract must declare dangerousNotes for advanced or dangerous policy.", errorMessage);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenDerivedRiskyPolicyHasNoDangerousNotes_ReturnsFalse ()
    {
        foreach (var sideEffect in DerivedRiskySideEffectsWithoutDangerousNotes)
        {
            var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
            describe.Assurance!.SideEffects = [sideEffect];

            var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

            Assert.False(isValid);
            Assert.Equal("Test contract must declare dangerousNotes for advanced or dangerous policy.", errorMessage);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenDeclaredPolicyDoesNotMatchDerivedPolicy_ReturnsFalse ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.SideEffects = ["editorStateChange"];
        describe.Assurance.DangerousNotes = ["Editor state changes require advanced policy."];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
            describe,
            operationKind: "command",
            operationPolicy: "safe",
            ownerName: "Test contract",
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract policy 'safe' does not match derived policy 'advanced'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContractAndDerivePolicy_WhenAssuranceIsValid_ReturnsDerivedPolicy ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.SideEffects = ["editorStateChange"];
        describe.Assurance.DangerousNotes = ["Editor state changes require advanced policy."];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContractAndDerivePolicy(
            describe,
            operationKind: "command",
            ownerName: "Test contract",
            out var derivedPolicy,
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(OperationPolicy.Advanced, derivedPolicy);
        Assert.Empty(errorMessage);
    }

    private sealed record QueryAssuranceMutationRiskCase (
        string FieldName,
        Action<UcliOperationAssuranceContract> Apply);

    private sealed record RiskyPolicyWithoutDangerousNotesCase (
        string Policy,
        string[] SideEffects);
}
