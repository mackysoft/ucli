using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.TestRun.Execution;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests;

public sealed class UnityCommandBuilderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArguments_WithMinimumEditModeConfiguration_ReturnsRequiredArguments ()
    {
        var configuration = CreateConfiguration(
            testPlatform: TestRunPlatform.EditMode,
            buildTarget: null,
            testFilter: null,
            testCategories: [],
            assemblyNames: [],
            testSettingsPath: null);
        var artifactPaths = CreateArtifactPaths();
        var builder = new UnityCommandBuilder();

        var arguments = builder.BuildArguments(configuration, artifactPaths);

        Assert.Equal(
            [
                "-batchmode",
                "-nographics",
                "-projectPath",
                configuration.UnityProject.UnityProjectRoot,
                "-runTests",
                "-testPlatform",
                "EditMode",
                "-testResults",
                artifactPaths.ResultsXmlPath,
                "-logFile",
                artifactPaths.EditorLogPath,
            ],
            arguments);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArguments_WithPlayModeAndOptionalValues_IncludesOptions ()
    {
        var configuration = CreateConfiguration(
            testPlatform: TestRunPlatform.PlayMode,
            buildTarget: "StandaloneWindows64",
            testFilter: "Category=Smoke",
            testCategories: ["smoke", "quick"],
            assemblyNames: ["Game.Tests", "Game.MoreTests"],
            testSettingsPath: Path.GetFullPath("./ProjectSettings/TestSettings.json"));
        var artifactPaths = CreateArtifactPaths();
        var builder = new UnityCommandBuilder();

        var arguments = builder.BuildArguments(configuration, artifactPaths);

        Assert.Equal("PlayMode", GetOptionValue(arguments, "-testPlatform"));
        Assert.Equal("StandaloneWindows64", GetOptionValue(arguments, "-buildTarget"));
        Assert.Equal("Category=Smoke", GetOptionValue(arguments, "-testFilter"));
        Assert.Equal("smoke,quick", GetOptionValue(arguments, "-testCategory"));
        Assert.Equal("Game.Tests,Game.MoreTests", GetOptionValue(arguments, "-assemblyNames"));
        Assert.Equal(configuration.TestSettingsPath, GetOptionValue(arguments, "-testSettingsFile"));
    }

    private static ResolvedTestRunConfiguration CreateConfiguration (
        TestRunPlatform testPlatform,
        string? buildTarget,
        string? testFilter,
        string[] testCategories,
        string[] assemblyNames,
        string? testSettingsPath)
    {
        var projectPath = Path.GetFullPath("./UnityProject");
        return new ResolvedTestRunConfiguration(
            UnityProject: new ResolvedUnityProjectContext(
                UnityProjectRoot: projectPath,
                RepositoryRoot: projectPath,
                ProjectFingerprint: "fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            Mode: "oneshot",
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: Path.GetFullPath("./Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: testPlatform,
            RawTestPlatform: testPlatform == TestRunPlatform.PlayMode ? "playmode" : "editmode",
            BuildTarget: buildTarget,
            TestFilter: testFilter,
            TestCategories: testCategories,
            AssemblyNames: assemblyNames,
            TestSettingsPath: testSettingsPath,
            TimeoutSeconds: 1800);
    }

    private static ArtifactPaths CreateArtifactPaths ()
    {
        var artifactsDir = Path.GetFullPath("./.ucli/local/fingerprints/fingerprint/artifacts/test/20260301_120000Z_abcd1234");
        return new ArtifactPaths(
            MetaJsonPath: Path.Combine(artifactsDir, "meta.json"),
            ResultsXmlPath: Path.Combine(artifactsDir, "results.xml"),
            EditorLogPath: Path.Combine(artifactsDir, "editor.log"),
            ResultsJsonPath: Path.Combine(artifactsDir, "results.json"),
            SummaryJsonPath: Path.Combine(artifactsDir, "summary.json"));
    }

    private static string GetOptionValue (IReadOnlyList<string> arguments, string option)
    {
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (arguments[index] == option)
            {
                return arguments[index + 1];
            }
        }

        throw new InvalidOperationException($"Option not found: {option}");
    }
}