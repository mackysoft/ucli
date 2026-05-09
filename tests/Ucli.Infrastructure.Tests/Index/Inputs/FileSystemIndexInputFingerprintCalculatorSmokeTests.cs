using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Index;

namespace MackySoft.Ucli.Infrastructure.Tests.Index.Inputs;

public sealed class FileSystemIndexInputFingerprintCalculatorSmokeTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task TryCompute_ReturnsNull_WhenRequiredInputsAreMissing ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-index-fingerprint", "missing-inputs");
        var calculator = new FileSystemIndexInputFingerprintCalculator();
        scope.CreateDirectory("Assets");
        scope.CreateDirectory("Packages");

        var snapshot = await calculator.TryComputeAsync(scope.FullPath, CancellationToken.None);

        Assert.Null(snapshot);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task TryCompute_ReturnsSnapshot_WhenRequiredInputsExist ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-index-fingerprint", "success");
        PrepareRequiredInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();

        var snapshot = await calculator.TryComputeAsync(scope.FullPath, CancellationToken.None);

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
        using var scope = TestDirectories.CreateTempScope("infrastructure-index-fingerprint", "change-detection");
        PrepareRequiredInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();

        var before = await calculator.TryComputeAsync(scope.FullPath, CancellationToken.None);
        scope.WriteFile(Path.Combine("Library", "ScriptAssemblies", "Assembly-CSharp.dll"), "updated");
        var after = await calculator.TryComputeAsync(scope.FullPath, CancellationToken.None);

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
