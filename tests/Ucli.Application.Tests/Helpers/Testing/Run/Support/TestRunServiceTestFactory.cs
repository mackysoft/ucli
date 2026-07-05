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
            TestSettingsPath: null,
            TimeoutMilliseconds: null);
    }

    public static ResolvedTestRunConfiguration CreateResolvedConfiguration (UnityExecutionMode mode = UnityExecutionMode.Auto)
    {
        return new ResolvedTestRunConfiguration(
            UnityProject: ProjectContextTestFactory.CreateSingleRootUnityProject(
                projectFingerprint: "fingerprint",
                unityVersion: ProjectIdentityDefaults.UnknownUnityVersion),
            Mode: mode,
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: Path.GetFullPath("./Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategories: [],
            AssemblyNames: [],
            TestSettingsPath: null,
            TimeoutMilliseconds: null);
    }

    public static ArtifactsSession CreateArtifactsSession (string? artifactsDir = null)
    {
        return new ArtifactsSession(
            RunId: "run-id",
            Paths: TestArtifactPaths.Create(artifactsDir ?? Path.Combine(Path.GetTempPath(), "ucli-test-run", "run-id")),
            StartedAtUtc: new DateTimeOffset(2026, 03, 08, 0, 0, 0, TimeSpan.Zero));
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
            ],
            HasFailureStatus: true,
            FailureStatus: IpcProtocol.StatusError);
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
            unityRequestResponse);
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
