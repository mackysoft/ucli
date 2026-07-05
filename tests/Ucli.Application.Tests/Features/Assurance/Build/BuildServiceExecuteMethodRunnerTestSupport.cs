using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

internal static class BuildServiceExecuteMethodRunnerTestSupport
{
    public static async Task<BuildExecutionResult> ExecuteWithExecuteMethodRunnerResultAsync (
        IpcBuildRunnerResultArtifact runnerResult,
        string completionReason,
        StubBuildRunArtifactStore artifactStore,
        bool writeRunnerResultOutputs = true,
        bool writeRunnerBuildReportSource = true,
        string? runnerBuildReportSourceJson = null)
    {
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: string.Empty);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: CreateBuildResponseExecutor(
                runnerResult.Status,
                completionReason,
                runnerResult.ErrorCount,
                runnerResult: runnerResult,
                omitReport: true,
                writeRunnerResultOutputs: writeRunnerResultOutputs,
                writeRunnerBuildReportSource: writeRunnerBuildReportSource,
                runnerBuildReportSourceJson: runnerBuildReportSourceJson),
            artifactStore: artifactStore);

        return await service.ExecuteAsync(CreateInput());
    }

    public static async Task<BuildExecutionResult> ExecuteWithExecuteMethodRunnerResultAsync (
        IpcBuildRunnerResultArtifact runnerResult,
        string completionReason = "completed",
        bool writeRunnerResultOutputs = true,
        bool writeRunnerBuildReportSource = true,
        string? runnerBuildReportSourceJson = null)
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        return await ExecuteWithExecuteMethodRunnerResultAsync(
            runnerResult,
            completionReason,
            new StubBuildRunArtifactStore(tempDirectory.FullPath),
            writeRunnerResultOutputs,
            writeRunnerBuildReportSource,
            runnerBuildReportSourceJson);
    }

    public static Task<BuildExecutionResult> ExecuteWithMalformedExecuteMethodRunnerResultPayloadAsync (
        Action<JsonObject> mutatePayload)
    {
        return ExecuteWithMalformedExecuteMethodRunnerResultRawPayloadAsync(payloadJson =>
        {
            var payloadObject = JsonNode.Parse(payloadJson)!.AsObject();
            mutatePayload(payloadObject);
            return payloadObject.ToJsonString();
        });
    }

    public static async Task<BuildExecutionResult> ExecuteWithMalformedExecuteMethodRunnerResultRawPayloadAsync (
        Func<string, string> mutatePayloadJson)
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var runnerResult = CreateExecuteMethodRunnerResult(
            status: ContractLiteralCodec.ToValue(IpcBuildReportResult.Failed),
            outputs: [],
            errorCount: 1,
            warningCount: 0);
        var profileJson = CreateExecuteMethodProfileJson(
            method: "Build.Entry.Run",
            arguments: string.Empty,
            environment: string.Empty);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: new RecordingUnityRequestExecutor(payload =>
            {
                var buildRunPayload = (UnityRequestPayload.BuildRun)payload;
                WriteRunnerResultFiles(
                    buildRunPayload,
                    runnerResult,
                    runnerResult.Status,
                    runnerResult.ErrorCount,
                    writeOutputs: true,
                    writeBuildReportSource: true,
                    buildReportSourceJson: null);
                var result = CreateBuildResponseResult(
                    runnerResult.Status,
                    ContractLiteralCodec.ToValue(IpcBuildLogCompletionReason.Failed),
                    runnerResult.ErrorCount,
                    reportOutputPath: buildRunPayload.OutputPath,
                    runnerResult: runnerResult,
                    omitReport: true);
                var response = result.Response!;
                using var document = JsonDocument.Parse(mutatePayloadJson(response.Payload.GetRawText()));
                return UnityRequestExecutionResult.Success(response with
                {
                    Payload = document.RootElement.Clone(),
                });
            }),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        return await service.ExecuteAsync(CreateInput());
    }

    public static IpcBuildRunnerResultArtifact CreateExecuteMethodRunnerResult (
        string? status = null,
        IReadOnlyList<string>? outputs = null,
        IpcBuildRunnerResultBuildReport? buildReport = null,
        long durationMilliseconds = 2500,
        int errorCount = 0,
        int warningCount = 1,
        IReadOnlyList<IpcBuildRunnerDiagnostic>? diagnostics = null)
    {
        return new IpcBuildRunnerResultArtifact(
            Source: ContractLiteralCodec.ToValue(IpcBuildRunnerResultSource.UcliBuildRunnerResult),
            Status: status ?? ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            DurationMilliseconds: durationMilliseconds,
            ErrorCount: errorCount,
            WarningCount: warningCount,
            Diagnostics: diagnostics ?? [])
        {
            Outputs = outputs ?? ["player.txt"],
            BuildReport = buildReport,
        };
    }

    public static void MutateInvalidRunnerResultShape (
        string caseName,
        JsonObject payload)
    {
        switch (caseName)
        {
            case "runnerResult-array":
                payload["runnerResult"] = new JsonArray();
                break;
            case "runnerResult-extra-property":
                payload["runnerResult"]!.AsObject()["extra"] = true;
                break;
            case "outputs-string":
                payload["runnerResult"]!.AsObject()["outputs"] = "player.txt";
                break;
            case "outputs-non-string-item":
                payload["runnerResult"]!.AsObject()["outputs"] = new JsonArray(1);
                break;
            case "buildReport-path-non-string":
                payload["runnerResult"]!.AsObject()["buildReport"] = new JsonObject
                {
                    ["path"] = 1,
                };
                break;
            default:
                throw new InvalidOperationException($"Unsupported runner result shape case '{caseName}'.");
        }
    }

    public static string MutateDuplicateRunnerResultProperty (
        string caseName,
        string payloadJson)
    {
        return caseName switch
        {
            "outputs-case-variant" => payloadJson.Replace(
                "\"outputs\":[]",
                "\"outputs\":[],\"Outputs\":[]",
                StringComparison.Ordinal),
            "diagnostic-code-case-variant" => payloadJson.Replace(
                "\"diagnostics\":[]",
                "\"diagnostics\":[{\"code\":\"D\",\"Code\":\"E\",\"severity\":\"error\",\"message\":\"m\"}]",
                StringComparison.Ordinal),
            "buildReport-path-case-variant" => payloadJson.Replace(
                "\"buildReport\":null",
                "\"buildReport\":{\"path\":\"reports/a.json\",\"Path\":\"reports/b.json\"}",
                StringComparison.Ordinal),
            _ => throw new InvalidOperationException($"Unsupported duplicate runner result property case '{caseName}'."),
        };
    }
}
