using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Infrastructure.Index;

namespace MackySoft.Ucli.Infrastructure.Tests.Index;

public sealed class FileSystemIndexInputFingerprintCalculatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryComputeCore_ReturnsSameCombinedHash_WhenOnlyAssetContentChanges ()
    {
        using var scope = TestDirectories.CreateTempScope("contracts-index-fingerprint", "core-asset-content");
        PrepareRequiredInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();

        var before = await calculator.TryComputeCore(scope.FullPath, CancellationToken.None);
        Assert.NotNull(before);

        scope.WriteFile(Path.Combine("Assets", "Data", "Spawner.asset"), "changed");

        var after = await calculator.TryComputeCore(scope.FullPath, CancellationToken.None);
        Assert.NotNull(after);

        Assert.Equal(before!.ScriptAssembliesHash, after!.ScriptAssembliesHash);
        Assert.Equal(before.PackagesManifestHash, after.PackagesManifestHash);
        Assert.Equal(before.PackagesLockHash, after.PackagesLockHash);
        Assert.Equal(before.AssemblyDefinitionHash, after.AssemblyDefinitionHash);
        Assert.Equal(before.CombinedHash, after.CombinedHash);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryCompute_ReturnsDifferentAssetHashes_WhenAssetContentChanges ()
    {
        using var scope = TestDirectories.CreateTempScope("contracts-index-fingerprint", "asset-content");
        PrepareRequiredInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();

        var before = await calculator.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(before);

        scope.WriteFile(Path.Combine("Assets", "Data", "Spawner.asset"), "changed");

        var after = await calculator.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(after);

        Assert.NotEqual(before!.AssetsContentHash, after!.AssetsContentHash);
        Assert.NotEqual(before.AssetSearchHash, after.AssetSearchHash);
        Assert.NotEqual(before.GuidPathHash, after.GuidPathHash);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("add")]
    [InlineData("delete")]
    [InlineData("rename")]
    [InlineData("move")]
    public async Task TryCompute_ReturnsDifferentAssetHashes_WhenAssetTopologyChanges (string changeKind)
    {
        using var scope = TestDirectories.CreateTempScope("contracts-index-fingerprint", changeKind);
        PrepareRequiredInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();

        var before = await calculator.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(before);

        switch (changeKind)
        {
            case "add":
                scope.WriteFile(Path.Combine("Assets", "Data", "Added.asset"), "new");
                scope.WriteFile(Path.Combine("Assets", "Data", "Added.asset.meta"), "guid: added");
                break;

            case "delete":
                File.Delete(Path.Combine(scope.FullPath, "Assets", "Data", "Spawner.asset"));
                File.Delete(Path.Combine(scope.FullPath, "Assets", "Data", "Spawner.asset.meta"));
                break;

            case "rename":
                File.Move(
                    Path.Combine(scope.FullPath, "Assets", "Data", "Spawner.asset"),
                    Path.Combine(scope.FullPath, "Assets", "Data", "SpawnerRenamed.asset"));
                File.Move(
                    Path.Combine(scope.FullPath, "Assets", "Data", "Spawner.asset.meta"),
                    Path.Combine(scope.FullPath, "Assets", "Data", "SpawnerRenamed.asset.meta"));
                break;

            case "move":
                Directory.CreateDirectory(Path.Combine(scope.FullPath, "Assets", "Moved"));
                File.Move(
                    Path.Combine(scope.FullPath, "Assets", "Data", "Spawner.asset"),
                    Path.Combine(scope.FullPath, "Assets", "Moved", "Spawner.asset"));
                File.Move(
                    Path.Combine(scope.FullPath, "Assets", "Data", "Spawner.asset.meta"),
                    Path.Combine(scope.FullPath, "Assets", "Moved", "Spawner.asset.meta"));
                break;

            default:
                throw new InvalidOperationException($"Unsupported change kind: {changeKind}");
        }

        var after = await calculator.TryCompute(scope.FullPath, CancellationToken.None);
        Assert.NotNull(after);

        Assert.NotEqual(before!.AssetsContentHash, after!.AssetsContentHash);
        Assert.NotEqual(before.AssetSearchHash, after.AssetSearchHash);
        Assert.NotEqual(before.GuidPathHash, after.GuidPathHash);
    }

    private static void PrepareRequiredInputs (TestDirectoryScope scope)
    {
        scope.CreateDirectory(Path.Combine("Library", "ScriptAssemblies"));
        scope.CreateDirectory(Path.Combine("Assets", "Data"));
        scope.CreateDirectory("Packages");
        scope.WriteFile(Path.Combine("Library", "ScriptAssemblies", "Assembly-CSharp.dll"), "initial");
        scope.WriteFile(Path.Combine("Packages", "manifest.json"), "{ \"dependencies\": {} }");
        scope.WriteFile(Path.Combine("Packages", "packages-lock.json"), "{ \"dependencies\": {} }");
        scope.WriteFile(Path.Combine("Assets", "Data", "Spawner.asset"), "initial");
        scope.WriteFile(Path.Combine("Assets", "Data", "Spawner.asset.meta"), "guid: initial");
    }
}
