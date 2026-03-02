using MackySoft.Ucli.Contracts.Paths;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.UnityProject;
using MackySoft.Ucli.UnityProject.Resolution;

namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Resolves test-run configuration by merging profile values and resolving Unity runtime dependencies. </summary>
internal sealed class TestRunConfigurationResolver : ITestRunConfigurationResolver
{
    private const int MinTimeoutSeconds = 1;

    private const int MaxTimeoutSeconds = 86400;

    private readonly ITestRunProfileLoader profileLoader;

    private readonly IUnityProjectResolver unityProjectResolver;

    private readonly IUnityVersionResolver unityVersionResolver;

    private readonly IUnityEditorPathResolver unityEditorPathResolver;

    /// <summary> Initializes a new instance of the <see cref="TestRunConfigurationResolver" /> class. </summary>
    /// <param name="profileLoader"> The test-run profile loader dependency. </param>
    /// <param name="unityProjectResolver"> The Unity project resolver dependency. </param>
    /// <param name="unityVersionResolver"> The Unity version resolver dependency. </param>
    /// <param name="unityEditorPathResolver"> The Unity editor path resolver dependency. </param>
    public TestRunConfigurationResolver (
        ITestRunProfileLoader profileLoader,
        IUnityProjectResolver unityProjectResolver,
        IUnityVersionResolver unityVersionResolver,
        IUnityEditorPathResolver unityEditorPathResolver)
    {
        this.profileLoader = profileLoader ?? throw new ArgumentNullException(nameof(profileLoader));
        this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
        this.unityVersionResolver = unityVersionResolver ?? throw new ArgumentNullException(nameof(unityVersionResolver));
        this.unityEditorPathResolver = unityEditorPathResolver ?? throw new ArgumentNullException(nameof(unityEditorPathResolver));
    }

    /// <summary> Resolves one test-run configuration from command input and optional profile values. </summary>
    /// <param name="input"> The raw command input values. </param>
    /// <returns> The configuration resolution result. </returns>
    public TestRunConfigurationResolutionResult Resolve (TestRunCommandInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        TestRunProfile? profile = null;
        if (!string.IsNullOrWhiteSpace(input.ProfilePath))
        {
            var profileLoadResult = profileLoader.Load(input.ProfilePath!);
            if (!profileLoadResult.IsSuccess)
            {
                return TestRunConfigurationResolutionResult.Failure([profileLoadResult.Error!]);
            }

            profile = profileLoadResult.Profile;
        }

        MergedTestRunConfiguration mergedConfiguration;
        try
        {
            mergedConfiguration = TestRunConfigurationMerger.Merge(input, profile);
        }
        catch (Exception exception) when (PathFormatExceptionClassifier.IsPathFormatException(exception))
        {
            return TestRunConfigurationResolutionResult.Failure(
            [
                ExecutionError.InvalidArgument($"Path value is invalid. {exception.Message}"),
            ]);
        }

        var validationErrors = ValidateMergedConfiguration(mergedConfiguration);
        if (validationErrors.Count > 0)
        {
            return TestRunConfigurationResolutionResult.Failure(validationErrors);
        }

        var unityProjectResolutionResult = unityProjectResolver.Resolve(mergedConfiguration.ProjectPath);
        if (!unityProjectResolutionResult.IsSuccess)
        {
            return TestRunConfigurationResolutionResult.Failure([unityProjectResolutionResult.Error!]);
        }

        var unityProject = unityProjectResolutionResult.Context!;
        var unityVersionResolutionResult = unityVersionResolver.Resolve(
            unityProject.UnityProjectRoot,
            mergedConfiguration.UnityVersion);
        if (!unityVersionResolutionResult.IsSuccess)
        {
            return TestRunConfigurationResolutionResult.Failure([unityVersionResolutionResult.Error!]);
        }

        var unityEditorPathResolutionResult = unityEditorPathResolver.Resolve(
            unityVersionResolutionResult.UnityVersion!,
            mergedConfiguration.UnityEditorPath);
        if (!unityEditorPathResolutionResult.IsSuccess)
        {
            return TestRunConfigurationResolutionResult.Failure([unityEditorPathResolutionResult.Error!]);
        }

        var resolvedConfiguration = new ResolvedTestRunConfiguration(
            UnityProject: unityProject,
            Mode: mergedConfiguration.Mode,
            UnityVersion: unityVersionResolutionResult.UnityVersion!,
            UnityEditorPath: unityEditorPathResolutionResult.UnityEditorPath!,
            TestPlatform: mergedConfiguration.TestPlatform,
            RawTestPlatform: mergedConfiguration.RawTestPlatform,
            BuildTarget: mergedConfiguration.BuildTarget,
            TestFilter: mergedConfiguration.TestFilter,
            TestCategories: mergedConfiguration.TestCategories,
            AssemblyNames: mergedConfiguration.AssemblyNames,
            TestSettingsPath: mergedConfiguration.TestSettingsPath,
            TimeoutSeconds: mergedConfiguration.TimeoutSeconds);
        return TestRunConfigurationResolutionResult.Success(resolvedConfiguration);
    }

    /// <summary> Validates merged configuration values before project and editor resolution. </summary>
    /// <param name="configuration"> The merged configuration values. </param>
    /// <returns> The structured validation errors. </returns>
    private static IReadOnlyList<ExecutionError> ValidateMergedConfiguration (MergedTestRunConfiguration configuration)
    {
        var errors = new List<ExecutionError>();

        if (configuration.TestPlatform == TestRunPlatform.Unknown)
        {
            errors.Add(ExecutionError.InvalidArgument(
                $"testPlatform must be editmode or playmode. Actual: {configuration.RawTestPlatform}"));
        }

        if (configuration.TestPlatform == TestRunPlatform.EditMode
            && !string.IsNullOrWhiteSpace(configuration.BuildTarget))
        {
            errors.Add(ExecutionError.InvalidArgument(
                "buildTarget is not allowed when testPlatform=editmode."));
        }

        if (configuration.TimeoutSeconds < MinTimeoutSeconds || configuration.TimeoutSeconds > MaxTimeoutSeconds)
        {
            errors.Add(ExecutionError.InvalidArgument(
                $"timeoutSeconds must be in range {MinTimeoutSeconds}..{MaxTimeoutSeconds}. Actual: {configuration.TimeoutSeconds}"));
        }

        if (!string.IsNullOrWhiteSpace(configuration.TestSettingsPath) && !File.Exists(configuration.TestSettingsPath))
        {
            errors.Add(ExecutionError.InvalidArgument(
                $"testSettingsPath does not exist: {configuration.TestSettingsPath}"));
        }

        return errors;
    }
}