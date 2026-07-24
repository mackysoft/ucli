using System.Runtime.Versioning;
using MackySoft.FileSystem;
using MackySoft.Ucli.Infrastructure.Cryptography;

namespace MackySoft.Ucli.Infrastructure.Tests.Cryptography;

public sealed class DeterministicPathTextTests
{
    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("windows")]
    public void ForIdentity_OnWindows_CollapsesCaseVariants ()
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
        var relativeProjectPath = ContainedPath.Create(storageRoot, projectRoot).RelativePath;
        var relativeProjectPathCaseVariant = ContainedPath.Create(
            storageRootCaseVariant,
            projectRootCaseVariant).RelativePath;

        Assert.Equal(storageRoot, storageRootCaseVariant);
        Assert.NotEqual(storageRoot.Value, storageRootCaseVariant.Value);
        Assert.Equal(
            DeterministicPathText.ForIdentity(storageRoot),
            DeterministicPathText.ForIdentity(storageRootCaseVariant));
        Assert.Equal(
            DeterministicPathText.ForIdentity(relativeProjectPath),
            DeterministicPathText.ForIdentity(relativeProjectPathCaseVariant));
    }

    [Fact]
    [Trait("Size", "Small")]
    [SupportedOSPlatform("macos")]
    [SupportedOSPlatform("linux")]
    public void ForIdentity_OnUnix_PreservesCaseAndLiteralBackslash ()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var storageRoot = AbsolutePath.Parse(
            Path.GetFullPath(Path.Combine(".", "sandbox", "Repository")));
        var projectRoot = AbsolutePath.Parse(
            Path.Combine(storageRoot.Value, @"Case\Sensitive"));
        var caseVariantProjectRoot = AbsolutePath.Parse(
            Path.Combine(storageRoot.Value, @"case\sensitive"));
        var relativeProjectPath = ContainedPath.Create(storageRoot, projectRoot).RelativePath;

        Assert.Contains('\\', projectRoot.Value);
        Assert.Equal(projectRoot.Value, DeterministicPathText.ForIdentity(projectRoot));
        Assert.Equal(
            relativeProjectPath.Value,
            DeterministicPathText.ForIdentity(relativeProjectPath));
        Assert.NotEqual(
            DeterministicPathText.ForIdentity(projectRoot),
            DeterministicPathText.ForIdentity(caseVariantProjectRoot));
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
