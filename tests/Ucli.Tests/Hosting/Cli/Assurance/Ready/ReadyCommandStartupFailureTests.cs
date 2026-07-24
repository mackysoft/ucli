using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.ReadyCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class ReadyCommandStartupFailureTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Ready_WithStartupFailure_EmitsProjectAndStartupDiagnosis ()
    {
        var projectFingerprint = ProjectFingerprintTestFactory.Create("<projectFingerprint>");
        var startupFailure = CreateStartupFailureDetail();
        var service = new RecordingReadyService((_, _) => ValueTask.FromResult(ReadyExecutionResult.Failure(
            ApplicationFailure.UnityIpcFailure(
                "Unity startup is blocked.",
                DaemonErrorCodes.DaemonStartupBlocked,
                startupFailure: startupFailure),
            ProjectIdentityInfoTestFactory.Create(projectFingerprint: projectFingerprint))));
        var command = new ReadyCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.ReadyAsync(
            @for: "execution",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Ready,
            TextVocabulary.GetText(CommandResultStatus.Error),
            (int)CliExitCode.ToolError);
        CommandResultAssert.HasSingleError(outputJson.RootElement, DaemonErrorCodes.DaemonStartupBlocked);

        var payload = outputJson.RootElement.GetProperty("payload");
        JsonAssert.For(payload)
            .HasProperty("project", project => project
                .HasString("projectPath", ProjectIdentityInfoTestFactory.DefaultProjectPath)
                .HasString("projectFingerprint", projectFingerprint.ToString()))
            .HasProperty("startup", startup => startup
                .HasString("startupStatus", "blocked")
                .HasString("startupBlockingReason", "compile"))
            .HasProperty("diagnosis", diagnosis => diagnosis
                .HasString("reason", "unityScriptCompilationFailed")
                .HasProperty("primaryDiagnostic", primaryDiagnostic => primaryDiagnostic
                    .HasString("code", "CS0246")))
            .HasString("retryDisposition", "retryAfterFix")
            .HasBoolean("safeToRetryImmediately", false);
        Assert.False(payload.TryGetProperty("lifecycleState", out _));
        Assert.False(payload.TryGetProperty("blockingReason", out _));
        Assert.False(payload.TryGetProperty("canAcceptExecutionRequests", out _));
    }
}
