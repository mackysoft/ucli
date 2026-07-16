using MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Contracts.Ipc;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Build.BuildServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Build;

public sealed class BuildServiceInputProjectionTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithoutTimeoutOption_UsesBuildRunConfigOverride ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var timeoutOverrides = new Dictionary<string, int?>(UcliConfig.CreateDefault().IpcTimeoutMillisecondsByCommand, StringComparer.Ordinal)
        {
            [UcliCommandIds.BuildRun.Name] = 432100,
        };
        var config = UcliConfig.CreateDefault() with
        {
            IpcTimeoutMillisecondsByCommand = timeoutOverrides,
        };
        var requestExecutor = CreateBuildResponseExecutor(
            IpcBuildReportResult.Succeeded,
            IpcBuildLogCompletionReason.Completed,
            errorCount: 0);
        var service = CreateService(
            projectContextResolver: new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ProjectContextTestFactory.Create(
                config: config,
                projectFingerprint: DefaultProjectFingerprint))),
            requestExecutor: requestExecutor,
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath),
            timeProvider: new ManualTimeProvider());

        var result = await service.ExecuteAsync(CreateInput(timeoutMilliseconds: null));

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        BuildRunInvocationAssert.DispatchedWithTimeout(
            requestExecutor,
            expectedTimeout: TimeSpan.FromMilliseconds(432100));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithCompositeUnityDevelopmentBuildOptions_ReturnsSuccess ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        var service = CreateService(
            requestExecutor: CreateBuildResponseExecutor(
                IpcBuildReportResult.Succeeded,
                IpcBuildLogCompletionReason.Completed,
                errorCount: 0,
                buildOptions: "ForceOptimizeScriptCompilation, Il2CPP, Development"),
            artifactStore: new StubBuildRunArtifactStore(tempDirectory.FullPath));

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Execute_WithEditorBuildSettings_UsesUnityResolvedScenesInPayload ()
    {
        using var tempDirectory = CreateArtifactDirectoryScope();
        const string profileJson = """
            {
              "schemaVersion": 1,
              "inputs": {
                "kind": "explicit",
                "buildTarget": "standaloneLinux64",
                "scenes": {
                  "source": "editorBuildSettings"
                },
                "options": {
                  "development": true
                }
              },
              "runner": {
                "kind": "buildPipeline"
              },
              "policy": {
                "runtime": {
                  "allowedExecutionModes": [
                    "daemon",
                    "oneshot"
                  ],
                  "allowedEditorModes": [
                    "batchmode",
                    "gui"
                  ]
                },
                "projectMutationMode": "forbid"
              }
            }
            """;
        var artifactStore = new StubBuildRunArtifactStore(tempDirectory.FullPath);
        var requestExecutor = CreateBuildResponseExecutor(
            IpcBuildReportResult.Succeeded,
            IpcBuildLogCompletionReason.Completed,
            errorCount: 0,
            sceneSource: BuildProfileSceneSource.EditorBuildSettings,
            scenes: [new SceneAssetPath("Assets/Scenes/FromSettings.unity")]);
        var service = CreateService(
            profileFileReader: new StubBuildProfileFileReader(BuildProfileFileReadResult.Success(profileJson, "/workspace/build.ucli.json")),
            requestExecutor: requestExecutor,
            artifactStore: artifactStore);

        var result = await service.ExecuteAsync(CreateInput());

        Assert.True(result.IsSuccess, string.Join(Environment.NewLine, result.Errors.Select(static error => $"{error.Code}: {error.Message}")));
        Assert.Equal(BuildProfileSceneSource.EditorBuildSettings, result.Output!.Build.Inputs.Scenes.Source);
        Assert.Equal([new SceneAssetPath("Assets/Scenes/FromSettings.unity")], result.Output.Build.Inputs.Scenes.Paths);
        var metadataScenePaths = artifactStore.WrittenMetadata!.Inputs
            .GetProperty("scenes")
            .GetProperty("paths")
            .EnumerateArray()
            .Select(static item => item.GetString()!)
            .ToArray();
        Assert.Equal(["Assets/Scenes/FromSettings.unity"], metadataScenePaths);
        BuildRunInvocationAssert.EditorBuildSettingsDelegatedToUnity(requestExecutor);
    }
}
