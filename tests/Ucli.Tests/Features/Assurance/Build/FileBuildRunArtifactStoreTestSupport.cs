using System.Runtime.InteropServices;
using System.Text;
using MackySoft.Tests;
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
            ? [Path.Combine(paths.RunnerOutputDirectory, "build")]
            : outputSourcePaths;
        return new BuildRunArtifactAccountingRequest(
            paths,
            "standaloneLinux64",
            "StandaloneLinux64",
            buildReportSource,
            sourcePaths.Select(static path => BuildOutputSourceEntry.FromAbsolutePath(path)).ToArray(),
            AllowEmptyOutputManifest: false);
    }

    public static BuildRunArtifactAccountingRequest CreateBuildReportOnlyAccountingRequest (
        BuildRunArtifactPaths paths,
        string buildReportSourcePath)
    {
        return new BuildRunArtifactAccountingRequest(
            paths,
            "standaloneLinux64",
            "StandaloneLinux64",
            BuildReportSourceEntry.FromRunnerOutputRelativePath(buildReportSourcePath),
            Array.Empty<BuildOutputSourceEntry>(),
            AllowEmptyOutputManifest: true);
    }

    public static IpcBuildReportArtifact CreateBuildReportArtifact (BuildRunArtifactPaths paths)
    {
        return new IpcBuildReportArtifact(
            SchemaVersion: 1,
            Result: ContractLiteralCodec.ToValue(IpcBuildReportResult.Succeeded),
            UnityBuildTarget: "StandaloneLinux64",
            OutputPath: Path.Combine(paths.RunnerOutputDirectory, "build"),
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

        return new BuildRunArtifactPaths(
            paths.RepositoryRoot,
            paths.RunId,
            paths.ArtifactsDirectory,
            pathKind == "buildJson" ? escapedPath : paths.BuildJsonPath,
            pathKind == "buildReport" ? escapedPath : paths.BuildReportJsonPath,
            pathKind == "buildLog" ? escapedPath : paths.BuildLogPath,
            pathKind == "outputManifest" ? escapedPath : paths.OutputManifestJsonPath,
            pathKind == "runnerOutput" ? escapedPath : paths.RunnerOutputDirectory,
            pathKind == "artifactOutput" ? escapedPath : paths.ArtifactOutputDirectory);
    }

    public static void WriteUnityGeneratedArtifacts (BuildRunArtifactPaths paths)
    {
        WriteUtf8(paths.BuildReportJsonPath, "{\"result\":\"succeeded\"}\n");
        WriteUtf8(paths.BuildLogPath, "build log\n");
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
