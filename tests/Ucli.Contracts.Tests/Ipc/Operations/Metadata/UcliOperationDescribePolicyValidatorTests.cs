using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationDescribePolicyValidatorTests
{
    private static readonly QueryAssuranceMutationRiskCase[] QueryAssuranceMutationRiskCases =
    [
        new(
            "derived mayDirty",
            [UcliOperationSideEffect.RuntimeStateMutation],
            Array.Empty<UcliTouchedResourceKind>()),
        new(
            "derived mayPersist",
            [UcliOperationSideEffect.SceneSave],
            [UcliTouchedResourceKind.Scene]),
        new(
            "touched kinds",
            Array.Empty<UcliOperationSideEffect>(),
            [UcliTouchedResourceKind.Scene]),
        new(
            "non-query side effect",
            [UcliOperationSideEffect.EditorStateChange],
            Array.Empty<UcliTouchedResourceKind>()),
    ];

    private static readonly RiskyPolicyWithoutDangerousNotesCase[] RiskyPolicyWithoutDangerousNotesCases =
    [
        new("advanced", UcliOperationSideEffect.EditorStateChange),
        new("dangerous", UcliOperationSideEffect.ExternalProcess),
    ];

    private static readonly UcliOperationSideEffect[] DerivedRiskySideEffectsWithoutDangerousNotes =
    [
        UcliOperationSideEffect.EditorStateChange,
        UcliOperationSideEffect.ExternalProcess,
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenAssurancePlanModeCreatesPreviewState_ReturnsFalse ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance = UcliOperationDescribeContractValidatorTestData.CreateAssurance(
            Array.Empty<UcliOperationSideEffect>(),
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.MayCreatePreviewState,
            ["Preview-state planning is not public raw safe."]);

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
        describe.Assurance = UcliOperationDescribeContractValidatorTestData.CreateAssurance(
            Array.Empty<UcliOperationSideEffect>(),
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.MayCreatePreviewState,
            ["Preview-state planning is allowed for edit-lowering-only operations."]);

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
        describe.Assurance = UcliOperationDescribeContractValidatorTestData.CreateAssurance(
            Array.Empty<UcliOperationSideEffect>(),
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.MayCreatePreviewState,
            ["Preview-state planning requires an edit-lowering-only operation."]);

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
        describe.Assurance = UcliOperationDescribeContractValidatorTestData.CreateAssurance(
            [UcliOperationSideEffect.ObservesUnityState],
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ObservesLiveUnity,
            Array.Empty<string>());

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
            describe.Assurance = UcliOperationDescribeContractValidatorTestData.CreateAssurance(
                testCase.SideEffects,
                testCase.TouchedKinds,
                UcliOperationPlanMode.ObservesLiveUnity,
                ["The operation has non-query effects."]);

            var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
                describe,
                operationKind: "query",
                operationPolicy: "safe",
                ownerName: "Test contract",
                out var errorMessage);

            Assert.False(isValid, $"{testCase.Name} must be rejected for query operations.");
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
            describe.Assurance = UcliOperationDescribeContractValidatorTestData.CreateAssurance(
                [testCase.SideEffect],
                Array.Empty<UcliTouchedResourceKind>(),
                UcliOperationPlanMode.ObservesLiveUnity,
                Array.Empty<string>());

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
            describe.Assurance = UcliOperationDescribeContractValidatorTestData.CreateAssurance(
                [sideEffect],
                Array.Empty<UcliTouchedResourceKind>(),
                UcliOperationPlanMode.ObservesLiveUnity,
                Array.Empty<string>());

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
        describe.Assurance = UcliOperationDescribeContractValidatorTestData.CreateAssurance(
            [UcliOperationSideEffect.EditorStateChange],
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ObservesLiveUnity,
            ["Editor state changes require advanced policy."]);

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
        describe.Assurance = UcliOperationDescribeContractValidatorTestData.CreateAssurance(
            [UcliOperationSideEffect.EditorStateChange],
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ObservesLiveUnity,
            ["Editor state changes require advanced policy."]);

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
        string Name,
        UcliOperationSideEffect[] SideEffects,
        UcliTouchedResourceKind[] TouchedKinds);

    private sealed record RiskyPolicyWithoutDangerousNotesCase (
        string Policy,
        UcliOperationSideEffect SideEffect);
}
