using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Infrastructure.Tests.Storage;

public sealed class UcliStoragePathResolverIndexContractTests
{
    private static readonly Guid GenerationId = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexDirectory_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveIndexDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveReadIndexWriteLockPath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveReadIndexWriteLockPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexWriteLockFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveReadIndexCurrentGenerationPath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveReadIndexCurrentGenerationPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexCurrentGenerationFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveReadIndexGenerationDirectory_UsesEncodedNonEmptyGuid ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            GenerationId);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexGenerationsDirectoryName,
            StoragePathSegmentCodec.EncodeGuid(GenerationId, nameof(GenerationId)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveReadIndexGenerationDirectory_WithEmptyGuid_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => UcliStoragePathResolver.ResolveReadIndexGenerationDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            Guid.Empty));

        Assert.Equal("generationId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveReadIndexRetentionMarkerPath_UsesEncodedNonEmptyGuid ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveReadIndexRetentionMarkerPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            GenerationId);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexRetentionDirectoryName,
            StoragePathSegmentCodec.EncodeGuid(GenerationId, nameof(GenerationId)));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveReadIndexRetentionMarkerPath_WithEmptyGuid_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => UcliStoragePathResolver.ResolveReadIndexRetentionMarkerPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            Guid.Empty));

        Assert.Equal("generationId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexCatalogsDirectory_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveIndexCatalogsDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.CatalogsDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveTypesCatalogPath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveTypesCatalogPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.CatalogsDirectoryName,
            UcliStoragePathNames.TypesCatalogFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSchemasCatalogPath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveSchemasCatalogPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.CatalogsDirectoryName,
            UcliStoragePathNames.SchemasCatalogFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveOpsCatalogPath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveOpsCatalogPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            GenerationId);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexGenerationsDirectoryName,
            StoragePathSegmentCodec.EncodeGuid(GenerationId, nameof(GenerationId)),
            UcliStoragePathNames.OpsCatalogFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveOpsDescribePath_ReturnsProjectScopedPath ()
    {
        var opKey = Sha256Digest.Compute(Encoding.UTF8.GetBytes("ucli.go.describe"));

        var resolvedPath = UcliStoragePathResolver.ResolveOpsDescribePath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            opKey);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexOpsDirectoryName,
            "roar90ksilj7prugog0djb4cl5a60ojunlu3slgp8t2ai86bo61g"
                + UcliStoragePathNames.OpsDescribeFileExtension);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveOpsDescribePath_WithNullDigest_ThrowsArgumentNullException ()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => UcliStoragePathResolver.ResolveOpsDescribePath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            null!));

        Assert.Equal("opKey", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveAssetSearchLookupPath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveAssetSearchLookupPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            GenerationId);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexGenerationsDirectoryName,
            StoragePathSegmentCodec.EncodeGuid(GenerationId, nameof(GenerationId)),
            UcliStoragePathNames.AssetSearchLookupFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveGuidPathLookupPath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            GenerationId);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexGenerationsDirectoryName,
            StoragePathSegmentCodec.EncodeGuid(GenerationId, nameof(GenerationId)),
            UcliStoragePathNames.GuidPathLookupFileName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSceneTreeLiteLookupDirectory_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupDirectory(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexScenesDirectoryName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveSceneTreeLiteLookupPath_ReturnsHashedSceneScopedPath ()
    {
        const string scenePath = @"Assets\Scenes\Sample.unity";

        var resolvedPath = UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            scenePath);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexScenesDirectoryName,
            "19c9f3h4ql00sn8fio1ktqcekpqmtbeqg91en9he1baqcsfrordg"
                + UcliStoragePathNames.SceneTreeLiteLookupFileExtension);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ResolveIndexInputsManifestPath_ReturnsProjectScopedPath ()
    {
        var resolvedPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(
            UcliStoragePathResolverTestSupport.StorageRoot,
            UcliStoragePathResolverTestSupport.ProjectFingerprint,
            GenerationId);

        UcliStoragePathResolverTestSupport.AssertProjectPath(
            resolvedPath,
            UcliStoragePathNames.IndexDirectoryName,
            UcliStoragePathNames.ReadIndexGenerationsDirectoryName,
            StoragePathSegmentCodec.EncodeGuid(GenerationId, nameof(GenerationId)),
            UcliStoragePathNames.IndexInputsManifestFileName);
    }
}
