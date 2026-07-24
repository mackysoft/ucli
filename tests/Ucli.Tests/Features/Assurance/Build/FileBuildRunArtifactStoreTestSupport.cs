using System.Runtime.InteropServices;
using System.Text;
using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Assurance.Build.Artifacts;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Assurance.Build;

namespace MackySoft.Ucli.Tests.Features.Assurance.Build;

internal static class FileBuildRunArtifactStoreTestSupport
{
    public static FileBuildRunArtifactStore CreateStore ()
    {
        return new FileBuildRunArtifactStore(
            new BuildOutputManifestJsonContractWriter(),
            new BuildRunMetadataDocumentWriter());
    }

    public static (FileBuildRunArtifactStore Store, BuildRunArtifactPaths Paths) PrepareArtifacts (TestDirectoryScope scope)
    {
        var store = CreateStore();
        var project = ResolvedUnityProjectContextTestFactory.CreateWithUnityProjectDirectory(scope, ProjectFingerprintTestFactory.Create("fingerprint"));
        var prepareResult = store.Prepare(project, RunIdTestValues.Build);

        Assert.True(prepareResult.IsSuccess);
        return (store, Assert.IsType<BuildRunArtifactPaths>(prepareResult.Paths));
    }

    public static byte[] WriteUtf8 (
        string path,
        string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var directoryPath = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllBytes(path, bytes);
        return bytes;
    }

    public static BuildRunArtifactAccountingRequest CreateAccountingRequest (
        BuildRunArtifactPaths paths,
        params string[] outputSourcePaths)
    {
        return CreateAccountingRequest(
            paths,
            BuildReportSourceEntry.FromArtifact(CreateBuildReportArtifact(paths)),
            outputSourcePaths);
    }

    public static BuildRunArtifactAccountingRequest CreateAccountingRequest (
        BuildRunArtifactPaths paths,
        BuildReportSourceEntry buildReportSource,
        params string[] outputSourcePaths)
    {
        var sourcePaths = outputSourcePaths.Length == 0
            ? [Path.Combine(paths.RunnerOutputDirectory.Value, "build")]
            : outputSourcePaths;
        return new BuildRunArtifactAccountingRequest(
            paths,
            BuildTargetStableName.StandaloneLinux64,
            "StandaloneLinux64",
            buildReportSource,
            sourcePaths
                .Select(static path => BuildOutputSourceEntry.FromAbsolutePath(AbsolutePath.Parse(path)))
                .ToArray(),
            allowEmptyOutputManifest: false);
    }

    public static BuildRunArtifactAccountingRequest CreateBuildReportOnlyAccountingRequest (
        BuildRunArtifactPaths paths,
        string buildReportSourcePath)
    {
        return new BuildRunArtifactAccountingRequest(
            paths,
            BuildTargetStableName.StandaloneLinux64,
            "StandaloneLinux64",
            BuildReportSourceEntry.FromRunnerOutputRelativePath(new BuildRunnerOutputPath(buildReportSourcePath)),
            Array.Empty<BuildOutputSourceEntry>(),
            allowEmptyOutputManifest: true);
    }

    public static IpcBuildReportArtifact CreateBuildReportArtifact (BuildRunArtifactPaths paths)
    {
        return new IpcBuildReportArtifact(
            SchemaVersion: 1,
            Result: IpcBuildReportResult.Succeeded,
            UnityBuildTarget: "StandaloneLinux64",
            OutputPath: Path.Combine(paths.RunnerOutputDirectory.Value, "build"),
            DurationMilliseconds: 2500,
            TotalSizeBytes: 4096,
            ErrorCount: 0,
            WarningCount: 1,
            Steps:
            [
                new IpcBuildReportStep(
                    Name: "Build player",
                    DurationMilliseconds: 2500,
                    Depth: 0,
                    MessageCount: 1),
            ],
            Messages:
            [
                new IpcBuildReportMessage(
                    Type: "warning",
                    Content: "Sample warning"),
            ]);
    }

    public static BuildRunArtifactPaths EscapeArtifactPath (
        BuildRunArtifactPaths paths,
        string pathKind,
        string escapedPath)
    {
        if (pathKind is not "buildJson"
            and not "buildReport"
            and not "buildLog"
            and not "outputManifest"
            and not "artifactOutput"
            and not "runnerOutput")
        {
            throw new ArgumentOutOfRangeException(nameof(pathKind), pathKind, "Unknown artifact path kind.");
        }

        var guardedEscapedPath = AbsolutePath.Parse(escapedPath);
        return new BuildRunArtifactPaths(
            paths.RepositoryRoot,
            paths.RunId,
            paths.ArtifactsDirectory,
            pathKind == "buildJson" ? guardedEscapedPath : paths.BuildJsonPath,
            pathKind == "buildReport" ? guardedEscapedPath : paths.BuildReportJsonPath,
            pathKind == "buildLog" ? guardedEscapedPath : paths.BuildLogPath,
            pathKind == "outputManifest" ? guardedEscapedPath : paths.OutputManifestJsonPath,
            pathKind == "runnerOutput" ? guardedEscapedPath : paths.RunnerOutputDirectory,
            pathKind == "artifactOutput" ? guardedEscapedPath : paths.ArtifactOutputDirectory);
    }

    public static void WriteUnityGeneratedArtifacts (BuildRunArtifactPaths paths)
    {
        WriteUtf8(paths.BuildReportJsonPath.Value, "{\"result\":\"succeeded\"}\n");
        WriteUtf8(paths.BuildLogPath.Value, "build log\n");
    }

    public static bool TryCreateFileSymbolicLink (
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

    public static bool TryCreateDirectorySymbolicLink (
        string symbolicLinkPath,
        string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(symbolicLinkPath, targetPath);
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

    public static bool CanOpenForRead (string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool CanEnumerateDirectory (string path)
    {
        try
        {
            _ = Directory.EnumerateFileSystemEntries(path).Any();
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static bool TryCreateFifo (string path)
    {
        return MkFifo(path, Convert.ToUInt32("600", 8)) == 0;
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "mkfifo")]
    private static extern int MkFifo (
        string path,
        uint mode);
}
