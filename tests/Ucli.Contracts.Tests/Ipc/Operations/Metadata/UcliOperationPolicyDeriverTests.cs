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
            "observesUnityState",
            "sceneSave",
        ],
        touchedKinds: [UcliTouchedResourceKindNames.Scene]);

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
            planMode: "mayCreatePreviewState");

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Advanced, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDerive_WhenCodeContractExists_ReturnsDangerous ()
    {
        var assurance = CreateAssurance(Array.Empty<string>());

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, CreateValidCodeContract(), out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Dangerous, policy);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("externalProcess")]
    [InlineData("filesystemWrite")]
    [InlineData("arbitrarySourceExecution")]
    [InlineData("destructiveScope")]
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
        var assurance = CreateAssurance(["arbitrarySourceExecution"]);

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Dangerous, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryDerive_WhenStoredProjectionIsCorrupted_UsesDescriptorProjection ()
    {
        var assurance = CreateAssurance(["observesUnityState"]);
        assurance.MayPersist = true;

        var result = UcliOperationPolicyDeriver.TryDerive(assurance, out var policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Safe, policy);
    }

    private static UcliOperationAssuranceContract CreateAssurance (
        IReadOnlyList<string> sideEffects,
        IReadOnlyList<string>? touchedKinds = null,
        string? planMode = "validationOnly")
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

    private static UcliOperationCodeContract CreateValidCodeContract ()
    {
        return new UcliOperationCodeContract(
            "csharp",
            new UcliCodeEntryPointContract(
                "public static object? Run(SampleContext context)",
                "Compiled source must contain exactly one matching Run method.",
                requiredStatic: true,
                new[] { "SampleContext" },
                "JSON-serializable value."),
            new[]
            {
                new UcliCodeSourceFormContract(CsEvalSourceKindValues.CompilationUnit, "Complete C# compilation unit."),
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
