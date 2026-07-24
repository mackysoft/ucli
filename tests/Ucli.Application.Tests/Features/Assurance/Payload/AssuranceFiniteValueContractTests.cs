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
            TextVocabulary.GetTexts<AssuranceClaimStatus>());
        Assert.False(TextVocabulary.IsDefined(default(AssuranceClaimStatus)));
        Assert.Equal(
            ["full", "partial", "none"],
            TextVocabulary.GetTexts<AssuranceCoverage>());
        Assert.False(TextVocabulary.IsDefined(default(AssuranceCoverage)));
        Assert.Equal(
            ["sessionBound", "probeOnly"],
            TextVocabulary.GetTexts<ReadyValidityKind>());
        Assert.False(TextVocabulary.IsDefined(default(ReadyValidityKind)));
        Assert.Equal(
            ["available", "failed"],
            TextVocabulary.GetTexts<ReadyReadIndexArtifactStatus>());
        Assert.False(TextVocabulary.IsDefined(default(ReadyReadIndexArtifactStatus)));
        Assert.Equal(
            ["readIndex.mode", "ops.catalog", "asset-search.lookup", "guid-path.lookup"],
            TextVocabulary.GetTexts<ReadyReadIndexArtifactName>());
        Assert.False(TextVocabulary.IsDefined(default(ReadyReadIndexArtifactName)));
        Assert.Equal(
            ["unknown", "disabled", "allowStale", "requireFresh"],
            TextVocabulary.GetTexts<ReadyReadIndexMode>());
        Assert.False(TextVocabulary.IsDefined(default(ReadyReadIndexMode)));
        Assert.Equal(
            ["fresh", "probable", "stale"],
            TextVocabulary.GetTexts<IndexFreshness>());
        Assert.False(TextVocabulary.IsDefined(default(IndexFreshness)));
        Assert.Equal(
            ["execution", "mutation", "test", "readIndex"],
            TextVocabulary.GetTexts<ReadyTarget>());
        Assert.False(TextVocabulary.IsDefined(default(ReadyTarget)));
        Assert.Equal(
            ["auto", "daemon", "oneshot"],
            TextVocabulary.GetTexts<AssuranceRequestedExecutionMode>());
        Assert.False(TextVocabulary.IsDefined(default(AssuranceRequestedExecutionMode)));
        Assert.Equal(
            ["daemon", "oneshot", "notApplicable"],
            TextVocabulary.GetTexts<AssuranceResolvedExecutionMode>());
        Assert.False(TextVocabulary.IsDefined(default(AssuranceResolvedExecutionMode)));
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
