using System.Reflection;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Text;

public sealed class ContractLiteralCodecTests
{
    private static readonly string?[] NonExactOperationPolicyLiterals =
    [
        null,
        "",
        "safe ",
        "SAFE",
        "unsupported",
    ];

    private static readonly LiteralMatchCase[] LiteralMatchCases =
    [
        new("dangerous", OperationPolicy.Dangerous, ExpectedResult: true),
        new("dangerous", OperationPolicy.Safe, ExpectedResult: false),
        new("DANGEROUS", OperationPolicy.Dangerous, ExpectedResult: false),
        new(null, OperationPolicy.Dangerous, ExpectedResult: false),
    ];

    private static readonly InvalidEnumCase[] InvalidEnumCases =
    [
        new("missing literal", static () => ContractLiteralCodec.GetLiterals<MissingLiteralEnum>()),
        new("empty literal", static () => ContractLiteralCodec.GetLiterals<EmptyLiteralEnum>()),
        new("outer whitespace literal", static () => ContractLiteralCodec.GetLiterals<WhitespaceLiteralEnum>()),
        new("duplicate literal", static () => ContractLiteralCodec.GetLiterals<DuplicateLiteralEnum>()),
        new("duplicate enum value", static () => ContractLiteralCodec.GetLiterals<DuplicateEnumValueEnum>()),
        new("literal and ignore attributes", static () => ContractLiteralCodec.GetLiterals<LiteralAndIgnoreEnum>()),
    ];

