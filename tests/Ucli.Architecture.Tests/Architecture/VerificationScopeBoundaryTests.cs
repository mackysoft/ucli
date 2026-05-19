namespace MackySoft.Ucli.Architecture.Tests.Architecture;

public sealed class VerificationScopeBoundaryTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Verify_scope_detector_tracks_application_project_changes ()
    {
        var sourceText = File.ReadAllText(ArchitectureTestRepository.ToFullPath("scripts/detect-verify-scopes.sh"));
        var applicationScopeOccurrences = sourceText.Split("src/Ucli.Application/*", StringSplitOptions.None).Length - 1;

        Assert.True(
            applicationScopeOccurrences >= 2,
            "Application project changes must trigger both .NET verification and CLI package verification scopes.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Verify_scope_detector_tracks_skill_distribution_changes ()
    {
        var sourceText = File.ReadAllText(ArchitectureTestRepository.ToFullPath("scripts/detect-verify-scopes.sh"));
        var requiredDotnetAndCliPackInputs = new[]
        {
            ".config/dotnet-tools.json",
            "skills/definitions/*",
            "skills/generated/*",
        };

        foreach (var input in requiredDotnetAndCliPackInputs)
        {
            var occurrences = sourceText.Split(input, StringSplitOptions.None).Length - 1;

            Assert.True(
                occurrences >= 2,
                $"{input} changes must trigger both .NET verification and CLI package verification scopes.");
        }
    }
}
