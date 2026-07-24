using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunArtifactsServiceTests
{
    private const string ProcessOwnedTemporaryNonce = "0123456789abcd";

    private static readonly IGuidGenerator RunIdGenerator = new GuidGenerator();

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Prepare_CreatesRunScopedArtifactsDirectoryUnderFingerprintPath ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-artifacts", "prepare-run-dir");
        var configuration = CreateResolvedConfiguration(scope);
        var service = new TestRunArtifactsService(new TestRunMetaStore(), RunIdGenerator, TimeProvider.System);
        var gitIgnorePath = Path.Combine(
            scope.FullPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.GitIgnoreFileName);

        var result = await service.PrepareAsync(configuration);

        Assert.True(result.IsSuccess);
        var session = Assert.IsType<ArtifactsSession>(result.Session);
        Assert.NotEqual(Guid.Empty, session.RunId);
        Assert.Equal(
            StoragePathSegmentCodec.EncodeGuid(session.RunId, nameof(session.RunId)),
            Path.GetFileName(session.Paths.ArtifactsDir.Value));
        Assert.StartsWith(
            UcliStoragePathResolver.ResolveTestArtifactsDirectory(
                AbsolutePath.Parse(scope.FullPath),
                configuration.UnityProject.ProjectFingerprint).Value,
            session.Paths.ArtifactsDir.Value,
            StringComparison.Ordinal);
        Assert.True(File.Exists(session.Paths.MetaJsonPath.Value));
        Assert.Equal(Path.Combine(session.Paths.ArtifactsDir.Value, "results.xml"), session.Paths.ResultsXmlPath.Value);
        Assert.Equal(Path.Combine(session.Paths.ArtifactsDir.Value, "editor.log"), session.Paths.EditorLogPath.Value);
        Assert.Equal(Path.Combine(session.Paths.ArtifactsDir.Value, "results.json"), session.Paths.ResultsJsonPath.Value);
        Assert.Equal(Path.Combine(session.Paths.ArtifactsDir.Value, "summary.json"), session.Paths.SummaryJsonPath.Value);
        AssertMetaJsonContract(session);
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
        var service = new TestRunArtifactsService(new TestRunMetaStore(), RunIdGenerator, timeProvider);

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
        var service = new TestRunArtifactsService(new TestRunMetaStore(), RunIdGenerator, TimeProvider.System);
        var prepareResult = await service.PrepareAsync(configuration);
        var session = Assert.IsType<ArtifactsSession>(prepareResult.Session);
        var interruptedExportPath = CreateOwnedTemporaryPath(session.Paths.EditorLogPath, int.MaxValue);
        var unrelatedPath = Path.Combine(session.Paths.ArtifactsDir.Value, ".tmp2147483647-0123456789abcdef");
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
        var service = new TestRunArtifactsService(new TestRunMetaStore(), RunIdGenerator, TimeProvider.System);
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
        var service = new TestRunArtifactsService(new TestRunMetaStore(), RunIdGenerator, TimeProvider.System);
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
        var service = new TestRunArtifactsService(new TestRunMetaStore(), RunIdGenerator, TimeProvider.System);
        var prepareResult = await service.PrepareAsync(configuration);
        var session = Assert.IsType<ArtifactsSession>(prepareResult.Session);
        var unownedExportPath = Path.Combine(session.Paths.ArtifactsDir.Value, ".tmp--0123456789abcd");
        File.WriteAllText(unownedExportPath, "unknown owner");

        var completeResult = await service.CompleteAsync(configuration, session, UnityExecutionTarget.Oneshot);

        Assert.True(completeResult.IsSuccess);
        Assert.True(File.Exists(unownedExportPath));
    }

    private static string CreateOwnedTemporaryPath (AbsolutePath destinationPath, int processId)
    {
        var directoryPath = Path.GetDirectoryName(destinationPath.Value)
            ?? throw new InvalidOperationException($"Destination directory path could not be resolved: {destinationPath}");
        var fileName = string.Concat(
            ".tmp-",
            processId.ToString(CultureInfo.InvariantCulture),
            "-",
            ProcessOwnedTemporaryNonce);
        return Path.Combine(directoryPath, fileName);
    }

    private static ResolvedTestRunConfiguration CreateResolvedConfiguration (TestDirectoryScope scope)
    {
        var projectPath = scope.GetPath("UnityProject");

        return new ResolvedTestRunConfiguration(
            UnityProject: ResolvedUnityProjectContextTestFactory.CreateWithPaths(
                unityProjectRoot: projectPath,
                repositoryRoot: scope.FullPath,
                projectFingerprint: ProjectFingerprintTestFactory.Create("abc123")),
            Mode: UnityExecutionMode.Oneshot,
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: AbsolutePath.Parse(scope.GetPath("Editors/6000.1.4f1/Editor/Unity")),
            TestPlatform: TestRunPlatform.Player("StandaloneWindows64"),
            TestFilter: "Category=Smoke",
            TestCategories: ["smoke", "quick"],
            AssemblyNames: ["My.Tests"],
            TimeoutMilliseconds: null);
    }

    private static void AssertMetaJsonContract (ArtifactsSession session)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(session.Paths.MetaJsonPath.Value));
        JsonAssert.For(document.RootElement)
            .HasInt32("schemaVersion", 1)
            .HasString("runId", session.RunId.ToString("D"))
            .HasString("testPlatform", "StandaloneWindows64");
        Assert.False(document.RootElement.TryGetProperty("buildTarget", out _));
        Assert.False(document.RootElement.TryGetProperty("testSettingsPath", out _));
    }

    private static (DateTimeOffset StartedAt, DateTimeOffset FinishedAt) ReadMetaJson (AbsolutePath metaJsonPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(metaJsonPath.Value));
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
