using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Project;

namespace MackySoft.Ucli.Infrastructure.Tests.Project;

public sealed class UnityProjectFingerprintCalculatorContractTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithEquivalentPathRepresentations_ReturnsSameFingerprint ()
    {
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Repo"));
        var unityProjectRoot = Path.Combine(storageRoot, "UnityProject");
        var unityProjectRootWithTrailingSeparator = unityProjectRoot + Path.DirectorySeparatorChar;

        var primary = UnityProjectFingerprintCalculator.Create(
            AbsolutePath.Parse(storageRoot),
            AbsolutePath.Parse(unityProjectRoot));
        var secondary = UnityProjectFingerprintCalculator.Create(
            AbsolutePath.Parse(storageRoot),
            AbsolutePath.Parse(unityProjectRootWithTrailingSeparator));

        Assert.Equal(primary, secondary);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WithDifferentUnityProjectsUnderSameStorageRoot_ReturnsDifferentFingerprints ()
    {
        var storageRoot = Path.GetFullPath(Path.Combine(".", "sandbox", "Repo"));
        var primaryProjectRoot = Path.Combine(storageRoot, "UnityProjectA");
        var secondaryProjectRoot = Path.Combine(storageRoot, "Packages", "UnityProjectB");

        var guardedStorageRoot = AbsolutePath.Parse(storageRoot);
        var primary = UnityProjectFingerprintCalculator.Create(
            guardedStorageRoot,
            AbsolutePath.Parse(primaryProjectRoot));
        var secondary = UnityProjectFingerprintCalculator.Create(
            guardedStorageRoot,
            AbsolutePath.Parse(secondaryProjectRoot));

        Assert.NotEqual(primary, secondary);
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("windows")]
    public void Create_OnWindows_WithCaseVariantPaths_ReturnsSameFingerprint ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var storageRoot = AbsolutePath.Parse(
            Path.GetFullPath(Path.Combine(".", "sandbox", "Repository")));
        var projectRoot = AbsolutePath.Parse(
            Path.Combine(storageRoot.Value, "UnityProject"));
        var storageRootCaseVariant = AbsolutePath.Parse(SwapLetterCase(storageRoot.Value));
        var projectRootCaseVariant = AbsolutePath.Parse(SwapLetterCase(projectRoot.Value));

        var primary = UnityProjectFingerprintCalculator.Create(storageRoot, projectRoot);
        var secondary = UnityProjectFingerprintCalculator.Create(
            storageRootCaseVariant,
            projectRootCaseVariant);

        Assert.Equal(storageRoot, storageRootCaseVariant);
        Assert.Equal(projectRoot, projectRootCaseVariant);
        Assert.NotEqual(storageRoot.Value, storageRootCaseVariant.Value);
        Assert.NotEqual(projectRoot.Value, projectRootCaseVariant.Value);
        Assert.Equal(primary, secondary);
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

        var guardedStorageRoot = AbsolutePath.Parse(storageRoot);
        var backslashFingerprint = UnityProjectFingerprintCalculator.Create(
            guardedStorageRoot,
            AbsolutePath.Parse(projectRootWithBackslashCharacter));
        var separatorFingerprint = UnityProjectFingerprintCalculator.Create(
            guardedStorageRoot,
            AbsolutePath.Parse(projectRootWithDirectorySeparator));

        Assert.NotEqual(backslashFingerprint, separatorFingerprint);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Create_WhenInputIsNull_ThrowsArgumentNullException ()
    {
        var path = AbsolutePath.Parse(Path.GetFullPath(Path.Combine(".", "sandbox", "Repo")));

        var storageRootException = Assert.Throws<ArgumentNullException>(
            () => UnityProjectFingerprintCalculator.Create(null!, path));
        var projectRootException = Assert.Throws<ArgumentNullException>(
            () => UnityProjectFingerprintCalculator.Create(path, null!));

        Assert.Equal("storageRoot", storageRootException.ParamName);
        Assert.Equal("unityProjectRoot", projectRootException.ParamName);
    }

    private static string SwapLetterCase (string value)
    {
        var characters = value.ToCharArray();
        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];
            if (char.IsUpper(character))
            {
                characters[index] = char.ToLowerInvariant(character);
            }
            else if (char.IsLower(character))
            {
                characters[index] = char.ToUpperInvariant(character);
            }
        }

        return new string(characters);
    }
}
