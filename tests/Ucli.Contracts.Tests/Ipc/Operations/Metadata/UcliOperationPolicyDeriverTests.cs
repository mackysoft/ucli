using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationPolicyDeriverTests
{
    private static readonly UcliOperationSideEffect[] DangerousSideEffects =
    [
        UcliOperationSideEffect.ExternalProcess,
        UcliOperationSideEffect.FilesystemWrite,
        UcliOperationSideEffect.ArbitrarySourceExecution,
        UcliOperationSideEffect.DestructiveScope,
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void Derive_WhenNoRiskFacts_ReturnsSafe ()
    {
        var assurance = CreateAssurance(
            Array.Empty<UcliOperationSideEffect>(),
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ValidationOnly);

        var policy = UcliOperationPolicyDeriver.Derive(assurance, codeContract: null);

        Assert.Equal(OperationPolicy.Safe, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Derive_WhenMultipleSideEffects_ReturnsStrictestPolicy ()
    {
        var assurance = CreateAssurance(
            [
                UcliOperationSideEffect.ObservesUnityState,
                UcliOperationSideEffect.SceneSave,
            ],
            [UcliTouchedResourceKind.Scene],
            UcliOperationPlanMode.ValidationOnly);

        var policy = UcliOperationPolicyDeriver.Derive(assurance, codeContract: null);

        Assert.Equal(OperationPolicy.Advanced, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Derive_WhenRuntimeStateMutationIsDeclared_ReturnsAdvanced ()
    {
        var assurance = CreateAssurance(
            [UcliOperationSideEffect.RuntimeStateMutation],
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ValidationOnly);

        var policy = UcliOperationPolicyDeriver.Derive(assurance, codeContract: null);

        Assert.Equal(OperationPolicy.Advanced, policy);
        Assert.True(assurance.MayDirty);
        Assert.False(assurance.MayPersist);
        Assert.Empty(assurance.TouchedKinds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Derive_WhenPlanMayCreatePreviewState_ReturnsAdvanced ()
    {
        var assurance = CreateAssurance(
            Array.Empty<UcliOperationSideEffect>(),
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.MayCreatePreviewState);

        var policy = UcliOperationPolicyDeriver.Derive(assurance, codeContract: null);

        Assert.Equal(OperationPolicy.Advanced, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Derive_WhenCodeContractExists_ReturnsDangerous ()
    {
        var assurance = CreateAssurance(
            Array.Empty<UcliOperationSideEffect>(),
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ValidationOnly);

        var policy = UcliOperationPolicyDeriver.Derive(assurance, CreateValidCodeContract());

        Assert.Equal(OperationPolicy.Dangerous, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Derive_WhenDangerousSideEffectIsDeclared_ReturnsDangerous ()
    {
        foreach (var sideEffect in DangerousSideEffects)
        {
            IReadOnlyList<UcliTouchedResourceKind> touchedKinds = sideEffect == UcliOperationSideEffect.FilesystemWrite
                ? [UcliTouchedResourceKind.Asset]
                : Array.Empty<UcliTouchedResourceKind>();
            var assurance = CreateAssurance(
                [sideEffect],
                touchedKinds,
                UcliOperationPlanMode.ValidationOnly);

            var policy = UcliOperationPolicyDeriver.Derive(assurance, codeContract: null);

            Assert.Equal(OperationPolicy.Dangerous, policy);
            Assert.Equal(
                OperationPolicy.Dangerous,
                UcliOperationSideEffectDescriptors.GetDescriptor(sideEffect).MinimumPolicy);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Derive_WhenAssuranceIsNull_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => UcliOperationPolicyDeriver.Derive(null!, codeContract: null));

        Assert.Equal("assurance", exception.ParamName);
    }

    private static UcliOperationAssuranceContract CreateAssurance (
        IReadOnlyList<UcliOperationSideEffect> sideEffects,
        IReadOnlyList<UcliTouchedResourceKind> touchedKinds,
        UcliOperationPlanMode planMode)
    {
        return UcliOperationDescribeContractValidatorTestData.CreateAssurance(
            sideEffects,
            touchedKinds,
            planMode,
            Array.Empty<string>());
    }

    private static UcliOperationCodeContract CreateValidCodeContract ()
    {
        return new UcliOperationCodeContract(
            UcliCodeLanguage.CSharp,
            new UcliCodeEntryPointContract(
                "public static object? Run(SampleContext context)",
                "Compiled source must contain exactly one matching Run method.",
                requiredStatic: true,
                new[] { "SampleContext" },
                "JSON-serializable value."),
            new[]
            {
                new UcliCodeSourceFormContract(UcliCodeSourceFormKind.CompilationUnit, "Complete C# compilation unit."),
            },
            new[]
            {
                new UcliCodeApiTypeContract(
                    "SampleContext",
                    "SampleContext",
                    "Sample context.",
                    Array.Empty<UcliCodeApiMemberContract>()),
            });
    }
}
