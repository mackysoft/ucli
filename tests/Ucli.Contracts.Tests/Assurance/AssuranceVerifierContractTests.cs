using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Assurance;

public sealed class AssuranceVerifierContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AssuranceVerifierKind_UsesClosedCanonicalLiterals ()
    {
        Assert.Equal(
            ["ready", "compile", "build", "postRead", "test", "logs"],
            ContractLiteralCodec.GetLiterals<AssuranceVerifierKind>());
        Assert.False(ContractLiteralCodec.IsDefined(default(AssuranceVerifierKind)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssuranceEffect_ContainsBuildVerifierEffects ()
    {
        Assert.Equal(
            [
                "assetDatabaseRefresh",
                "scriptCompilation",
                "domainReload",
                "unityTestRunner",
                "unityLifecycleRead",
                "unityBuildPipeline",
                "unityBuildReportRead",
                "unityLogWindowRead",
                "ucliArtifactWrite",
                "outputManifestWrite",
                "generationSnapshot",
                "projectMutationAudit",
                "unityExecuteMethod",
            ],
            ContractLiteralCodec.GetLiterals<AssuranceEffect>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssuranceVerifierId_RoundTripsAsJsonString ()
    {
        var verifierId = new AssuranceVerifierId("ready.lifecycle");

        var json = IpcPayloadCodec.SerializeToElement(verifierId);
        var success = IpcPayloadCodec.TryDeserialize<AssuranceVerifierId>(json, out var deserialized, out _);

        Assert.Equal("ready.lifecycle", json.GetString());
        Assert.True(success);
        Assert.Equal(verifierId, deserialized);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" ready.lifecycle")]
    [InlineData("ready.lifecycle ")]
    [Trait("Size", "Small")]
    public void AssuranceVerifierId_WithInvalidValue_RejectsConstructionAndCreation (string value)
    {
        Assert.Throws<ArgumentException>(() => new AssuranceVerifierId(value));
        Assert.False(AssuranceVerifierId.TryCreate(value, out var verifierId));
        Assert.Null(verifierId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssuranceVerifierId_WithNull_RejectsConstructionAndCreation ()
    {
        Assert.Throws<ArgumentNullException>(() => new AssuranceVerifierId(null!));
        Assert.False(AssuranceVerifierId.TryCreate(null, out var verifierId));
        Assert.Null(verifierId);
    }
}
