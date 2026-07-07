using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationAssuranceContractValidatorTests
{
    private static readonly AssuranceSemanticFieldFailureCase[] AssuranceSemanticFieldFailureCases =
    [
        new(
            nameof(UcliOperationAssuranceContract.PlanSemantics),
            assurance => assurance.PlanSemantics = null),
        new(
            nameof(UcliOperationAssuranceContract.CallSemantics),
            assurance => assurance.CallSemantics = string.Empty),
        new(
            nameof(UcliOperationAssuranceContract.TouchedContract),
            assurance => assurance.TouchedContract = " "),
        new(
            nameof(UcliOperationAssuranceContract.ReadPostconditionContract),
            assurance => assurance.ReadPostconditionContract = null),
        new(
            nameof(UcliOperationAssuranceContract.FailureSemantics),
            assurance => assurance.FailureSemantics = string.Empty),
    ];

    private static readonly AssuranceProjectionAndConstraintFailureCase[] AssuranceProjectionAndConstraintFailureCases =
    [
        new(
            SideEffect: "assetContentMutation",
            MayDirty: false,
            MayPersist: false,
            TouchedKinds: [UcliTouchedResourceKindNames.Asset],
            ExpectedErrorMessage: "Test contract assurance.mayDirty does not match derived projection 'true'."),
        new(
            SideEffect: "assetContentMutation",
            MayDirty: true,
            MayPersist: false,
            TouchedKinds: [],
            ExpectedErrorMessage: "Test contract side effect 'assetContentMutation' requires assurance.touchedKinds to include 'asset'."),
        new(
            SideEffect: "assetSave",
            MayDirty: false,
            MayPersist: false,
            TouchedKinds: [UcliTouchedResourceKindNames.Asset],
            ExpectedErrorMessage: "Test contract assurance.mayPersist does not match derived projection 'true'."),
        new(
            SideEffect: "assetSave",
            MayDirty: false,
            MayPersist: true,
            TouchedKinds: [],
            ExpectedErrorMessage: "Test contract assurance.mayPersist requires assurance.touchedKinds to be non-empty."),
        new(
            SideEffect: "filesystemWrite",
            MayDirty: false,
            MayPersist: false,
            TouchedKinds: [],
            ExpectedErrorMessage: "Test contract assurance.mayPersist does not match derived projection 'true'."),
        new(
            SideEffect: "filesystemWrite",
            MayDirty: false,
            MayPersist: true,
            TouchedKinds: [],
            ExpectedErrorMessage: "Test contract assurance.mayPersist requires assurance.touchedKinds to be non-empty."),
        new(
            SideEffect: "opensSceneInEditor",
            MayDirty: false,
            MayPersist: false,
            TouchedKinds: [],
            ExpectedErrorMessage: "Test contract side effect 'opensSceneInEditor' requires assurance.touchedKinds to include 'scene'."),
        new(
            SideEffect: "projectSave",
            MayDirty: false,
            MayPersist: true,
            TouchedKinds:
            [
                UcliTouchedResourceKindNames.Scene,
                UcliTouchedResourceKindNames.Prefab,
                UcliTouchedResourceKindNames.Asset,
            ],
            ExpectedErrorMessage: "Test contract side effect 'projectSave' requires assurance.touchedKinds to include 'projectSettings'."),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenAssuranceSemanticFieldIsMissing_ReturnsFalse ()
    {
        foreach (var testCase in AssuranceSemanticFieldFailureCases)
        {
            var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
            testCase.Invalidate(describe.Assurance!);

            var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

            Assert.False(isValid, $"{testCase.FieldName} must be rejected when it is missing or blank.");
            Assert.Equal("Test contract has invalid assurance metadata.", errorMessage);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenAssuranceSideEffectIsUnsupported_ReturnsFalse ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.SideEffects = ["not-supported"];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an unsupported side effect 'not-supported'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenAssuranceTouchedKindIsUnsupported_ReturnsFalse ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.TouchedKinds = ["not-supported"];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an unsupported touched kind 'not-supported'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenAssuranceProjectionOrConstraintIsInvalid_ReturnsFalse ()
    {
        foreach (var testCase in AssuranceProjectionAndConstraintFailureCases)
        {
            var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
            describe.Assurance!.SideEffects = [testCase.SideEffect];
            describe.Assurance.MayDirty = testCase.MayDirty;
            describe.Assurance.MayPersist = testCase.MayPersist;
            describe.Assurance.TouchedKinds = testCase.TouchedKinds;

            var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

            Assert.False(isValid);
            Assert.Equal(testCase.ExpectedErrorMessage, errorMessage);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenRuntimeStateMutationHasNoTouchedKinds_ReturnsTrue ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.SideEffects = ["runtimeStateMutation"];
        describe.Assurance.MayDirty = true;
        describe.Assurance.MayPersist = false;
        describe.Assurance.TouchedKinds = [];
        describe.Assurance.DangerousNotes = ["Runtime state mutation changes Play Mode state without persisting project resources."];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.True(isValid);
        Assert.Empty(errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenAssurancePlanModeIsUnsupported_ReturnsFalse ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.PlanMode = "not-supported";

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has invalid assurance metadata.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenDangerousNotesIsNull_ReturnsFalse ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.DangerousNotes = null;

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has invalid assurance metadata.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenDangerousNoteIsEmpty_ReturnsFalse ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance!.DangerousNotes = [" "];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an invalid dangerous note.", errorMessage);
    }

    private sealed record AssuranceSemanticFieldFailureCase (
        string FieldName,
        Action<UcliOperationAssuranceContract> Invalidate);

    private sealed record AssuranceProjectionAndConstraintFailureCase (
        string SideEffect,
        bool MayDirty,
        bool MayPersist,
        string[] TouchedKinds,
        string ExpectedErrorMessage);
}
