using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Index;

namespace MackySoft.Ucli.Infrastructure.Tests.Index.Inputs;

public sealed class FileSystemIndexInputFingerprintCalculatorSmokeTests
{
    [Fact]
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
    public async Task TryCompute_ReturnsSnapshot_WhenRequiredInputsExist ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-index-fingerprint", "success");
        UnityIndexInputTestFactory.WriteRequiredCoreInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();

        var snapshot = await calculator.TryComputeAsync(scope.FullPath, CancellationToken.None);

        Assert.NotNull(snapshot);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryCompute_ReturnsDifferentCombinedHash_WhenInputChanges ()
    {
        using var scope = TestDirectories.CreateTempScope("infrastructure-index-fingerprint", "change-detection");
        UnityIndexInputTestFactory.WriteRequiredCoreInputs(scope);
        var calculator = new FileSystemIndexInputFingerprintCalculator();

        var before = await calculator.TryComputeAsync(scope.FullPath, CancellationToken.None);
        UnityIndexInputTestFactory.WriteScriptAssembly(scope, "updated");
        var after = await calculator.TryComputeAsync(scope.FullPath, CancellationToken.None);

        Assert.NotNull(before);
        Assert.NotNull(after);
        Assert.NotEqual(before!.CombinedHash, after!.CombinedHash);
    }

}
