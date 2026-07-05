using System.Runtime.InteropServices;
using MackySoft.Ucli.Infrastructure.Project;

namespace MackySoft.Ucli.Infrastructure.Tests.Project;

public sealed class UnityProjectFingerprintCalculatorContractTests
{
    private static readonly FingerprintInputCase[] NullOrWhitespaceInputCases =
    [
        new(null, "/tmp/unity"),
        new(string.Empty, "/tmp/unity"),
        new(" ", "/tmp/unity"),
        new("/tmp/repo", null),
        new("/tmp/repo", string.Empty),
        new("/tmp/repo", " "),
    ];

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

    [Fact]
    [Trait("Size", "Small")]
    public void Create_OnUnix_DistinguishesBackslashCharacterFromDirectorySeparator ()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Repo"));
        var projectRootWithBackslashCharacter = Path.Combine(storageRoot, @"foo\bar");
        var projectRootWithDirectorySeparator = Path.Combine(storageRoot, "foo/bar");

        var backslashFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectRootWithBackslashCharacter);
        var separatorFingerprint = UnityProjectFingerprintCalculator.Create(storageRoot, projectRootWithDirectorySeparator);

        Assert.NotEqual(backslashFingerprint, separatorFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenInputIsNullOrWhitespace_ThrowsArgumentException ()
    {
        foreach (var testCase in NullOrWhitespaceInputCases)
        {
            Assert.ThrowsAny<ArgumentException>(() =>
            {
                UnityProjectFingerprintCalculator.Create(testCase.StorageRoot!, testCase.UnityProjectRoot!);
            });
        }
    }

    private sealed record FingerprintInputCase (
        string? StorageRoot,
        string? UnityProjectRoot);
}
