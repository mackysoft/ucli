using MackySoft.FileSystem;
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
    public void ResolveSessionOwnershipLockPaths_ReturnProjectScopedPaths ()
    {
        var sessionLockPath = UcliStoragePathResolver.ResolveDaemonSessionLockPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);
        var supervisorLockPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestLockPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            sessionLockPath,
            UcliStoragePathNames.DaemonSessionLockFileName);
        UcliStoragePathResolverTestSupport.AssertProjectPath(
            supervisorLockPath,
            UcliStoragePathNames.GuiSupervisorManifestLockFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveDaemonLifecycleLockPath_ReturnsProjectScopedPathWithoutReparsingTheLifecyclePath ()
    {
        var lockPath = UcliStoragePathResolver.ResolveDaemonLifecycleLockPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            lockPath,
            UcliStoragePathNames.DaemonLifecycleFileName + ".lock");
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
    public void ResolvePlanTokenKeyPath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolvePlanTokenKeyPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.PlanTokenKeyFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveUnityUcliPluginMarkerCachePath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveUnityUcliPluginMarkerCachePath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.UnityUcliPluginMarkerCacheFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveMutationReadPostconditionPath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.MutationReadPostconditionFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveProjectDirectory_WithNullProjectFingerprint_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() =>
            UcliStoragePathResolver.ResolveProjectDirectory(
                UcliStoragePathResolverTestSupport.StorageRoot,
                null!));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveUcliDirectoryPath_WithLongStorageRoot_ReturnsPath ()
    {
        var storageRoot = AbsolutePath.Parse(CreateRootedPathWithLength(160));

        var resolvedPath = UcliStoragePathResolver.ResolveUcliDirectoryPath(storageRoot);

        Assert.Equal(
            Path.Combine(storageRoot.Value, UcliStoragePathNames.UcliDirectoryName),
            resolvedPath.Value);
    }

    private static string CreateRootedPathWithLength (int length)
    {
        var root = Path.GetPathRoot(Environment.CurrentDirectory)
            ?? throw new InvalidOperationException("Current directory root could not be resolved.");
        return root + new string('r', length - root.Length);
    }
}
