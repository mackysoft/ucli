using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Contracts.Editor;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.CompileCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class CompileCommandGoldenOutputTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("text")]
    [InlineData("json")]
    [Trait("Size", "Medium")]
    public async Task Compile_WithDefaultOrSupportedFormat_WritesOnlyFinalCommandResult (string? format)
    {
        var service = new RecordingCompileService((_, _, _) => ValueTask.FromResult<CompileExecutionResult>(CompileExecutionResult.Completed(CreateOutput())));
        var command = new CompileCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteWithErrorAsync(() => command.CompileAsync(
            format: format,
            cancellationToken: CancellationToken.None));

        CompileCommandAssert.SucceededWithOnlyFinalOutputAndGolden(
            result,
            service,
            CreateGoldenNormalization());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Compile_WithCompileErrorOutput_ReturnsOkEnvelopeWithFailureExitCodeAndMatchesGolden ()
    {
        var service = new RecordingCompileService((_, _, _) => ValueTask.FromResult<CompileExecutionResult>(CompileExecutionResult.Completed(CreateOutput(errorCount: 1))));
        var command = new CompileCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteAsync(() => command.CompileAsync(
            cancellationToken: CancellationToken.None));

        Assert.Equal(1, result.ExitCode);
        using var outputJson = JsonAssert.ParseMultilineObject(result.StdOut);
        CommandResultAssert.HasStandardEnvelope(
            outputJson.RootElement,
            UcliCommandNames.Compile,
            TextVocabulary.GetText(CommandResultStatus.Ok),
            1);
        Assert.Equal(
            TextVocabulary.GetText(Verdict.Fail),
            outputJson.RootElement
                .GetProperty("payload")
                .GetProperty("verdict")
                .GetString());

        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("compile", "compile-error.json"),
            result.StdOut,
            CreateGoldenNormalization());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WithFailureAfterRegistration_ReturnsClosedReconnectableErrorPayload ()
    {
        var project = ProjectIdentityInfoTestFactory.Create(
            projectFingerprint: ProjectFingerprintTestFactory.Create("<projectFingerprint>"));
        var service = new RecordingCompileService(
            (_, _, _) => ValueTask.FromResult<CompileExecutionResult>(
                CompileExecutionResult.Failed(
                    ApplicationFailure.Timeout(
                        "Waiting for Unity compile timed out.",
                        ExecutionErrorCodes.IpcTimeout),
                    project,
                    CreateActiveReference(),
                    ExecutionApplicationState.Indeterminate)));
        var command = new CompileCommand(
            service,
            CommandResultTestWriter.Create(),
            CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteAsync(
            () => command.CompileAsync(cancellationToken: CancellationToken.None));

        using var document = JsonAssert.ParseMultilineObject(
            result.StdOut);
        var payload = document.RootElement.GetProperty("payload");
        Assert.Equal(
            [
                "payloadKind",
                "project",
                "lifecycleExecutionRef",
                "applicationState",
            ],
            payload.EnumerateObject().Select(static property => property.Name));
        Assert.Equal(
            RunIdTestValues.CompileText,
            payload.GetProperty("lifecycleExecutionRef").GetProperty("id").GetString());
        Assert.Equal(
            TextVocabulary.GetText(ExecutionApplicationState.Indeterminate),
            payload.GetProperty("applicationState").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_WhenTerminalPublicationFails_KeepsTypedEvidenceInternal ()
    {
        var project = ProjectIdentityInfoTestFactory.Create(
            projectFingerprint: ProjectFingerprintTestFactory.Create(
                "<projectFingerprint>"));
        var typedResult = CreateLifecycleResult();
        var observedLifecycle = UnityEditorObservationTestFactory.Create(
            projectFingerprint: project.ProjectFingerprint,
            generations: new UnityEditorGenerationSnapshot(
                CompileGeneration: 14,
                DomainReloadGeneration: 7,
                AssetRefreshGeneration: 3,
                PlayModeGeneration: 2),
            observedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:03Z"));
        var service = new RecordingCompileService(
            (_, _, _) => ValueTask.FromResult<CompileExecutionResult>(
                CompileExecutionResult.Failed(
                    ApplicationFailure.InternalError(
                        "Compile terminal record could not be published.",
                        LifecycleExecutionErrorCodes.TerminalPublicationFailed),
                    project,
                    CreatePublishingReference(),
                    ExecutionApplicationState.Applied,
                    typedResult,
                    observedLifecycle)));
        var command = new CompileCommand(
            service,
            CommandResultTestWriter.Create(),
            CliStreamEntryWriterFactoryTestFixture.System);

        var result = await CommandResultCapture.ExecuteAsync(
            () => command.CompileAsync(
                cancellationToken: CancellationToken.None));

        using var document = JsonAssert.ParseMultilineObject(
            result.StdOut);
        var payload = document.RootElement.GetProperty("payload");
        Assert.Equal(
            "publishing",
            payload
                .GetProperty("lifecycleExecutionRef")
                .GetProperty("state")
                .GetString());
        Assert.Equal(
            "recovery",
            payload
                .GetProperty("lifecycleExecutionRef")
                .GetProperty("lifecycle")
                .GetString());
        Assert.False(payload.TryGetProperty("result", out _));
        Assert.False(payload.TryGetProperty("observedLifecycle", out _));
    }
}