    private static readonly Type[] ContractVocabularyEnumTypes =
    [
        typeof(UcliOperationExposure),
        typeof(UcliOperationKind),
        typeof(OperationPolicy),
        typeof(PlanTokenMode),
        typeof(ReadIndexMode),
        typeof(BuildProfileProjectMutationMode),
        typeof(BuildProfileSceneSource),
        typeof(DaemonEditorMode),
        typeof(DaemonSessionOwnerKind),
        typeof(DaemonStartupBlockedProcessPolicy),
        typeof(DaemonStartProgressEvent),
        typeof(DaemonStartProgressPayloadKind),
        typeof(IndexSchemaKind),
        typeof(IndexPropertyType),
        typeof(IpcResponseMode),
        typeof(IpcTransportKind),
        typeof(IpcEditorLifecycleState),
        typeof(IpcCompileState),
        typeof(IpcEditorBlockingReason),
        typeof(IpcScreenshotTarget),
        typeof(IpcScreenshotSizeMode),
        typeof(IpcScreenshotColorSpace),
        typeof(IpcScreenshotPixelFormat),
        typeof(IpcScreenshotRowOrder),
        typeof(ScreenshotArtifactKind),
        typeof(IpcPlayModeState),
        typeof(IpcPlayModeTransition),
        typeof(IpcEditStepContract.ActionKind),
        typeof(SceneTreeSourceStateKind),
        typeof(UcliOperationInputConstraintKind),
        typeof(UcliOperationAssetKind),
        typeof(UcliOperationReferenceTargetKind),
        typeof(UcliOperationSerializedPropertyAccess),
        typeof(UcliOperationTypeKind),
        typeof(UcliOperationPlanMode),
        typeof(UcliOperationSideEffect),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_WhenValueIsMapped_ReturnsCanonicalLiteral ()
    {
        Assert.Equal("safe", ContractLiteralCodec.ToValue(OperationPolicy.Safe));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryToValue_WhenValueIsMapped_ReturnsCanonicalLiteral ()
    {
        var result = ContractLiteralCodec.TryToValue(OperationPolicy.Advanced, out var literal);

        Assert.True(result);
        Assert.Equal("advanced", literal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenLiteralExactlyMatches_ReturnsEnumValue ()
    {
        var result = ContractLiteralCodec.TryParse("dangerous", out OperationPolicy policy);

        Assert.True(result);
        Assert.Equal(OperationPolicy.Dangerous, policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryParse_WhenLiteralDoesNotExactlyMatch_ReturnsFalse ()
    {
        foreach (string? literal in NonExactOperationPolicyLiterals)
        {
            var result = ContractLiteralCodec.TryParse<OperationPolicy>(literal, out var policy);

            Assert.False(result);
            Assert.Equal(default, policy);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Matches_ReturnsWhetherLiteralMapsToExpectedValue ()
    {
        foreach (var testCase in LiteralMatchCases)
        {
            Assert.Equal(
                testCase.ExpectedResult,
                ContractLiteralCodec.Matches(testCase.Literal, testCase.ExpectedValue));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsDefined_WhenLiteralDoesNotExactlyMatch_ReturnsFalse ()
    {
        foreach (string? literal in NonExactOperationPolicyLiterals)
        {
            Assert.False(ContractLiteralCodec.IsDefined<OperationPolicy>(literal));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetLiterals_ReturnsMappedLiteralsInDeclarationOrder ()
    {
        Assert.Equal(
            ["asset", "prefab", "projectSettings", "scene"],
            ContractLiteralCodec.GetLiterals<UcliOperationAssetKind>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetLiterals_WhenEditActionKind_ReturnsCanonicalActionLiterals ()
    {
        Assert.Equal(
            [
                "set",
                "ensureComponent",
                "createObject",
                "createAsset",
                "createPrefab",
                "applyPrefabOverrides",
                "revertPrefabOverrides",
                "delete",
                "reparent",
            ],
            ContractLiteralCodec.GetLiterals<IpcEditStepContract.ActionKind>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ToValue_WhenValueIsUnmapped_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            static () => ContractLiteralCodec.ToValue((OperationPolicy)999));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryToValue_WhenValueIsUnmapped_ReturnsFalse ()
    {
        var result = ContractLiteralCodec.TryToValue(UcliOperationAssetKind.Unspecified, out var literal);

        Assert.False(result);
        Assert.Null(literal);
        Assert.False(ContractLiteralCodec.IsDefined(UcliOperationAssetKind.Unspecified));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetLiterals_WhenEnumLiteralDefinitionIsInvalid_ThrowsInvalidOperationException ()
    {
        foreach (var testCase in InvalidEnumCases)
        {
            var exception = Record.Exception(testCase.GetLiterals);
            Assert.True(
                exception?.GetType() == typeof(InvalidOperationException),
                $"{testCase.Name} must throw {nameof(InvalidOperationException)}.");
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GetLiterals_WhenContractVocabularyEnumIsRegistered_BuildsTable ()
    {
        foreach (Type enumType in ContractVocabularyEnumTypes)
        {
            var literals = InvokeGetLiterals(enumType);

            Assert.True(literals.Count > 0, $"{enumType.FullName} must expose at least one contract literal.");
        }
    }

    private static IReadOnlyList<string> InvokeGetLiterals (Type enumType)
    {
        var method = typeof(ContractLiteralCodec)
            .GetMethod(nameof(ContractLiteralCodec.GetLiterals), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(enumType);

        return (IReadOnlyList<string>)method.Invoke(null, null)!;
    }

    private sealed record LiteralMatchCase (
        string? Literal,
        OperationPolicy ExpectedValue,
        bool ExpectedResult);

    private sealed record InvalidEnumCase (
        string Name,
        Action GetLiterals);

    private enum MissingLiteralEnum
    {
        Value = 0,
    }

    private enum EmptyLiteralEnum
    {
        [UcliContractLiteral("")]
        Value = 0,
    }

    private enum WhitespaceLiteralEnum
    {
        [UcliContractLiteral(" value")]
        Value = 0,
    }

    private enum DuplicateLiteralEnum
    {
        [UcliContractLiteral("value")]
        First = 0,

        [UcliContractLiteral("value")]
        Second = 1,
    }

    private enum DuplicateEnumValueEnum
    {
        [UcliContractLiteral("first")]
        First = 0,

        [UcliContractLiteral("second")]
        Second = 0,
    }

    private enum LiteralAndIgnoreEnum
    {
        [UcliContractLiteral("value")]
        [UcliContractLiteralIgnore]
        Value = 0,
    }
}
