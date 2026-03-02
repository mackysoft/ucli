using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MackySoft.Tests;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunArtifactsServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_CreatesRunScopedArtifactsDirectoryUnderFingerprintPath ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifacts", "prepare-run-dir");
        var configuration = CreateResolvedConfiguration(scope);
        var service = new TestRunArtifactsService();

        var result = await service.Prepare(configuration);

        Assert.True(result.IsSuccess);
        var session = Assert.IsType<ArtifactsSession>(result.Session);
        Assert.Matches(new Regex(@"^\d{8}_\d{6}Z_[0-9a-f]{8}$"), session.RunId);
        Assert.StartsWith(
            Path.Combine(
                scope.FullPath,
                ".ucli",
                "local",
                "fingerprints",
                configuration.UnityProject.ProjectFingerprint,
                "artifacts",
                "test"),
            session.ArtifactsDir,
            StringComparison.Ordinal);
        Assert.True(File.Exists(session.Paths.MetaJsonPath));
        Assert.Equal(Path.Combine(session.ArtifactsDir, "results.xml"), session.Paths.ResultsXmlPath);
        Assert.Equal(Path.Combine(session.ArtifactsDir, "editor.log"), session.Paths.EditorLogPath);
        Assert.Equal(Path.Combine(session.ArtifactsDir, "results.json"), session.Paths.ResultsJsonPath);
        Assert.Equal(Path.Combine(session.ArtifactsDir, "summary.json"), session.Paths.SummaryJsonPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Complete_UpdatesFinishedAtInMetaJson ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifacts", "complete-meta");
        var configuration = CreateResolvedConfiguration(scope);
        var service = new TestRunArtifactsService();

        var prepareResult = await service.Prepare(configuration);
        Assert.True(prepareResult.IsSuccess);
        var session = Assert.IsType<ArtifactsSession>(prepareResult.Session);

        var before = ReadMetaJson(session.Paths.MetaJsonPath);
        await Task.Delay(20);

        var completeResult = await service.Complete(configuration, session);

        var after = ReadMetaJson(session.Paths.MetaJsonPath);
        Assert.True(completeResult.IsSuccess);
        Assert.Equal(before.StartedAt, after.StartedAt);
        Assert.True(after.FinishedAt > before.FinishedAt);
    }

    private static ResolvedTestRunConfiguration CreateResolvedConfiguration (TestDirectoryScope scope)
    {
        var projectPath = scope.GetPath("UnityProject");
        var testSettingsPath = scope.WriteFile("UnityProject/ProjectSettings/TestSettings.json", "{}");

        return new ResolvedTestRunConfiguration(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: projectPath,
                RepositoryRoot: scope.FullPath,
                ProjectFingerprint: "abc123",
                PathSource: UnityProjectPathSource.CommandOption),
            Mode: "oneshot",
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: TestRunPlatform.PlayMode,
            RawTestPlatform: "playmode",
            BuildTarget: "StandaloneWindows64",
            TestFilter: "Category=Smoke",
            TestCategories: ["smoke", "quick"],
            AssemblyNames: ["My.Tests"],
            TestSettingsPath: testSettingsPath,
            TimeoutSeconds: 1800);
    }

    private static (DateTimeOffset StartedAt, DateTimeOffset FinishedAt) ReadMetaJson (string metaJsonPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(metaJsonPath));
        var root = document.RootElement;
        var startedAt = DateTimeOffset.Parse(
            root.GetProperty("startedAt").GetString()!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
        var finishedAt = DateTimeOffset.Parse(
            root.GetProperty("finishedAt").GetString()!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);

        return (startedAt, finishedAt);
    }
}