namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.Contracts.Project;

public sealed class UnityProjectFingerprintCalculatorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithEquivalentPathRepresentations_ReturnsSameFingerprint ()
    {
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Repo"));
        var unityProjectRoot = Path.Combine(storageRoot, "UnityProject");
        var unityProjectRootWithTrailingSeparator = unityProjectRoot + Path.DirectorySeparatorChar;

        var primary = UnityProjectFingerprintCalculator.Create(storageRoot, unityProjectRoot);
        var secondary = UnityProjectFingerprintCalculator.Create(storageRoot, unityProjectRootWithTrailingSeparator);

        Assert.Equal(primary, secondary);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithDifferentUnityProjectsUnderSameStorageRoot_ReturnsDifferentFingerprints ()
    {
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Repo"));
        var primaryProjectRoot = Path.Combine(storageRoot, "UnityProjectA");
        var secondaryProjectRoot = Path.Combine(storageRoot, "Packages", "UnityProjectB");

        var primary = UnityProjectFingerprintCalculator.Create(storageRoot, primaryProjectRoot);
        var secondary = UnityProjectFingerprintCalculator.Create(storageRoot, secondaryProjectRoot);

        Assert.NotEqual(primary, secondary);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(null, "/tmp/unity")]
    [InlineData("", "/tmp/unity")]
    [InlineData(" ", "/tmp/unity")]
    [InlineData("/tmp/repo", null)]
    [InlineData("/tmp/repo", "")]
    [InlineData("/tmp/repo", " ")]
    public void Create_WhenInputIsNullOrWhitespace_ThrowsArgumentException (
        string? storageRoot,
        string? unityProjectRoot)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            UnityProjectFingerprintCalculator.Create(storageRoot!, unityProjectRoot!);
        });
    }
}