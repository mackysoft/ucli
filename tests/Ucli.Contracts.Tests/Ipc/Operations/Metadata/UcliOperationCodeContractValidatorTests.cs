using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationCodeContractValidatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractIsValid_ReturnsTrue ()
    {
        var describe = CreateDescribeWithDangerousAssurance();
        describe.CodeContract = UcliOperationDescribeContractValidatorTestData.CreateValidCodeContract();

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractLacksArbitrarySourceExecution_ReturnsFalse ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.CodeContract = UcliOperationDescribeContractValidatorTestData.CreateValidCodeContract();

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract codeContract requires assurance.sideEffects to include 'arbitrarySourceExecution'.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContractAndDerivePolicy_WhenCodeContractHasArbitrarySourceExecution_ReturnsDangerousPolicy ()
    {
        var describe = CreateDescribeWithDangerousAssurance();
        describe.CodeContract = UcliOperationDescribeContractValidatorTestData.CreateValidCodeContract();

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContractAndDerivePolicy(
            describe,
            operationKind: "command",
            ownerName: "Test contract",
            out var derivedPolicy,
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(OperationPolicy.Dangerous, derivedPolicy);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenQueryHasCodeContract_ReturnsFalse ()
    {
        var describe = CreateDescribeWithDangerousAssurance();
        describe.CodeContract = UcliOperationDescribeContractValidatorTestData.CreateValidCodeContract();

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
            describe,
            operationKind: "query",
            operationPolicy: "dangerous",
            ownerName: "Test contract",
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has query assurance metadata with mutation or side-effect risk.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractParameterDescriptionIsMissing_ReturnsFalse ()
    {
        var describe = CreateDescribeWithDangerousAssurance();
        describe.CodeContract = new UcliOperationCodeContract(
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
                    new[]
                    {
                        new UcliCodeApiMemberContract(
                            UcliCodeApiMemberKind.Method,
                            "Log",
                            "Records a log message.",
                            type: null,
                            returnType: "void",
                            parameters:
                            [
                                new UcliCodeApiParameterContract("message", "System.String", string.Empty),
                            ]),
                    }),
            });

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an invalid codeContract method parameter at index 0.", errorMessage);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractEntryPointMatchRuleIsMissing_ReturnsFalse (
        string? matchRule)
    {
        var describe = CreateDescribeWithDangerousAssurance();
        describe.CodeContract = UcliOperationDescribeContractValidatorTestData.CreateValidCodeContract();
        describe.CodeContract.EntryPoint!.MatchRule = matchRule;

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has invalid codeContract metadata.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractLanguageIsUnsupported_ReturnsFalse ()
    {
        var describe = CreateDescribeWithDangerousAssurance();
        describe.CodeContract = UcliOperationDescribeContractValidatorTestData.CreateValidCodeContract();
        describe.CodeContract.Language = (UcliCodeLanguage)int.MaxValue;

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an unsupported codeContract language.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidatePublicRawOpDescribeContract_WhenCodeContractSourceFormIsUnsupported_ReturnsFalse ()
    {
        var describe = CreateDescribeWithDangerousAssurance();
        describe.CodeContract = UcliOperationDescribeContractValidatorTestData.CreateValidCodeContract();
        describe.CodeContract.SourceForms =
        [
            new UcliCodeSourceFormContract((UcliCodeSourceFormKind)int.MaxValue, "Unsupported source form."),
        ];

        var isValid = UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(describe, "Test contract", out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Test contract has an unsupported codeContract source form at index 0.", errorMessage);
    }

    private static UcliOperationDescribeContract CreateDescribeWithDangerousAssurance ()
    {
        var describe = UcliOperationDescribeContractValidatorTestData.CreateValidDescribeContract();
        describe.Assurance = UcliOperationDescribeContractValidatorTestData.CreateAssurance(
            [UcliOperationSideEffect.ArbitrarySourceExecution],
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ObservesLiveUnity,
            ["Arbitrary source execution requires dangerous policy."]);
        return describe;
    }
}
