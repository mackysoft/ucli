using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class UcliRequestLocalAliasContractPolicyTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(typeof(AssetReferenceArgs))]
    [InlineData(typeof(ComponentReferenceArgs))]
    [InlineData(typeof(GameObjectReferenceArgs))]
    [InlineData(typeof(SceneGameObjectReferenceArgs))]
    public void IsBuiltInReferenceContractType_WhenReferenceContractIsBuiltIn_ReturnsTrue (Type referenceType)
    {
        Assert.True(UcliRequestLocalAliasContractPolicy.IsBuiltInReferenceContractType(referenceType));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRequestLocalAliasValueType_WhenTypeIsUcliPlanAlias_ReturnsTrue ()
    {
        Assert.True(UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasValueType(typeof(UcliPlanAlias)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsRequestLocalAliasValueType_WhenTypeIsAnotherStringValue_ReturnsFalse ()
    {
        Assert.False(UcliRequestLocalAliasContractPolicy.IsRequestLocalAliasValueType(typeof(OtherStringValue)));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(typeof(AssetReferenceArgs))]
    [InlineData(typeof(ComponentReferenceArgs))]
    [InlineData(typeof(GameObjectReferenceArgs))]
    [InlineData(typeof(SceneGameObjectReferenceArgs))]
    public void IsInternalRequestLocalAliasBranchProperty_WhenReferenceContractIsBuiltIn_ReturnsTrue (Type referenceType)
    {
        var aliasProperty = referenceType.GetProperty(nameof(GameObjectReferenceArgs.Alias));

        Assert.NotNull(aliasProperty);
        Assert.True(UcliRequestLocalAliasContractPolicy.IsInternalRequestLocalAliasBranchProperty(aliasProperty));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsInternalRequestLocalAliasBranchProperty_WhenReferenceLikeContractIsCustom_ReturnsFalse ()
    {
        var aliasProperty = typeof(CustomReferenceLikeContract).GetProperty(nameof(CustomReferenceLikeContract.Alias));

        Assert.NotNull(aliasProperty);
        Assert.False(UcliRequestLocalAliasContractPolicy.IsInternalRequestLocalAliasBranchProperty(aliasProperty));
    }

    private sealed class OtherStringValue : UcliStringValue
    {
        public OtherStringValue (string value)
            : base(value)
        {
        }
    }

    private sealed record CustomReferenceLikeContract (
        [property: UcliDescription("Request-local alias.")]
        [property: JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
        UcliPlanAlias? Alias);
}
