using MackySoft.Ucli.Application.Features.Assurance.Build.Vocabulary;
using MackySoft.Ucli.Tests.Helpers.Assurance;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunCliOutputSemanticContractTests
{
    [Theory]
    [InlineData("success.json")]
    [InlineData("build-report-failed.json")]
    [Trait("Size", "Medium")]
    public void OkGoldenPayloads_SatisfyBuildSemanticInvariants (string fileName)
    {
        var payload = BuildRunCliOutputContractTestSupport.ReadGoldenPayload(fileName);

        var result = CliAssuranceSemanticInvariantValidatorFactory.CreateBuildValidator().Validate(payload);

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Violations.Select(static violation => $"{violation.Path}: {violation.Message}")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void BuildReportFailureGolden_FixesVerifierFailureContract ()
    {
        using var document = BuildRunCliOutputContractTestSupport.ReadGoldenDocument("build-report-failed.json");
        var root = document.RootElement;
        var payload = root.GetProperty("payload");

        Assert.Equal(
            ContractLiteralCodec.ToValue(CommandResultStatus.Ok),
            root.GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("exitCode").GetInt32());
        Assert.Equal("fail", payload.GetProperty("verdict").GetString());
        Assert.Equal("failed", payload.GetProperty("build").GetProperty("summary").GetProperty("result").GetString());
        Assert.Equal("failed", payload.GetProperty("build").GetProperty("logs").GetProperty("completionReason").GetString());
        Assert.Equal("passed", BuildRunCliOutputContractTestSupport.GetClaimStatus(payload, BuildClaimCodes.UnityBuildCompleted.Value));
        Assert.Equal("failed", BuildRunCliOutputContractTestSupport.GetClaimStatus(payload, BuildClaimCodes.UnityBuildSucceeded.Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void DirtySceneFailureGolden_FixesDirtyStateContract ()
    {
        using var document = BuildRunCliOutputContractTestSupport.ReadGoldenDocument("dirty-scene.json");
        var root = document.RootElement;
        var payload = root.GetProperty("payload");
        var dirtyState = payload.GetProperty("dirtyState");
        var dirtyItem = dirtyState.GetProperty("items")[0];

        Assert.Equal(
            ContractLiteralCodec.ToValue(CommandResultStatus.Error),
            root.GetProperty("status").GetString());
        Assert.Equal(BuildErrorCodes.BuildDirtyStatePresent.Value, root.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.True(dirtyState.GetProperty("checked").GetBoolean());
        Assert.True(dirtyState.GetProperty("dirty").GetBoolean());
        Assert.Equal("full", dirtyState.GetProperty("coverage").GetString());
        Assert.Equal("scene", dirtyItem.GetProperty("kind").GetString());
        Assert.Equal("Assets/Scenes/Main.unity", dirtyItem.GetProperty("path").GetString());
    }

    [Theory]
    [InlineData("success.json")]
    [InlineData("build-report-failed.json")]
    [Trait("Size", "Medium")]
    public void BuildRunPayloadGoldens_UseArtifactRelativeReportsWithoutLegacyKeys (string fileName)
    {
        var payload = BuildRunCliOutputContractTestSupport.ReadGoldenPayload(fileName);
        var reports = payload.GetProperty("reports");

        Assert.False(reports.TryGetProperty("buildResult", out _));
        foreach (var report in reports.EnumerateObject())
        {
            Assert.False(report.Value.TryGetProperty("kind", out _));
            Assert.False(report.Value.TryGetProperty("category", out _));
            var path = report.Value.GetProperty("path").GetString()!;
            Assert.False(BuildRunCliOutputContractTestSupport.IsAbsoluteLikePath(path), path);
        }
    }

    [Theory]
    [InlineData("missing-report-ref", "$.claims[4].evidence[0].evidenceRef")]
    [InlineData("digest-only-entry", "$.reports.buildLog.path")]
    [InlineData("invalid-digest", "$.reports.buildLog.digest")]
    [InlineData("manifest-ref-mismatch", "$.build.output.manifestRef")]
    [Trait("Size", "Medium")]
    public void BuildSemanticInvariant_RejectsInvalidReportContracts (
        string caseName,
        string expectedViolationPath)
    {
        var payload = BuildRunCliOutputContractTestSupport.CreateMutatedSuccessPayload(caseName);

        var result = CliAssuranceSemanticInvariantValidatorFactory.CreateBuildValidator().Validate(payload);

        Assert.Contains(result.Violations, violation => string.Equals(violation.Path, expectedViolationPath, StringComparison.Ordinal));
    }
}
