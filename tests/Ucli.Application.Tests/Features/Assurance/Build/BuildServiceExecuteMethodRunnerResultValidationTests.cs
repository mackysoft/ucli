using System.Text.Json.Nodes;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceExecuteMethodRunnerTestSupport;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceExecuteMethodRunnerResultValidationTests
{
    public static TheoryData<string> InvalidExecuteMethodRunnerResultShapeCases => new()
    {
        "runnerResult-array",
        "runnerResult-extra-property",
        "outputs-string",
        "outputs-non-string-item",
        "buildReport-path-non-string",
    };

    public static TheoryData<string> DuplicateExecuteMethodRunnerResultPropertyCases => new()
    {
        "outputs-case-variant",
        "diagnostic-code-case-variant",
        "buildReport-path-case-variant",
    };

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithExecuteMethodRunnerResultStatusMismatch_ReturnsCommandFailure ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: string.Empty);
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                runnerResult: CreateExecuteMethodRunnerResult(
                    status: IpcBuildReportResult.Failed,
                    outputs: [],
                    errorCount: 1,
                    warningCount: 0),
                omitReport: true),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(UcliCoreErrorCodes.InternalError, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithMissingExecuteMethodRunnerResult_ReturnsBuildRunnerResultMissing ()
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: string.Empty);
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                omitReport: true),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultMissing, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithExecuteMethodSucceededAndEmptyOutputs_ReturnsBuildRunnerResultInvalid ()
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync(
            static payload =>
            {
                var runnerResult = payload["runnerResult"]!.AsObject();
                runnerResult["status"] = "succeeded";
                runnerResult["outputs"] = new JsonArray();
            });

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithExecuteMethodRunnerResultOutputsNull_ReturnsBuildRunnerResultInvalid ()
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync(
            static payload => payload["runnerResult"]!.AsObject()["outputs"] = null);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithExecuteMethodRunnerResultOutputsMissing_ReturnsBuildRunnerResultInvalid ()
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync(
            static payload => payload["runnerResult"]!.AsObject().Remove("outputs"));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [MemberData(nameof(InvalidExecuteMethodRunnerResultShapeCases))]
    public async Task Execute_WithInvalidExecuteMethodRunnerResultPayloadShape_ReturnsBuildRunnerResultInvalid (
        string caseName)
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync(payload => MutateInvalidRunnerResultShape(caseName, payload));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [MemberData(nameof(DuplicateExecuteMethodRunnerResultPropertyCases))]
    public async Task Execute_WithDuplicateExecuteMethodRunnerResultProperty_ReturnsBuildRunnerResultInvalid (
        string caseName)
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultRawPayloadAsync(payloadJson => MutateDuplicateRunnerResultProperty(caseName, payloadJson));

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData(IpcBuildReportResult.Failed, IpcBuildLogCompletionReason.Failed)]
    [InlineData(IpcBuildReportResult.Canceled, IpcBuildLogCompletionReason.Canceled)]
    public async Task Execute_WithUnsuccessfulExecuteMethodAndEmptyOutputs_AllowsEmptyOutputManifest (
        IpcBuildReportResult runnerStatus,
        IpcBuildLogCompletionReason completionReason)
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var result = await ExecuteWithExecuteMethodRunnerResultAsync(
            CreateExecuteMethodRunnerResult(
                status: runnerStatus,
                outputs: [],
                errorCount: runnerStatus == IpcBuildReportResult.Failed ? 1 : 0,
                warningCount: 0),
            completionReason,
            artifactStore);

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        var accountingRequest = Assert.IsType<BuildRunArtifactAccountingRequest>(artifactStore.AccountingRequest);
        Assert.Empty(accountingRequest.OutputSources);
        Assert.True(accountingRequest.AllowEmptyOutputManifest);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("unsupported", 2500, 0, 1)]
    [InlineData("succeeded", -1, 0, 1)]
    public async Task Execute_WithInvalidExecuteMethodRunnerResultShape_ReturnsBuildRunnerResultInvalid (
        string status,
        long durationMilliseconds,
        int errorCount,
        int warningCount)
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync(payload =>
        {
            var runnerResult = payload["runnerResult"]!;
            runnerResult["status"] = status;
            runnerResult["durationMilliseconds"] = durationMilliseconds;
            runnerResult["errorCount"] = errorCount;
            runnerResult["warningCount"] = warningCount;
        });

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithInvalidExecuteMethodDiagnostics_ReturnsBuildRunnerResultInvalid ()
    {
        var result = await ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync(payload =>
        {
            payload["runnerResult"]!["diagnostics"] = new JsonArray
            {
                new JsonObject
                {
                    ["severity"] = "verbose",
                    ["code"] = "diagnostic",
                    ["message"] = "Unsupported severity",
                },
            };
        });

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(BuildErrorCodes.BuildRunnerResultInvalid, error.Code);
    }
}
