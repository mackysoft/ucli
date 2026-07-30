using System.Security.Cryptography;
using System.Text;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationDescribeContractBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void CreateJudging_WithGeneratedAggregate_DeliversBothProviderProjectionsAndDigestWithoutReprojection ()
    {
        const string operationName = "ucli.test.assets.find";
        var serializerOptions = IpcJsonSerializerOptions.PublicRawOperationContracts;
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            operationName,
            serializerOptions.GetTypeInfo(typeof(AssetsFindArgs)),
            serializerOptions.GetTypeInfo(typeof(AssetsFindResult)));
        var codeContract = new UcliOperationCodeContract
        {
            Language = UcliCodeLanguage.CSharp,
        };
        var verdictContract = new UcliOperationVerdictContract(
            "At least one project asset matches the supplied filters.");

        var describe = UcliOperationDescribeContractBuilder.CreateJudging(
            generationResult,
            "Finds project assets by type, path prefix, or name substring.",
            CreateSafeAssurance(),
            verdictContract,
            codeContract);

        Assert.Equal(generationResult.ArgsContract, describe.ArgsContract);
        Assert.Equal(generationResult.ResultContract, describe.ResultContract);
        Assert.Equal(verdictContract, describe.VerdictContract);
        Assert.Equal(UcliCodeLanguage.CSharp, describe.CodeContract!.Language);

        var operationNameDigest = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(operationName)))
            .ToLowerInvariant();
        Assert.Equal(
            $"ucli.operation/{operationNameDigest}/args",
            describe.ArgsContract!.Value.TypeMetadata
                .TryGetProperty("contractId", out var argsContractId)
                ? argsContractId.GetString()
                : null);
        Assert.Equal(
            $"ucli.operation/{operationNameDigest}/result",
            describe.ResultContract!.Value.TypeMetadata
                .TryGetProperty("contractId", out var resultContractId)
                ? resultContractId.GetString()
                : null);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CreateWithoutVerdict_WhenOperationDeclaresNoResult_DoesNotGenerateOrPublishResultContract ()
    {
        const string operationName = "ucli.test.scene.open";
        var generationResult = UcliOperationJsonContractGenerator.Generate(
            operationName,
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(ScenePathArgs)),
            resultTypeInfo: null);

        var describe = UcliOperationDescribeContractBuilder.CreateWithoutVerdict(
            generationResult,
            "Opens a Unity scene asset in the editor.",
            CreateSafeAssurance(),
            codeContract: null);

        Assert.Equal(generationResult.ArgsContract, describe.ArgsContract);
        Assert.Null(generationResult.ResultContract);
        Assert.Null(describe.ResultContract);
        Assert.Null(describe.VerdictContract);
    }

    private static UcliOperationAssuranceContract CreateSafeAssurance ()
    {
        return new UcliOperationAssuranceContract(
            sideEffects: Array.Empty<UcliOperationSideEffect>(),
            touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
            planMode: UcliOperationPlanMode.ObservesLiveUnity,
            planSemantics: "Validate arguments and observe Unity state without applying mutation.",
            callSemantics: "Read Unity state without applying mutation.",
            touchedContract: "Returns no touched resources.",
            readPostconditionContract: "Does not stale read surfaces by itself.",
            failureSemantics: "Failure means the observation was not fully produced.",
            dangerousNotes: Array.Empty<string>());
    }
}
