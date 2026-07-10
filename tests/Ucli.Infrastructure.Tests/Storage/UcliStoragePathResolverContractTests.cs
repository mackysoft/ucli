using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliStoragePathResolverContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSupervisorManifestLockPath_ReturnsSupervisorScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveSupervisorManifestLockPath(
            UcliStoragePathResolverTestSupport.StorageRoot);

        UcliStoragePathResolverTestSupport.AssertStoragePath(
            resolvedPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.LocalDirectoryName,
            UcliStoragePathNames.SupervisorDirectoryName,
            UcliStoragePathNames.SupervisorManifestLockFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSessionOwnershipLockPaths_ReturnFingerprintScopedPaths ()
    {
        var sessionLockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);
        var supervisorLockPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestLockPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            sessionLockPath,
            UcliStoragePathNames.DaemonSessionLockFileName);
        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            supervisorLockPath,
            UcliStoragePathNames.GuiSupervisorManifestLockFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveConfigPath_ReturnsSharedUcliConfigPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveConfigPath(UcliStoragePathResolverTestSupport.StorageRoot);

        UcliStoragePathResolverTestSupport.AssertStoragePath(
            resolvedPath,
            UcliStoragePathNames.UcliDirectoryName,
            UcliStoragePathNames.ConfigFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolvePlanTokenKeyPath_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolvePlanTokenKeyPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.PlanTokenKeyFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveUnityUcliPluginMarkerCachePath_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.UnityUcliPluginMarkerCacheFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveMutationReadPostconditionPath_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.MutationReadPostconditionFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveFingerprintDirectory_WithUnsafeProjectFingerprint_ThrowsArgumentException ()
    {
        foreach (var projectFingerprint in UcliStoragePathResolverTestSupport.UnsafeProjectFingerprints)
        {
            Assert.Throws<ArgumentException>(() =>
            {
                UcliStoragePathResolver.ResolveFingerprintDirectory(
                    UcliStoragePathResolverTestSupport.StorageRoot,
                    projectFingerprint);
            });
        }
    }
}
