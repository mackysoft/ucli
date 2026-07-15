using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliStoragePathResolverPathBudgetTests
{
    private const int MaximumLegacyWindowsPathLength = 259;

    private static readonly Guid CaptureId =
        Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    private static readonly Guid RunId =
        Guid.Parse("3a1c6904-6c83-4e8d-a39d-0d9e2459ae16");

    private static readonly Guid BootstrapId =
        Guid.Parse("21234567-89ab-cdef-0123-456789abcdef");

    private static readonly Guid LaunchAttemptId =
        Guid.Parse("11234567-89ab-cdef-0123-456789abcdef");

    private static readonly Guid IndexGenerationId =
        Guid.Parse("31234567-89ab-cdef-0123-456789abcdef");

    public static TheoryData<string, string> DeepestResolvedPaths =>
        new()
        {
            {
                "scene lookup",
                UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(
                    UcliStoragePathResolverTestSupport.StorageRoot,
                    UcliStoragePathResolverTestSupport.ProjectFingerprint,
                    "Assets/Scenes/Sample.unity")
            },
            {
                "operation description",
                UcliStoragePathResolver.ResolveOpsDescribePath(
                    UcliStoragePathResolverTestSupport.StorageRoot,
                    UcliStoragePathResolverTestSupport.ProjectFingerprint,
                    Sha256Digest.Compute(Encoding.UTF8.GetBytes("ucli.go.describe")))
            },
            {
                "generation operation catalog",
                ResolveAtomicTemporaryPath(
                    UcliStoragePathResolver.ResolveOpsCatalogPath(
                        UcliStoragePathResolverTestSupport.StorageRoot,
                        UcliStoragePathResolverTestSupport.ProjectFingerprint,
                        IndexGenerationId))
            },
            {
                "generation asset lookup",
                ResolveAtomicTemporaryPath(
                    UcliStoragePathResolver.ResolveAssetSearchLookupPath(
                        UcliStoragePathResolverTestSupport.StorageRoot,
                        UcliStoragePathResolverTestSupport.ProjectFingerprint,
                        IndexGenerationId))
            },
            {
                "atomic screenshot staging file",
                ResolveAtomicTemporaryPath(
                    UcliStoragePathResolver.ResolveScreenshotCaptureRawStagingPath(
                        UcliStoragePathResolverTestSupport.StorageRoot,
                        UcliStoragePathResolverTestSupport.ProjectFingerprint,
                        CaptureId))
            },
            {
                "atomic screenshot artifact file",
                ResolveAtomicTemporaryPath(
                    UcliStoragePathResolver.ResolveScreenshotCaptureArtifactPath(
                        UcliStoragePathResolverTestSupport.StorageRoot,
                        UcliStoragePathResolverTestSupport.ProjectFingerprint,
                        CaptureId))
            },
            {
                "build runner output managed assembly",
                Path.Combine(
                    UcliStoragePathResolver.ResolveBuildRunOutputDirectory(
                        UcliStoragePathResolverTestSupport.StorageRoot,
                        RunId),
                    "player",
                    "Player_Data",
                    "Managed",
                    "Assembly-CSharp.dll")
            },
            {
                "build artifact managed assembly",
                Path.Combine(
                    UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
                        UcliStoragePathResolverTestSupport.StorageRoot,
                        RunId),
                    UcliStoragePathNames.BuildOutputDirectoryName,
                    "output-0001",
                    "Player_Data",
                    "Managed",
                    "Assembly-CSharp.dll")
            },
            {
                "oneshot bootstrap envelope",
                UcliStoragePathResolver.ResolveOneshotBootstrapPath(
                    UcliStoragePathResolverTestSupport.StorageRoot,
                    UcliStoragePathResolverTestSupport.ProjectFingerprint,
                    BootstrapId)
            },
            {
                "launch attempt diagnosis",
                UcliStoragePathResolver.ResolveLaunchAttemptStartupDiagnosisPath(
                    UcliStoragePathResolverTestSupport.StorageRoot,
                    UcliStoragePathResolverTestSupport.ProjectFingerprint,
                    LaunchAttemptId)
            },
            {
                "build output manifest",
                Path.Combine(
                    UcliStoragePathResolver.ResolveBuildRunArtifactsDirectory(
                        UcliStoragePathResolverTestSupport.StorageRoot,
                        RunId),
                    UcliStoragePathNames.BuildOutputManifestFileName)
            },
            {
                "compile diagnostics",
                Path.Combine(
                    UcliStoragePathResolver.ResolveCompileRunArtifactsDirectory(
                        UcliStoragePathResolverTestSupport.StorageRoot,
                        UcliStoragePathResolverTestSupport.ProjectFingerprint,
                        RunId),
                    UcliStoragePathNames.CompileDiagnosticsFileName)
            },
            {
                "process-owned editor log temporary file",
                Path.Combine(
                    UcliStoragePathResolver.ResolveTestRunArtifactsDirectory(
                        UcliStoragePathResolverTestSupport.StorageRoot,
                        UcliStoragePathResolverTestSupport.ProjectFingerprint,
                        RunId),
                    new string('x', EditorLogTemporaryFilePath.MaximumFileNameLength))
            },
            {
                "atomic recoverable IPC operation record",
                ResolveAtomicTemporaryPath(
                    Path.Combine(
                        UcliStoragePathResolver.ResolveProjectDirectory(
                            UcliStoragePathResolverTestSupport.StorageRoot,
                            UcliStoragePathResolverTestSupport.ProjectFingerprint),
                        UcliStoragePathNames.IpcOperationsDirectoryName,
                        StoragePathSegmentCodec.EncodeGuid(RunId, nameof(RunId)),
                        "operation.json"))
            },
        };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(DeepestResolvedPaths))]
    public void DeepestResolvedPaths_WithMaximumSupportedWindowsRoot_FitLegacyPathLimit (
        string pathKind,
        string resolvedPath)
    {
        var projectedPathLength = GetProjectedWindowsPathLength(resolvedPath);

        Assert.True(
            projectedPathLength <= MaximumLegacyWindowsPathLength,
            $"{pathKind} requires {projectedPathLength} characters.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ProcessOwnedEditorLogTemporaryPath_DefinesMaximumSupportedWindowsStorageRootLength ()
    {
        var editorLogTemporaryPath = Path.Combine(
            UcliStoragePathResolver.ResolveTestRunArtifactsDirectory(
                UcliStoragePathResolverTestSupport.StorageRoot,
                UcliStoragePathResolverTestSupport.ProjectFingerprint,
                RunId),
            new string('x', EditorLogTemporaryFilePath.MaximumFileNameLength));

        Assert.Equal(
            MaximumLegacyWindowsPathLength,
            GetProjectedWindowsPathLength(editorLogTemporaryPath));
    }

    private static string ResolveAtomicTemporaryPath (string destinationPath)
    {
        return Path.Combine(
            Path.GetDirectoryName(destinationPath)!,
            FileUtilities.CreateAtomicWriteTemporaryFileName());
    }

    private static int GetProjectedWindowsPathLength (string resolvedPath)
    {
        var relativePath = Path.GetRelativePath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            resolvedPath);
        return UcliStoragePathResolver.MaximumSupportedWindowsStorageRootLength
            + 1
            + relativePath.Length;
    }
}
