using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Contracts.Tests.Ipc.Operations.UcliOperationContractValidatorTestContracts;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

public sealed class UcliOperationContractValidatorRequestLocalAliasTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateNoRequestLocalAliases_WhenNestedAliasIsPresent_ReturnsFalse ()
    {
        var args = new ReferenceArgs(
            new GameObjectReferenceArgs(
                alias: new UcliPlanAlias("created"),
                globalObjectId: null,
                prefab: null,
                scene: null,
                hierarchyPath: null));

        var isValid = UcliOperationContractValidator.TryValidateNoRequestLocalAliases(
            args,
            typeof(ReferenceArgs),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args.target.var' cannot use reserved request-local alias property 'var' in public op steps.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateNoRequestLocalAliases_WhenAliasPropertyIsNotPresent_ReturnsTrue ()
    {
        var args = new ReferenceArgs(
            new GameObjectReferenceArgs(
                alias: null,
                globalObjectId: null,
                prefab: null,
                scene: new SceneAssetPath("Assets/Scenes/Main.unity"),
                hierarchyPath: new UnityHierarchyPath("Root")));

        var isValid = UcliOperationContractValidator.TryValidateNoRequestLocalAliases(
            args,
            typeof(ReferenceArgs),
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateNoRequestLocalAliases_WhenAnyJsonValueContainsVarProperty_ReturnsTrue ()
    {
        using var document = JsonDocument.Parse("""{"var":"literal"}""");
        var args = new AnyJsonValueArgs(document.RootElement.Clone());

        var isValid = UcliOperationContractValidator.TryValidateNoRequestLocalAliases(
            args,
            typeof(AnyJsonValueArgs),
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateNoRequestLocalAliasProperties_WhenAliasPropertyIsNull_ReturnsFalse ()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "target": {
                "var": null,
                "scene": "Assets/Scenes/Main.unity",
                "hierarchyPath": "Root"
              }
            }
            """);

        var isValid = UcliOperationContractValidator.TryValidateNoRequestLocalAliasProperties(
            document.RootElement,
            typeof(ReferenceArgs),
            out var errorMessage);

        Assert.False(isValid);
        Assert.Equal("Operation 'args.target.var' cannot use reserved request-local alias property 'var' in public op steps.", errorMessage);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryValidateNoRequestLocalAliasProperties_WhenAnyJsonValueContainsVarProperty_ReturnsTrue ()
    {
        using var document = JsonDocument.Parse("""{"value":{"var":null}}""");

        var isValid = UcliOperationContractValidator.TryValidateNoRequestLocalAliasProperties(
            document.RootElement,
            typeof(AnyJsonValueArgs),
            out var errorMessage);

        Assert.True(isValid, errorMessage);
        Assert.Equal(string.Empty, errorMessage);
    }
}
