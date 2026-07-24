using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Features.Testing.Run.Results;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Preflight;
using MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Projection;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Testing;
using static MackySoft.Ucli.Application.Tests.Helpers.ApplicationCommandInputTestHelper;

namespace MackySoft.Ucli.Application.Tests;

internal static class TestRunServiceTestFactory
{
    public static readonly Guid RunId = Guid.Parse("dbd61ee7-6a8a-4555-80a6-64b486a97d29");
    public static readonly Guid OtherRunId = Guid.Parse("09ff7fb2-67ea-42c2-a142-bd4f7445782a");

    public static TestRunCommandInput CreateInput ()
    {
        return new TestRunCommandInput(
            ProjectPath: null,
            ProfilePath: null,
            Mode: NormalizeMode(null),
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: NormalizeTestPlatform(null),
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TimeoutMilliseconds: null);
    }

    public static ResolvedTestRunConfiguration CreateResolvedConfiguration (UnityExecutionMode mode = UnityExecutionMode.Auto)
    {
        return new ResolvedTestRunConfiguration(
            UnityProject: ProjectContextTestFactory.CreateSingleRootUnityProject(
                projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
                unityVersion: ProjectIdentityDefaults.UnknownUnityVersion),
            Mode: mode,
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: AbsolutePath.Parse(Path.GetFullPath("./Editors/6000.1.4f1/Editor/Unity")),
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategories: [],
            AssemblyNames: [],
            TimeoutMilliseconds: null);
    }

    public static ArtifactsSession CreateArtifactsSession (string? artifactsDir = null)
    {
        return new ArtifactsSession(
            runId: RunId,
            paths: TestArtifactPaths.Create(artifactsDir ?? Path.Combine(Path.GetTempPath(), "ucli-test-run", RunId.ToString("D"))),
            startedAtUtc: new DateTimeOffset(2026, 03, 08, 0, 0, 0, TimeSpan.Zero));
    }

    public static UnityRequestResponse CreateFailureUnityRequestResponse (
        UcliCode code,
        string message)
    {
        return new UnityRequestResponse(
            Payload: IpcPayloadCodec.SerializeToElement(new { }),
            Errors:
            [
                new OperationExecutionError(code, message, OpId: null),
            ]);
    }

    public static TestRunService CreateService (
        ITestRunConfigurationResolver configurationResolver,
        IUnityExecutionModeDecisionService modeDecisionService,
        ITestRunArtifactsService artifactsService,
        StubUnityTestExecutor unityTestExecutor,
        IUnityResultsConverter resultsConverter,
        IUcliConfigStore? configStore = null,
        RecordingDaemonTestRunClient? daemonTestRunClient = null,
        UnityRequestProgressFrame? streamingProgressFrame = null,
        IReadOnlyList<UnityRequestProgressFrame>? streamingProgressFrames = null,
        UnityRequestResponse? unityRequestResponse = null)
    {
        var preflightService = new TestRunPreflightService(
            configurationResolver,
            configStore ?? new StubUcliConfigStore(),
            modeDecisionService);
        var unityRequestExecutor = new StubTestRunUnityRequestExecutor(
            unityTestExecutor,
            daemonTestRunClient,
            streamingProgressFrames ?? (streamingProgressFrame is null ? null : [streamingProgressFrame]),
            unityRequestResponse,
            runId => artifactsService is StubTestRunArtifactsService stubArtifactsService
                ? stubArtifactsService.GetPreparedPaths(runId)
                : throw new InvalidOperationException("Test-run service tests require the recording artifact service."));
        var executionPipeline = new TestRunExecutionPipeline(
            artifactsService,
            unityRequestExecutor,
            resultsConverter,
            new StubTestRunArtifactExistenceProbe(),
            unityRequestExecutor);
        var resultMapper = new TestRunResultMapper();

        return new TestRunService(
            preflightService,
            executionPipeline,
            resultMapper);
    }
}
