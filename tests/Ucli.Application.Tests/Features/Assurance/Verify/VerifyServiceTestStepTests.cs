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
        var testRunService = new RecordingVerifyTestRunService(_ => TestRunServiceResult.Pass(
            "Tests passed.",
            TestRunId,
            "/repo/.ucli/local/test/test-run-1",
            "/repo/.ucli/local/test/test-run-1/summary.json"));
        var service = CreateService(scope.FullPath, testRunService: testRunService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(AssuranceVerdict.Pass, result.Output!.Verdict);
        VerifyStepInvocationAssert.TestRunRequestedWithPlatform(
            testRunService,
            TestRunPlatform.EditMode);
        Assert.True(result.Output.Reports.ContainsKey("test.summary"));
        var claim = Assert.Single(result.Output.Claims);
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
        var testRunService = new RecordingVerifyTestRunService(_ => TestRunServiceResult.Fail(
            "Tests failed.",
            TestRunId,
            "/repo/.ucli/local/test/test-run-1",
            "/repo/.ucli/local/test/test-run-1/summary.json"));
        var service = CreateService(scope.FullPath, testRunService: testRunService);

        var result = await service.ExecuteAsync(new VerifyCommandInput(
            ProjectPath: null,
            Profile: null,
            ProfilePath: "verify.json",
            FromPath: null,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000));

        Assert.True(result.IsSuccess);
        Assert.Equal(AssuranceVerdict.Fail, result.Output!.Verdict);
        var claim = Assert.Single(result.Output.Claims);
        Assert.Equal(VerifyClaimCodes.UnityTestsPassed, claim.Id);
        Assert.Equal(AssuranceClaimStatus.Failed, claim.Status);
        Assert.True(claim.Required);
    }

}
