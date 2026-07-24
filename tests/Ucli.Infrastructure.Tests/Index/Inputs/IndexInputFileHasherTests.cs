using System.Runtime.Versioning;
using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Infrastructure.Index;

namespace MackySoft.Ucli.Infrastructure.Tests.Index.Inputs;

public sealed class IndexInputFileHasherTests
{
    [Fact]
    [Trait("Size", "Medium")]
    [SupportedOSPlatform("windows")]
    public async Task TryHashDirectoryContent_OnWindows_WithCaseVariantPathsAndOrdering_ReturnsSameHash ()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using var scope = TestDirectories.CreateTempScope(
            "infrastructure-index-input-hasher",
            "windows-case-variant");
        var directoryPath = AbsolutePath.Parse(scope.CreateDirectory("Inputs"));
        var uppercaseFirstPath = scope.WriteFile(Path.Combine("Inputs", "A.asset"), "first");
        var lowercaseSecondPath = scope.WriteFile(Path.Combine("Inputs", "b.asset"), "second");

        var primary = await IndexInputFileHasher.TryHashDirectoryContentAsync(
            directoryPath,
            CancellationToken.None);

        RenameCaseOnly(uppercaseFirstPath, Path.Combine(directoryPath.Value, "a.asset"));
        RenameCaseOnly(lowercaseSecondPath, Path.Combine(directoryPath.Value, "B.asset"));
        var caseVariantDirectoryPath = AbsolutePath.Parse(SwapLetterCase(directoryPath.Value));
        var secondary = await IndexInputFileHasher.TryHashDirectoryContentAsync(
            caseVariantDirectoryPath,
            CancellationToken.None);

        Assert.Equal(directoryPath, caseVariantDirectoryPath);
        Assert.NotEqual(directoryPath.Value, caseVariantDirectoryPath.Value);
        Assert.NotNull(primary);
        Assert.Equal(primary, secondary);
    }

    private static void RenameCaseOnly (
        string sourcePath,
        string destinationPath)
    {
        var temporaryPath = sourcePath + ".case-rename";
        File.Move(sourcePath, temporaryPath);
        File.Move(temporaryPath, destinationPath);
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
