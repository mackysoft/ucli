using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationPolicyDeriverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryDerive_WhenNoRiskFacts_ReturnsSafe ()
    {
        var assurance = CreateAssurance(Array.Empty<string>());

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Safe, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDerive_WhenMultipleSideEffects_ReturnsStrictestPolicy ()
    {
        var assurance = CreateAssurance(
        [
            UcliOperationSideEffectValues.ObservesUnityState,
            UcliOperationSideEffectValues.SceneSave,
        ],
        touchedKinds: [IpcExecuteTouchedResourceKindNames.Scene]);

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Advanced, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDerive_WhenPlanMayCreatePreviewState_ReturnsAdvanced ()
    {
        var assurance = CreateAssurance(
            Array.Empty<string>(),
            planMode: UcliOperationPlanModeValues.MayCreatePreviewState);

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Advanced, policy);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliOperationSideEffectValues.ExternalProcess)]
    [InlineData(UcliOperationSideEffectValues.FilesystemWrite)]
    [InlineData(UcliOperationSideEffectValues.ArbitrarySourceExecution)]
    [InlineData(UcliOperationSideEffectValues.DestructiveScope)]
    public void TryDerive_WhenDangerousSideEffectIsDeclared_ReturnsDangerous (string sideEffect)
    {
        var assurance = CreateAssurance([sideEffect]);

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Dangerous, policy);
        Assert.True(UcliOperationSideEffectDescriptors.IsDangerousDerivationSource(sideEffect));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDerive_WhenSideEffectIsUnsupported_ReturnsFalse ()
    {
        var assurance = CreateAssurance(["not-supported"]);

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, out _);

        Assert.False(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDerive_WhenArbitrarySourceExecutionIsDeclared_ReturnsDangerous ()
    {
        var assurance = CreateAssurance([UcliOperationSideEffectValues.ArbitrarySourceExecution]);

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Dangerous, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDerive_WhenStoredProjectionIsCorrupted_UsesDescriptorProjection ()
    {
        var assurance = CreateAssurance([UcliOperationSideEffectValues.ObservesUnityState]);
        assurance.MayPersist = true;

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Safe, policy);
    }

    private static UcliOperationAssuranceContract CreateAssurance (
        IReadOnlyList<string> sideEffects,
        IReadOnlyList<string>? touchedKinds = null,
        string? planMode = UcliOperationPlanModeValues.ValidationOnly)
    {
        return new UcliOperationAssuranceContract(
            sideEffects,
            touchedKinds ?? Array.Empty<string>(),
            planMode,
            planSemantics: "Validate arguments without applying mutation.",
            callSemantics: "Execute the operation contract.",
            touchedContract: "Returns no touched resources.",
            readPostconditionContract: "Does not stale read surfaces by itself.",
            failureSemantics: "Failure means the operation was not completed.",
            dangerousNotes: Array.Empty<string>());
    }
}
