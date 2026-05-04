using MackySoft.Ucli.Skills.Generation;

namespace MackySoft.Ucli.Skills.Tests;

internal static class SkillTestData
{
    internal static readonly string[] ExpectedSkillNames =
    [
        "ucli-plan-apply",
        "ucli-read-project",
        "ucli-troubleshoot",
        "ucli-verify-changes",
    ];

    internal static string GetDefinitionsRoot ()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Ucli.Skills", "SkillDefinitions");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate src/Ucli.Skills/SkillDefinitions from the test output directory.");
    }

    internal static async Task<IReadOnlyList<CanonicalSkillPackage>> GenerateOfficialPackagesAsync ()
    {
        var service = new SkillPackageGenerationService();
        var result = await service.GenerateAllAsync(GetDefinitionsRoot(), CancellationToken.None);
        Assert.True(result.IsSuccess, result.Failure?.Message);
        return result.Value!;
    }
}
