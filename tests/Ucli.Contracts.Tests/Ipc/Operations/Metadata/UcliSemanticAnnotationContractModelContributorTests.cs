using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliSemanticAnnotationContractModelContributorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Generate_FromActualScenePathArgs_ContributesTypedProductSemantics ()
    {
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;

        var result = UcliOperationJsonContractGenerator.Generate(
            "ucli.test.scene.path",
            serializerOptions.GetTypeInfo(typeof(ScenePathArgs)),
            resultTypeInfo: null);

        var assetExists = Assert.Single(
            result.ArgsContractModel.Contributions,
            static contribution => contribution.Name == "ucli.assetExists");
        Assert.Equal(
            TextVocabulary.GetText(UcliOperationAssetKind.Scene),
            assetExists.Value.GetString());

        var projectRelativePath = Assert.Single(
            result.ArgsContractModel.Contributions,
            static contribution => contribution.Name == "ucli.projectRelativePath");
        Assert.True(projectRelativePath.Value.GetBoolean());
    }
}
