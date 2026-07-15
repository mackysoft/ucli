using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Assurance.Compile;

namespace MackySoft.Ucli.Tests.Features.Assurance.Compile;

public sealed class FileCompileRunArtifactReaderTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadSummaryAsync_WhenSummaryIsSymbolicLink_ReturnsFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("compile-artifact-reader", "summary-symlink");
        var store = new FileCompileRunArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var summaryPath = store.ResolveSummaryPath(project, RunIdTestValues.Compile);
        var directoryPath = Path.GetDirectoryName(summaryPath)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {summaryPath}");
        Directory.CreateDirectory(directoryPath);
        var targetPath = Path.Combine(scope.FullPath, "target-summary.json");
        File.WriteAllText(targetPath, Serialize(CreateSummary(errorCount: 0)));
        if (!TryCreateFileSymbolicLink(summaryPath, targetPath))
        {
            return;
        }

        var result = await store.ReadSummaryAsync(project, RunIdTestValues.Compile, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsMissing);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
        Assert.Contains("reparse point", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadSummaryAsync_WhenSummaryExceedsLimit_ReturnsFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("compile-artifact-reader", "summary-too-large");
        var store = new FileCompileRunArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var summaryPath = store.ResolveSummaryPath(project, RunIdTestValues.Compile);
        var directoryPath = Path.GetDirectoryName(summaryPath)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {summaryPath}");
        Directory.CreateDirectory(directoryPath);
        await File.WriteAllTextAsync(summaryPath, new string(' ', (1024 * 1024) + 1), CancellationToken.None);

        var result = await store.ReadSummaryAsync(project, RunIdTestValues.Compile, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsMissing);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
        Assert.Contains("exceeded", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadSummaryAsync_WhenSummaryContainsEmptyRunId_ReturnsInvalidArtifactFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("compile-artifact-reader", "summary-empty-run-id");
        var store = new FileCompileRunArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var summaryPath = store.ResolveSummaryPath(project, RunIdTestValues.Compile);
        Directory.CreateDirectory(Path.GetDirectoryName(summaryPath)!);
        var invalidJson = Serialize(CreateSummary())
            .Replace(RunIdTestValues.CompileText, Guid.Empty.ToString("D"), StringComparison.Ordinal);
        await File.WriteAllTextAsync(summaryPath, invalidJson, CancellationToken.None);

        var result = await store.ReadSummaryAsync(project, RunIdTestValues.Compile, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsMissing);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, result.Error.Kind);
        Assert.Contains("invalid", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteArtifactsAsync_WhenArtifactsAlreadyExist_ReplacesArtifactsWithoutTempFiles ()
    {
        using var scope = TestDirectories.CreateTempScope("compile-artifact-reader", "write-replace");
        var store = new FileCompileRunArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var first = CreateSummary(errorCount: 1);
        var second = CreateSummary(errorCount: 0);

        var firstError = await store.WriteArtifactsAsync(project, RunIdTestValues.Compile, first, CancellationToken.None);
        var secondError = await store.WriteArtifactsAsync(project, RunIdTestValues.Compile, second, CancellationToken.None);
        var readResult = await store.ReadSummaryAsync(project, RunIdTestValues.Compile, CancellationToken.None);

        Assert.Null(firstError);
        Assert.Null(secondError);
        Assert.True(readResult.IsSuccess);
        Assert.Equal(0, readResult.Summary!.ScriptCompilation.Diagnostics.ErrorCount);
        var runDirectoryPath = Path.GetDirectoryName(store.ResolveSummaryPath(project, RunIdTestValues.Compile))
            ?? throw new InvalidOperationException("Run directory path could not be resolved.");
        Assert.DoesNotContain(
            Directory.EnumerateFiles(runDirectoryPath),
            path => Path.GetFileName(path).Contains(".tmp.", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteArtifactsAsync_WithMismatchedRunId_ReturnsInvalidArgumentWithoutCreatingStorage ()
    {
        using var scope = TestDirectories.CreateTempScope("compile-artifact-reader", "write-run-id-mismatch");
        var store = new FileCompileRunArtifactReader();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));

        var error = await store.WriteArtifactsAsync(
            project,
            RunIdTestValues.Build,
            CreateSummary(errorCount: 0),
            CancellationToken.None);

        Assert.NotNull(error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.False(Directory.Exists(Path.Combine(scope.FullPath, ".ucli")));
    }

    private static IpcCompileSummary CreateSummary (int errorCount = 0)
    {
        var primaryDiagnostic = errorCount == 0
            ? null
            : new IpcPrimaryDiagnostic(
                Kind: DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                Code: "CS1002",
                File: "Assets/Broken.cs",
                Line: 4,
                Column: 16,
                Message: "; expected");
        var canAcceptExecutionRequests = errorCount == 0;
        return new IpcCompileSummary(
            RunId: RunIdTestValues.Compile,
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
            Completed: true,
            StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
            CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
            Refresh: new IpcCompileSummary.RefreshEvidence(
                Origin: CompileRefreshOrigin.AssetDatabaseRefresh,
                Requested: true,
                StartedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:00Z"),
                CompletedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:02Z"),
                Completed: true),
            ScriptCompilation: new IpcCompileSummary.ScriptCompilationEvidence(
                Started: true,
                Completed: true,
                CompileGenerationBefore: 12,
                CompileGenerationAfter: 14,
                Diagnostics: new IpcCompileSummary.DiagnosticsEvidence(
                    ErrorCount: errorCount,
                    WarningCount: 0,
                    PrimaryDiagnostic: primaryDiagnostic)),
            DomainReload: new IpcCompileSummary.DomainReloadEvidence(
                ReloadRequired: false,
                ReloadObserved: false,
                GenerationBefore: 7,
                GenerationAfter: 7,
                Settled: true),
            Lifecycle: new IpcCompileSummary.LifecycleEvidence(
                ServerVersion: "0.5.0",
                UnityVersion: "6000.1.4f1",
                State: new UnityEditorStateSnapshot(
                    editorMode: DaemonEditorMode.Batchmode,
                    lifecycleState: canAcceptExecutionRequests
                        ? IpcEditorLifecycleState.Ready
                        : IpcEditorLifecycleState.CompileFailed,
                    compileState: canAcceptExecutionRequests
                        ? IpcCompileState.Ready
                        : IpcCompileState.Failed,
                    generations: new IpcUnityGenerationSnapshot(14, 7, 0, 0),
                    playMode: new IpcPlayModeSnapshot(
                        State: IpcPlayModeState.Stopped,
                        Transition: IpcPlayModeTransition.None,
                        IsPlaying: false,
                        IsPlayingOrWillChangePlaymode: false)),
                ObservedAtUtc: DateTimeOffset.Parse("2026-05-17T00:00:03Z"),
                ActionRequired: canAcceptExecutionRequests ? null : DaemonDiagnosisActionRequired.FixCompileErrors,
                PrimaryDiagnostic: primaryDiagnostic));
    }

    private static string Serialize (IpcCompileSummary summary)
    {
        return JsonSerializer.Serialize(summary, IpcJsonSerializerOptions.Default);
    }

    private static bool TryCreateFileSymbolicLink (
        string symbolicLinkPath,
        string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(symbolicLinkPath, targetPath);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}
