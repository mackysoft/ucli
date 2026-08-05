using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Vocabulary;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Testing;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Verify.VerifyServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Verify;

public sealed class VerifyServiceTestStepTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithFileProfileTestPass_MapsUnityTestClaimAndReport ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithFileProfileTestPass_MapsUnityTestClaimAndReport));
        scope.WriteFile(
            "verify.json",
            """
            {
              "schemaVersion": 1,
              "name": "test-profile",
              "steps": [
                {
                  "kind": "test",
                  "required": true,
                  "effects": [
                    "unityTestRunner"
                  ],
                  "testPlatform": "editmode"
                }
              ]
            }
            """);
        var testRunArtifactsDirectory = AbsolutePath.Resolve(
            AbsolutePath.Parse(scope.FullPath),
            "test-artifacts");
        var testRunService = new RecordingVerifyTestRunService(_ => TestRunResultTestValues.CreateCompleted(
            Verdict.Pass,
            TestArtifactPaths.CreateSession(TestRunId, testRunArtifactsDirectory.Value)));
        var service = CreateService(scope.FullPath, testRunService: testRunService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: FilePathReference.Parse("verify.json"),
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        var completed = Assert.IsType<VerifyExecutionResult.CompletedResult>(result);
        Assert.Equal(Verdict.Pass, completed.Output.Verdict);
        VerifyStepInvocationAssert.TestRunRequestedWithPlatform(
            testRunService,
            TestRunPlatform.EditMode);
        Assert.True(completed.Output.Reports.ContainsKey(AssuranceReportIds.TestSummary.Value));
        var claim = Assert.Single(completed.Output.Claims);
        Assert.Equal(VerifyClaimCodes.UnityTestsPassed, claim.Id);
        Assert.Equal(AssuranceClaimStatus.Passed, claim.Status);
        Assert.True(claim.Required);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithFileProfileTestFail_MapsFailedClaimWithoutCommandError ()
    {
        using var scope = TestDirectories.CreateTempScope("ucli-verify", nameof(Execute_WithFileProfileTestFail_MapsFailedClaimWithoutCommandError));
        scope.WriteFile(
            "verify.json",
            """
            {
              "schemaVersion": 1,
              "name": "test-profile",
              "steps": [
                {
                  "kind": "test",
                  "required": true
                }
              ]
            }
            """);
        var testRunArtifactsDirectory = AbsolutePath.Resolve(
            AbsolutePath.Parse(scope.FullPath),
            "test-artifacts");
        var testRunService = new RecordingVerifyTestRunService(_ => TestRunResultTestValues.CreateCompleted(
            Verdict.Fail,
            TestArtifactPaths.CreateSession(TestRunId, testRunArtifactsDirectory.Value)));
        var service = CreateService(scope.FullPath, testRunService: testRunService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: FilePathReference.Parse("verify.json"),
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        var completed = Assert.IsType<VerifyExecutionResult.CompletedResult>(result);
        Assert.Equal(Verdict.Fail, completed.Output.Verdict);
        var claim = Assert.Single(completed.Output.Claims);
        Assert.Equal(VerifyClaimCodes.UnityTestsPassed, claim.Id);
        Assert.Equal(AssuranceClaimStatus.Failed, claim.Status);
        Assert.True(claim.Required);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithFileProfileTestIncomplete_MapsIndeterminatePartialClaim ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "ucli-verify",
            nameof(Execute_WithFileProfileTestIncomplete_MapsIndeterminatePartialClaim));
        scope.WriteFile(
            "verify.json",
            """
            {
              "schemaVersion": 1,
              "name": "test-profile",
              "steps": [
                {
                  "kind": "test",
                  "required": true
                }
              ]
            }
            """);
        var testRunArtifactsDirectory = AbsolutePath.Resolve(
            AbsolutePath.Parse(scope.FullPath),
            "test-artifacts");
        var testRunService = new RecordingVerifyTestRunService(_ => TestRunResultTestValues.CreateCompleted(
            Verdict.Incomplete,
            TestArtifactPaths.CreateSession(TestRunId, testRunArtifactsDirectory.Value)));
        var service = CreateService(scope.FullPath, testRunService: testRunService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: FilePathReference.Parse("verify.json"),
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        var completed = Assert.IsType<VerifyExecutionResult.CompletedResult>(result);
        Assert.Equal(Verdict.Incomplete, completed.Output.Verdict);
        var claim = Assert.Single(completed.Output.Claims);
        Assert.Equal(VerifyClaimCodes.UnityTestsPassed, claim.Id);
        Assert.Equal(AssuranceClaimStatus.Indeterminate, claim.Status);
        Assert.Equal(AssuranceCoverage.Partial, claim.Coverage);
        Assert.True(claim.Required);
    }

}
