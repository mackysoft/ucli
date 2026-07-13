using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunArtifactsServiceTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Prepare_CreatesRunScopedArtifactsDirectoryUnderFingerprintPath ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifacts", "prepare-run-dir");
        var configuration = CreateResolvedConfiguration(scope);
        var service = new TestRunArtifactsService(new TestRunMetaStore());
        var gitIgnorePath = Path.Combine(
            scope.FullPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);

        var result = await service.PrepareAsync(configuration);

        Assert.True(result.IsSuccess);
        var session = Assert.IsType<ArtifactsSession>(result.Session);
        Assert.Matches(new Regex(@"^\d{8}_\d{6}Z_[0-9a-f]{8}$"), session.RunId);
        Assert.StartsWith(
            Path.Combine(
                scope.FullPath,
                ".ucli",
                "local",
                "fingerprints",
                configuration.UnityProject.ProjectFingerprint.ToString(),
                "artifacts",
                "test"),
            session.Paths.ArtifactsDir,
            StringComparison.Ordinal);
        Assert.True(File.Exists(session.Paths.MetaJsonPath));
        Assert.Equal(Path.Combine(session.Paths.ArtifactsDir, "results.xml"), session.Paths.ResultsXmlPath);
        Assert.Equal(Path.Combine(session.Paths.ArtifactsDir, "editor.log"), session.Paths.EditorLogPath);
        Assert.Equal(Path.Combine(session.Paths.ArtifactsDir, "results.json"), session.Paths.ResultsJsonPath);
        Assert.Equal(Path.Combine(session.Paths.ArtifactsDir, "summary.json"), session.Paths.SummaryJsonPath);
        AssertMetaJsonContract(session.Paths.MetaJsonPath);
        FileSystemAssert.ForFile(gitIgnorePath).Exists();
        Assert.Equal(UcliContractConstants.LocalDirectoryIgnoreEntry + Environment.NewLine, File.ReadAllText(gitIgnorePath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Complete_UpdatesFinishedAtInMetaJson ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifacts", "complete-meta");
        var configuration = CreateResolvedConfiguration(scope);
        var timeProvider = new ManualTimeProvider();
        var service = new TestRunArtifactsService(new TestRunMetaStore(), timeProvider);

        var prepareResult = await service.PrepareAsync(configuration);
        Assert.True(prepareResult.IsSuccess);
        var session = Assert.IsType<ArtifactsSession>(prepareResult.Session);

        var before = ReadMetaJson(session.Paths.MetaJsonPath);
        timeProvider.Advance(TimeSpan.FromMilliseconds(20));

        var completeResult = await service.CompleteAsync(configuration, session, UnityExecutionTarget.Oneshot);

        var after = ReadMetaJson(session.Paths.MetaJsonPath);
        Assert.True(completeResult.IsSuccess);
        Assert.Equal(before.StartedAt, after.StartedAt);
        Assert.True(after.FinishedAt > before.FinishedAt);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Complete_WithOneshotTarget_DeletesInterruptedEditorLogExport ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifacts", "cleanup-editor-log-export");
        var configuration = CreateResolvedConfiguration(scope);
        var service = new TestRunArtifactsService(new TestRunMetaStore());
        var prepareResult = await service.PrepareAsync(configuration);
        var session = Assert.IsType<ArtifactsSession>(prepareResult.Session);
        var interruptedExportPath = CreateOwnedTemporaryPath(session.Paths.EditorLogPath, int.MaxValue);
        var unrelatedPath = session.Paths.EditorLogPath + ".tmp.keep";
        File.WriteAllText(interruptedExportPath, "partial");
        File.WriteAllText(unrelatedPath, "keep");

        var completeResult = await service.CompleteAsync(configuration, session, UnityExecutionTarget.Oneshot);

        Assert.True(completeResult.IsSuccess);
        Assert.False(File.Exists(interruptedExportPath));
        Assert.True(File.Exists(unrelatedPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Complete_WithDaemonTarget_PreservesEditorLogExport ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifacts", "preserve-daemon-editor-log-export");
        var configuration = CreateResolvedConfiguration(scope);
        var service = new TestRunArtifactsService(new TestRunMetaStore());
        var prepareResult = await service.PrepareAsync(configuration);
        var session = Assert.IsType<ArtifactsSession>(prepareResult.Session);
        var interruptedExportPath = CreateOwnedTemporaryPath(session.Paths.EditorLogPath, int.MaxValue);
        File.WriteAllText(interruptedExportPath, "partial");

        var completeResult = await service.CompleteAsync(configuration, session, UnityExecutionTarget.Daemon);

        Assert.True(completeResult.IsSuccess);
        Assert.True(File.Exists(interruptedExportPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Complete_WithOneshotTargetAndActiveEditorLogExport_PreservesExport ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifacts", "preserve-active-editor-log-export");
        var configuration = CreateResolvedConfiguration(scope);
        var service = new TestRunArtifactsService(new TestRunMetaStore());
        var prepareResult = await service.PrepareAsync(configuration);
        var session = Assert.IsType<ArtifactsSession>(prepareResult.Session);
        using var currentProcess = Process.GetCurrentProcess();
        var activeExportPath = CreateOwnedTemporaryPath(session.Paths.EditorLogPath, currentProcess.Id);
        File.WriteAllText(activeExportPath, "in progress");

        var completeResult = await service.CompleteAsync(configuration, session, UnityExecutionTarget.Oneshot);

        Assert.True(completeResult.IsSuccess);
        Assert.True(File.Exists(activeExportPath));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Complete_WithOneshotTargetAndMissingOwnerProcessId_PreservesExport ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifacts", "preserve-unowned-editor-log-export");
        var configuration = CreateResolvedConfiguration(scope);
        var service = new TestRunArtifactsService(new TestRunMetaStore());
        var prepareResult = await service.PrepareAsync(configuration);
        var session = Assert.IsType<ArtifactsSession>(prepareResult.Session);
        var unownedExportPath = session.Paths.EditorLogPath + ".tmp." + Guid.NewGuid().ToString("N");
        File.WriteAllText(unownedExportPath, "unknown owner");

        var completeResult = await service.CompleteAsync(configuration, session, UnityExecutionTarget.Oneshot);

        Assert.True(completeResult.IsSuccess);
        Assert.True(File.Exists(unownedExportPath));
    }

    private static string CreateOwnedTemporaryPath (string destinationPath, int processId)
    {
        return string.Concat(
            destinationPath,
            ".tmp.",
            processId.ToString(CultureInfo.InvariantCulture),
            ".",
            Guid.NewGuid().ToString("N"));
    }

    private static ResolvedTestRunConfiguration CreateResolvedConfiguration (TestDirectoryScope scope)
    {
        var projectPath = scope.GetPath("UnityProject");
        var testSettingsPath = scope.WriteFile("UnityProject/ProjectSettings/TestSettings.json", "{}");

        return new ResolvedTestRunConfiguration(
            UnityProject: ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: projectPath,
                repositoryRoot: scope.FullPath,
                projectFingerprint: ProjectFingerprintTestFactory.Create("abc123")),
            Mode: UnityExecutionMode.Oneshot,
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: TestRunPlatform.Player("StandaloneWindows64"),
            TestFilter: "Category=Smoke",
            TestCategories: ["smoke", "quick"],
            AssemblyNames: ["My.Tests"],
            TestSettingsPath: testSettingsPath,
            TimeoutMilliseconds: null);
    }

    private static void AssertMetaJsonContract (string metaJsonPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(metaJsonPath));
        JsonAssert.For(document.RootElement)
            .HasInt32("schemaVersion", 1)
            .HasString("testPlatform", "StandaloneWindows64");
        Assert.False(document.RootElement.TryGetProperty("buildTarget", out _));
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
