using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Index;

namespace MackySoft.Ucli.Infrastructure.Tests.Index.Inputs;

public sealed class FileSystemIndexInputFingerprintCalculatorTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryComputeCore_ReturnsSameCombinedHash_WhenOnlyAssetContentChanges ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-index-fingerprint", "core-asset-content");
        UnityIndexInputTestFactory.WriteRequiredInputsWithSampleAsset(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();
        var projectRoot = AbsolutePath.Parse(scope.FullPath);

        var before = await calculator.TryComputeCoreAsync(projectRoot, CancellationToken.None);
        Assert.NotNull(before);

        UnityIndexInputTestFactory.WriteSampleAsset(scope, "changed");

        var after = await calculator.TryComputeCoreAsync(projectRoot, CancellationToken.None);
        Assert.NotNull(after);

        Assert.Equal(before!.ScriptAssembliesHash, after!.ScriptAssembliesHash);
        Assert.Equal(before.PackagesManifestHash, after.PackagesManifestHash);
        Assert.Equal(before.PackagesLockHash, after.PackagesLockHash);
        Assert.Equal(before.AssemblyDefinitionHash, after.AssemblyDefinitionHash);
        Assert.Equal(before.CombinedHash, after.CombinedHash);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryCompute_ReturnsDifferentAssetHashes_WhenAssetContentChanges ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-index-fingerprint", "asset-content");
        UnityIndexInputTestFactory.WriteRequiredInputsWithSampleAsset(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();
        var projectRoot = AbsolutePath.Parse(scope.FullPath);

        var before = await calculator.TryComputeAsync(projectRoot, CancellationToken.None);
        Assert.NotNull(before);

        UnityIndexInputTestFactory.WriteSampleAsset(scope, "changed");

        var after = await calculator.TryComputeAsync(projectRoot, CancellationToken.None);
        Assert.NotNull(after);

        Assert.NotEqual(before!.AssetsContentHash, after!.AssetsContentHash);
        Assert.NotEqual(before.AssetSearchHash, after.AssetSearchHash);
        Assert.NotEqual(before.GuidPathHash, after.GuidPathHash);
    }

    [Theory]
    [Trait("Size", "Medium")]
    [InlineData("add")]
    [InlineData("delete")]
    [InlineData("rename")]
    [InlineData("move")]
    public async Task TryCompute_ReturnsDifferentAssetHashes_WhenAssetTopologyChanges (string changeKind)
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-index-fingerprint", changeKind);
        UnityIndexInputTestFactory.WriteRequiredInputsWithSampleAsset(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();
        var projectRoot = AbsolutePath.Parse(scope.FullPath);

        var before = await calculator.TryComputeAsync(projectRoot, CancellationToken.None);
        Assert.NotNull(before);

        switch (changeKind)
        {
            case "add":
                UnityIndexInputTestFactory.WriteAssetWithMeta(
                    scope,
                    Path.Combine("Assets", "Data", "Added.asset"),
                    "new",
                    "guid: added");
                break;

            case "delete":
                File.Delete(scope.GetPath(UnityIndexInputTestFactory.SampleAssetPath));
                File.Delete(scope.GetPath(UnityIndexInputTestFactory.SampleAssetMetaPath));
                break;

            case "rename":
                File.Move(
                    scope.GetPath(UnityIndexInputTestFactory.SampleAssetPath),
                    Path.Combine(scope.FullPath, "Assets", "Data", "SpawnerRenamed.asset"));
                File.Move(
                    scope.GetPath(UnityIndexInputTestFactory.SampleAssetMetaPath),
                    Path.Combine(scope.FullPath, "Assets", "Data", "SpawnerRenamed.asset.meta"));
                break;

            case "move":
                Directory.CreateDirectory(Path.Combine(scope.FullPath, "Assets", "Moved"));
                File.Move(
                    scope.GetPath(UnityIndexInputTestFactory.SampleAssetPath),
                    Path.Combine(scope.FullPath, "Assets", "Moved", "Spawner.asset"));
                File.Move(
                    scope.GetPath(UnityIndexInputTestFactory.SampleAssetMetaPath),
                    Path.Combine(scope.FullPath, "Assets", "Moved", "Spawner.asset.meta"));
                break;

            default:
                throw new InvalidOperationException($"Unsupported change kind: {changeKind}");
        }

        var after = await calculator.TryComputeAsync(projectRoot, CancellationToken.None);
        Assert.NotNull(after);

        Assert.NotEqual(before!.AssetsContentHash, after!.AssetsContentHash);
        Assert.NotEqual(before.AssetSearchHash, after.AssetSearchHash);
        Assert.NotEqual(before.GuidPathHash, after.GuidPathHash);
    }

}
