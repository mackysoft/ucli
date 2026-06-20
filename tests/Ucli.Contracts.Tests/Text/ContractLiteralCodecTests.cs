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

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("safe ")]
    [InlineData("SAFE")]
    [InlineData("unsupported")]
    public void TryParse_WhenLiteralDoesNotExactlyMatch_ReturnsFalse (string? literal)
    {
        var result = ContractLiteralCodec.TryParse<OperationPolicy>(literal, out var policy);

        Assert.False(result);
        Assert.Equal(default, policy);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("safe ")]
    [InlineData("SAFE")]
    [InlineData("unsupported")]
    public void IsDefined_WhenLiteralDoesNotExactlyMatch_ReturnsFalse (string? literal)
    {
        Assert.False(ContractLiteralCodec.IsDefined<OperationPolicy>(literal));
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

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidEnumCases))]
    public void GetLiterals_WhenEnumLiteralDefinitionIsInvalid_ThrowsInvalidOperationException (Action getLiterals)
    {
        Assert.Throws<InvalidOperationException>(getLiterals);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(ContractVocabularyEnumTypes))]
    public void GetLiterals_WhenContractVocabularyEnumIsRegistered_BuildsTable (Type enumType)
    {
        Assert.NotEmpty(InvokeGetLiterals(enumType));
    }

    public static IEnumerable<object[]> InvalidEnumCases
    {
        get
        {
            yield return new object[] { (Action)(static () => ContractLiteralCodec.GetLiterals<MissingLiteralEnum>()) };
            yield return new object[] { (Action)(static () => ContractLiteralCodec.GetLiterals<EmptyLiteralEnum>()) };
            yield return new object[] { (Action)(static () => ContractLiteralCodec.GetLiterals<WhitespaceLiteralEnum>()) };
            yield return new object[] { (Action)(static () => ContractLiteralCodec.GetLiterals<DuplicateLiteralEnum>()) };
            yield return new object[] { (Action)(static () => ContractLiteralCodec.GetLiterals<DuplicateEnumValueEnum>()) };
            yield return new object[] { (Action)(static () => ContractLiteralCodec.GetLiterals<LiteralAndIgnoreEnum>()) };
        }
    }

    public static IEnumerable<object[]> ContractVocabularyEnumTypes
    {
        get
        {
            yield return new object[] { typeof(UcliOperationExposure) };
            yield return new object[] { typeof(UcliOperationKind) };
            yield return new object[] { typeof(OperationPolicy) };
            yield return new object[] { typeof(PlanTokenMode) };
            yield return new object[] { typeof(ReadIndexMode) };
            yield return new object[] { typeof(BuildProfileProjectMutationMode) };
            yield return new object[] { typeof(BuildProfileSceneSource) };
            yield return new object[] { typeof(DaemonEditorMode) };
            yield return new object[] { typeof(DaemonSessionOwnerKind) };
            yield return new object[] { typeof(DaemonStartupBlockedProcessPolicy) };
            yield return new object[] { typeof(DaemonStartProgressEvent) };
            yield return new object[] { typeof(DaemonStartProgressPayloadKind) };
            yield return new object[] { typeof(IndexSchemaKind) };
            yield return new object[] { typeof(IndexPropertyType) };
            yield return new object[] { typeof(IpcResponseMode) };
            yield return new object[] { typeof(IpcTransportKind) };
            yield return new object[] { typeof(IpcPlayModeState) };
            yield return new object[] { typeof(IpcPlayModeTransition) };
            yield return new object[] { typeof(IpcEditStepContract.ActionKind) };
            yield return new object[] { typeof(SceneTreeSourceStateKind) };
            yield return new object[] { typeof(UcliOperationInputConstraintKind) };
            yield return new object[] { typeof(UcliOperationAssetKind) };
            yield return new object[] { typeof(UcliOperationReferenceTargetKind) };
            yield return new object[] { typeof(UcliOperationSerializedPropertyAccess) };
            yield return new object[] { typeof(UcliOperationTypeKind) };
            yield return new object[] { typeof(UcliOperationPlanMode) };
            yield return new object[] { typeof(UcliOperationSideEffect) };
        }
    }

    private static IReadOnlyList<string> InvokeGetLiterals (Type enumType)
    {
        var method = typeof(ContractLiteralCodec)
            .GetMethod(nameof(ContractLiteralCodec.GetLiterals), BindingFlags.Public | BindingFlags.Static)!
            .MakeGenericMethod(enumType);

        return (IReadOnlyList<string>)method.Invoke(null, null)!;
    }

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
