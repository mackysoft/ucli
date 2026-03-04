using MackySoft.Tests;
using MackySoft.Ucli.Index;

namespace MackySoft.Ucli.Tests.Index;

public sealed class IndexInputSnapshotHasherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryCompute_ReturnsNull_WhenRequiredInputsAreMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("index-hasher", "missing-inputs");
        var hasher = new IndexInputSnapshotHasher();
        scope.CreateDirectory("Assets");
        scope.CreateDirectory("Packages");

        var snapshot = await hasher.TryCompute(scope.FullPath, CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryCompute_ReturnsSnapshot_WhenRequiredInputsExist ()
    {
        using var scope = TestDirectories.CreateTempScope("index-hasher", "success");
        PrepareRequiredInputs(scope);
        var hasher = new IndexInputSnapshotHasher();

        var snapshot = await hasher.TryCompute(scope.FullPath, CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.False(string.IsNullOrWhiteSpace(snapshot!.ScriptAssembliesHash));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.PackagesManifestHash));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.PackagesLockHash));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.AssemblyDefinitionHash));
        Assert.False(string.IsNullOrWhiteSpace(snapshot.CombinedHash));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryCompute_ReturnsDifferentCombinedHash_WhenInputChanges ()
    {
        using var scope = TestDirectories.CreateTempScope("index-hasher", "change-detection");
        PrepareRequiredInputs(scope);
        var hasher = new IndexInputSnapshotHasher();

        var before = await hasher.TryCompute(scope.FullPath, CancellationToken.None);
        scope.WriteFile(Path.Combine("Library", "ScriptAssemblies", "Assembly-CSharp.dll"), "updated");
        var after = await hasher.TryCompute(scope.FullPath, CancellationToken.None);

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.NotEqual(before!.CombinedHash, after!.CombinedHash);
    }

    private static void PrepareRequiredInputs (TestDirectoryScope scope)
    {
        scope.CreateDirectory(Path.Combine("Library", "ScriptAssemblies"));
        scope.CreateDirectory("Assets");
        scope.CreateDirectory("Packages");
        scope.WriteFile(Path.Combine("Library", "ScriptAssemblies", "Assembly-CSharp.dll"), "initial");
        scope.WriteFile(Path.Combine("Packages", "manifest.json"), "{ \"dependencies\": {} }");
        scope.WriteFile(Path.Combine("Packages", "packages-lock.json"), "{ \"dependencies\": {} }");
    }
}