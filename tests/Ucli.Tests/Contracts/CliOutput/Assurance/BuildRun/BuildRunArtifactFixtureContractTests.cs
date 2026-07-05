using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Tests.Helpers.Assurance.Build;

namespace MackySoft.Ucli.Tests;

public sealed class BuildRunArtifactFixtureContractTests
{
    private const string BuildArtifactFixtureRoot = "tests/Ucli.Tests/GoldenFiles/Json/BuildRunArtifacts";

    [Theory]
    [InlineData("success.json", "success")]
    [InlineData("build-report-failed.json", "failed")]
    [Trait("Size", "Medium")]
    public void BuildJsonFixtures_ProjectFieldsUsedByBuildRunPayloadGoldens (
        string fileName,
        string fixtureName)
    {
        var payloadBuild = BuildRunCliOutputContractTestSupport.ReadGoldenPayload(fileName).GetProperty("build");
        using var fixture = ReadBuildArtifactFixture(fixtureName, "build.json");
        var buildRoot = fixture.RootElement;

        AssertEquivalentJson(buildRoot.GetProperty("profile"), payloadBuild.GetProperty("profile"));
        AssertEquivalentJson(buildRoot.GetProperty("inputs"), payloadBuild.GetProperty("inputs"));
        AssertEquivalentJson(buildRoot.GetProperty("summary"), payloadBuild.GetProperty("summary"));
        AssertEquivalentJson(buildRoot.GetProperty("logs"), payloadBuild.GetProperty("logs"));
        AssertEquivalentJson(buildRoot.GetProperty("generations"), payloadBuild.GetProperty("generations"));
        Assert.Equal(
            buildRoot.GetProperty("runner").GetProperty("kind").GetString(),
            payloadBuild.GetProperty("runner").GetProperty("kind").GetString());
        Assert.Equal(
            buildRoot.GetProperty("runner").GetProperty("method").ValueKind,
            payloadBuild.GetProperty("runner").GetProperty("method").ValueKind);
        Assert.Equal(
            buildRoot.GetProperty("runnerResult").GetProperty("source").GetString(),
            payloadBuild.GetProperty("runnerResult").GetProperty("source").GetString());
        Assert.Equal(
            buildRoot.GetProperty("runnerResult").GetProperty("status").GetString(),
            payloadBuild.GetProperty("runnerResult").GetProperty("status").GetString());
        Assert.False(payloadBuild.TryGetProperty("buildTarget", out _));
        Assert.False(payloadBuild.TryGetProperty("scenes", out _));
        Assert.False(payloadBuild.TryGetProperty("options", out _));
    }

    [Theory]
    [InlineData("success.json", "success")]
    [InlineData("build-report-failed.json", "failed")]
    [Trait("Size", "Medium")]
    public void OutputManifestFixtures_StoreCanonicalManifestDigestUsedByBuildRunPayloadGoldens (
        string fileName,
        string fixtureName)
    {
        using var manifest = ReadBuildArtifactFixture(fixtureName, "output-manifest.json");
        var manifestRoot = manifest.RootElement;
        var payloadManifestDigest = BuildRunCliOutputContractTestSupport.ReadGoldenPayload(fileName)
            .GetProperty("build")
            .GetProperty("output")
            .GetProperty("manifestDigest")
            .GetString();

        var recalculatedDigest = new BuildOutputManifestJsonContractWriter()
            .CalculateManifestDigest(BuildOutputManifestJsonContractTestSupport.ReadContent(manifestRoot));

        Assert.Equal(recalculatedDigest, manifestRoot.GetProperty("manifestDigest").GetString());
        Assert.Equal(recalculatedDigest, payloadManifestDigest);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void BuildRunArtifactFixtures_DoNotExposeRemovedArtifactsOrSha256Prefixes ()
    {
        string[] removedNames =
        [
            "build-summary.json",
            "profile-snapshot.json",
            "lifecycle.json",
            "manifest.json",
        ];
        var fixtureRoot = TestRepositoryPaths.GetFullPath(BuildArtifactFixtureRoot);
        var fixtureFiles = Directory.EnumerateFiles(fixtureRoot, "*.json", SearchOption.AllDirectories).ToArray();
        var cliOutputFiles = Directory.EnumerateFiles(
            TestRepositoryPaths.GetFullPath(
                "tests",
                "Ucli.Tests",
                "GoldenFiles",
                "Json",
                "CliOutput",
                BuildRunCliOutputContractTestSupport.GoldenDirectory),
            "*.json",
            SearchOption.AllDirectories);

        foreach (var filePath in fixtureFiles)
        {
            Assert.DoesNotContain(Path.GetFileName(filePath), removedNames);
            var rawText = File.ReadAllText(filePath);
            Assert.DoesNotContain("sha256:", rawText, StringComparison.Ordinal);
            using var document = JsonDocument.Parse(rawText);
            AssertNoRemovedPathSegment(document.RootElement, removedNames);
        }

        foreach (var filePath in cliOutputFiles)
        {
            Assert.DoesNotContain("sha256:", File.ReadAllText(filePath), StringComparison.Ordinal);
        }
    }

    private static JsonDocument ReadBuildArtifactFixture (
        string fixtureName,
        string fileName)
    {
        return JsonDocument.Parse(File.ReadAllText(TestRepositoryPaths.GetFullPath(
            BuildArtifactFixtureRoot,
            fixtureName,
            fileName)));
    }

    private static void AssertEquivalentJson (
        JsonElement expected,
        JsonElement actual)
    {
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(actual));
    }

    private static void AssertNoRemovedPathSegment (
        JsonElement element,
        IReadOnlyCollection<string> removedNames)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
            {
                var segments = value.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
                foreach (var removedName in removedNames)
                {
                    Assert.DoesNotContain(removedName, segments);
                }
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                AssertNoRemovedPathSegment(property.Value, removedNames);
            }

            return;
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                AssertNoRemovedPathSegment(item, removedNames);
            }
        }
    }
}
