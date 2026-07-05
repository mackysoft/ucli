using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliStoragePathResolverIndexContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexDirectory_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveIndexDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexCatalogsDirectory_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveIndexCatalogsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.CatalogsDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTypesCatalogPath_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveTypesCatalogPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.CatalogsDirectoryName,
            UcliStoragePathNames.TypesCatalogFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSchemasCatalogPath_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveSchemasCatalogPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.CatalogsDirectoryName,
            UcliStoragePathNames.SchemasCatalogFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveOpsCatalogPath_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveOpsCatalogPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.CatalogsDirectoryName,
            UcliStoragePathNames.OpsCatalogFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveOpsDescribePath_ReturnsFingerprintScopedPath ()
    {
        var opKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes("ucli.go.describe"));

        var resolvedPath = UcliStoragePathResolver.ResolveOpsDescribePath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            opKey);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.CatalogsDirectoryName,
            UcliStoragePathNames.OpsDescribeDirectoryName,
            opKey + UcliStoragePathNames.OpsDescribeFileExtension);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexLookupsDirectory_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveIndexLookupsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.LookupsDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveAssetSearchLookupPath_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveAssetSearchLookupPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.LookupsDirectoryName,
            UcliStoragePathNames.AssetSearchLookupFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveGuidPathLookupPath_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.LookupsDirectoryName,
            UcliStoragePathNames.GuidPathLookupFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSceneTreeLiteLookupDirectory_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.LookupsDirectoryName,
            UcliStoragePathNames.SceneTreeLiteLookupDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSceneTreeLiteLookupPath_ReturnsHashedSceneScopedPath ()
    {
        const string scenePath = @"Assets\Scenes\Sample.unity";
        var expectedSceneKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes("Assets/Scenes/Sample.unity"));

        var resolvedPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            scenePath);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.LookupsDirectoryName,
            UcliStoragePathNames.SceneTreeLiteLookupDirectoryName,
            expectedSceneKey + UcliStoragePathNames.SceneTreeLiteLookupFileExtension);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexInputsManifestPath_ReturnsFingerprintScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertFingerprintPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.IndexInputsDirectoryName,
            UcliStoragePathNames.IndexInputsManifestFileName);
    }
}
