using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.AssuranceSemanticInvariantValidatorTestSupport;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics.BuildAssuranceSemanticInvariantValidatorTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Semantics;

public sealed class BuildAssuranceSemanticInvariantValidatorReportTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData("Packages/BuildProfiles/Linux.asset")]
    [InlineData("Assets/BuildProfiles/Linux.asset.meta")]
    [InlineData("Assets/../BuildProfiles/Linux.asset")]
    public void Validate_WithUnityBuildProfileInvalidPath_ReturnsPathViolation (string path)
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            useUnityBuildProfileInput: true,
            unityBuildProfilePath: path));

        AssertViolationPath(result, "$.build.inputs.unityBuildProfile.path");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadMissingStableReport_ReturnsReportsPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildLogReport: false));

        AssertViolationPath(result, "$.reports");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadReportMissingDigest_ReturnsReportDigestPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildLogDigest: false));

        AssertViolationPath(result, "$.reports.buildLog.digest");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadDigestOnlyReport_ReturnsReportPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildLogPath: false));

        AssertViolationPath(result, "$.reports.buildLog.path");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadReportInvalidDigest_ReturnsReportDigestPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(buildLogDigest: "sha256:dddd"));

        AssertViolationPath(result, "$.reports.buildLog.digest");
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("/workspace/.ucli/build.log")]
    [InlineData("C:/workspace/.ucli/build.log")]
    [InlineData("C:workspace/.ucli/build.log")]
    [InlineData("../build.log")]
    [InlineData("artifacts/../build.log")]
    [InlineData("artifacts//build.log")]
    [InlineData("artifacts\\build.log")]
    [InlineData(".")]
    [InlineData("")]
    public void Validate_WithBuildPayloadReportNonArtifactRootRelativePath_ReturnsReportPath (string buildLogPath)
    {
        var result = ValidateBuildPayload(CreateBuildPayload(buildLogPath: buildLogPath));

        AssertViolationPath(result, "$.reports.buildLog.path");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadReportKind_ReturnsReportKindPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildLogKind: true));

        AssertViolationPath(result, "$.reports.buildLog.kind");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadReportNonArtifactRootRelativePathAndNoClaims_ReturnsReportPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(
            includeBuildClaims: false,
            buildLogPath: "../build.log"));

        AssertViolationPath(result, "$.reports.buildLog.path");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildPayloadMissingProfile_ReturnsProfilePath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(includeBuildProfile: false));

        AssertViolationPath(result, "$.build.profile");
        Assert.Single(result.Violations, static violation => string.Equals(violation.Path, "$.build.profile", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildOutputManifestRefMismatch_ReturnsManifestRefPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(buildManifestRef: "build"));

        AssertViolationPath(result, "$.build.output.manifestRef");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildSummaryReportRefMismatch_ReturnsSummaryReportRefPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(summaryReportRef: "build"));

        AssertViolationPath(result, "$.build.summary.reportRef");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Validate_WithBuildLogsReportRefMismatch_ReturnsLogsReportRefPath ()
    {
        var result = ValidateBuildPayload(CreateBuildPayload(logsReportRef: "buildReport"));

        AssertViolationPath(result, "$.build.logs.reportRef");
    }
}
