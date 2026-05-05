using MackySoft.Ucli.Application.Features.Testing.Run.Artifacts;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Features.Testing.Run.Execution;

/// <summary> Implements Unity command argument building for test execution. </summary>
internal sealed class UnityCommandBuilder : IUnityCommandBuilder
{
    private const char MultiValueOptionSeparator = ';';

    /// <inheritdoc />
    public IReadOnlyList<string> BuildArguments (
        ResolvedTestRunConfiguration configuration,
        ArtifactPaths artifactPaths)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(artifactPaths);
        if (!TestRunArtifactPathValidator.TryValidateOutputPaths(artifactPaths, out var artifactPathError))
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
            TestRunPlatformCodec.ToUnityValue(configuration.TestPlatform),
        };

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
