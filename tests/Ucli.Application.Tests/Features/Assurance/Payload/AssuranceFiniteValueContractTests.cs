using MackySoft.Ucli.Application.Features.Assurance.Ready;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Payload;

public sealed class AssuranceFiniteValueContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void FiniteValues_DeclareExistingWireLiterals ()
    {
        Assert.Equal(
            ["passed", "failed", "indeterminate", "unverified", "outOfScope"],
            ContractLiteralCodec.GetLiterals<AssuranceClaimStatus>());
        Assert.False(ContractLiteralCodec.IsDefined(default(AssuranceClaimStatus)));
        Assert.Equal(
            ["full", "partial", "none"],
            ContractLiteralCodec.GetLiterals<AssuranceCoverage>());
        Assert.False(ContractLiteralCodec.IsDefined(default(AssuranceCoverage)));
        Assert.Equal(
            ["sessionBound", "probeOnly"],
            ContractLiteralCodec.GetLiterals<ReadyValidityKind>());
        Assert.False(ContractLiteralCodec.IsDefined(default(ReadyValidityKind)));
        Assert.Equal(
            ["available", "failed"],
            ContractLiteralCodec.GetLiterals<ReadyReadIndexArtifactStatus>());
        Assert.False(ContractLiteralCodec.IsDefined(default(ReadyReadIndexArtifactStatus)));
        Assert.Equal(
            ["readIndex.mode", "ops.catalog", "asset-search.lookup", "guid-path.lookup"],
            ContractLiteralCodec.GetLiterals<ReadyReadIndexArtifactName>());
        Assert.False(ContractLiteralCodec.IsDefined(default(ReadyReadIndexArtifactName)));
        Assert.Equal(
            ["unknown", "disabled", "allowStale", "requireFresh"],
            ContractLiteralCodec.GetLiterals<ReadyReadIndexMode>());
        Assert.False(ContractLiteralCodec.IsDefined(default(ReadyReadIndexMode)));
        Assert.Equal(
            ["fresh", "probable", "stale"],
            ContractLiteralCodec.GetLiterals<IndexFreshness>());
        Assert.False(ContractLiteralCodec.IsDefined(default(IndexFreshness)));
        Assert.Equal(
            ["execution", "mutation", "test", "readIndex"],
            ContractLiteralCodec.GetLiterals<ReadyTarget>());
        Assert.False(ContractLiteralCodec.IsDefined(default(ReadyTarget)));
        Assert.Equal(
            ["auto", "daemon", "oneshot"],
            ContractLiteralCodec.GetLiterals<AssuranceRequestedExecutionMode>());
        Assert.False(ContractLiteralCodec.IsDefined(default(AssuranceRequestedExecutionMode)));
        Assert.Equal(
            ["daemon", "oneshot", "notApplicable"],
            ContractLiteralCodec.GetLiterals<AssuranceResolvedExecutionMode>());
        Assert.False(ContractLiteralCodec.IsDefined(default(AssuranceResolvedExecutionMode)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadyFiniteValueOutputs_WithUndefinedValues_ThrowArgumentOutOfRangeException ()
    {
        var testCases = new (Action Construct, string ParameterName)[]
        {
            (static () => new ReadyClaimValidityOutput(default, false), "Kind"),
            (static () => ReadyReadIndexArtifactOutput.Failed(
                default,
                required: true,
                UcliCoreErrorCodes.InvalidArgument,
                "message",
                actionRequired: null), "Name"),
            (static () => ReadyReadIndexArtifactOutput.Available(
                ReadyReadIndexArtifactName.OpsCatalog,
                required: true,
                default,
                Sha256DigestTestFactory.Compute("source-hash"),
                DateTimeOffset.UnixEpoch), "Freshness"),
            (static () => new ReadyReadIndexOutput(default, []), "Mode"),
        };

        Assert.All(
            testCases,
            testCase => Assert.Equal(
                testCase.ParameterName,
                Assert.Throws<ArgumentOutOfRangeException>(testCase.Construct).ParamName));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadyCommandInput_WithUndefinedTarget_ThrowsArgumentOutOfRangeException ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new ReadyCommandInput(
            ProjectPath: null,
            Target: default,
            Mode: null,
            TimeoutMilliseconds: null,
            ReadIndexMode: null,
            IsReadIndexModeSpecified: false,
            FailFast: false));

        Assert.Equal("Target", exception.ParamName);
    }
}
