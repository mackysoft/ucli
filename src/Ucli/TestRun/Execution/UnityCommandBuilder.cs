using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.TestRun.Artifacts;
using MackySoft.Ucli.TestRun.Configuration;

namespace MackySoft.Ucli.TestRun.Execution;

/// <summary> Implements Unity command argument building for test execution. </summary>
internal sealed class UnityCommandBuilder : IUnityCommandBuilder
{
    private const char MultiValueOptionSeparator = ';';

    /// <summary> Builds one Unity command argument list from resolved run configuration and artifact paths. </summary>
    /// <param name="configuration"> The resolved test-run configuration. </param>
    /// <param name="artifactPaths"> The run artifact paths. </param>
    /// <returns> The command argument list. </returns>
    public IReadOnlyList<string> BuildArguments (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(artifactPaths);
        if (!TestRunArtifactValidator.TryValidateOutputPaths(artifactPaths, out var artifactPathError))
        {
            throw new ArgumentException(artifactPathError!, nameof(artifactPaths));
        }

        var arguments = new List<string>
        {
            "-batchmode",
            "-nographics",
            "-projectPath",
            configuration.UnityProject.UnityProjectRoot,
            "-runTests",
            "-testPlatform",
            IpcTestRunPlatformCodec.ToUnityValue(configuration.TestPlatform),
        };

        if (configuration.TestPlatform == IpcTestRunPlatform.PlayMode
            && !string.IsNullOrWhiteSpace(configuration.BuildTarget))
        {
            arguments.Add("-buildTarget");
            arguments.Add(configuration.BuildTarget!);
        }

        if (configuration.AssemblyNames.Length > 0)
        {
            arguments.Add("-assemblyNames");
            arguments.Add(string.Join(MultiValueOptionSeparator, configuration.AssemblyNames));
        }

        if (!string.IsNullOrWhiteSpace(configuration.TestFilter))
        {
            arguments.Add("-testFilter");
            arguments.Add(configuration.TestFilter!);
        }

        if (configuration.TestCategories.Length > 0)
        {
            arguments.Add("-testCategory");
            arguments.Add(string.Join(MultiValueOptionSeparator, configuration.TestCategories));
        }

        if (!string.IsNullOrWhiteSpace(configuration.TestSettingsPath))
        {
            arguments.Add("-testSettingsFile");
            arguments.Add(configuration.TestSettingsPath!);
        }

        arguments.Add("-testResults");
        arguments.Add(artifactPaths.ResultsXmlPath);
        arguments.Add("-logFile");
        arguments.Add(artifactPaths.EditorLogPath);

        return arguments;
    }
}