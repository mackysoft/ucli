using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Infrastructure.Paths;
using MackySoft.Ucli.Shared.Context.Project;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project.Resolution;
using MackySoft.Ucli.UnityIntegration.Resolution;

namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Resolves test-run configuration by merging profile values and resolving Unity runtime dependencies. </summary>
internal sealed class TestRunConfigurationResolver : ITestRunConfigurationResolver
{
    private const int MinTimeoutMilliseconds = 1;
    private const string DefaultProjectPath = ".";

    private readonly ITestRunProfileLoader profileLoader;

    private readonly IProjectPathInputResolver projectPathInputResolver;

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
        IProjectPathInputResolver projectPathInputResolver,
        IUnityProjectResolver unityProjectResolver,
        IUnityVersionResolver unityVersionResolver,
        IUnityEditorPathResolver unityEditorPathResolver)
    {
        this.profileLoader = profileLoader ?? throw new ArgumentNullException(nameof(profileLoader));
        this.projectPathInputResolver = projectPathInputResolver ?? throw new ArgumentNullException(nameof(projectPathInputResolver));
        this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
        this.unityVersionResolver = unityVersionResolver ?? throw new ArgumentNullException(nameof(unityVersionResolver));
        this.unityEditorPathResolver = unityEditorPathResolver ?? throw new ArgumentNullException(nameof(unityEditorPathResolver));
    }

    /// <summary> Resolves one test-run configuration from command input and optional profile values. </summary>
    /// <param name="input"> The interpreted command input values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the configuration resolution result. </returns>
    public async ValueTask<TestRunConfigurationResolutionResult> Resolve (
        TestRunConfigurationRequest input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        TestRunProfile? profile = null;
        if (!string.IsNullOrWhiteSpace(input.ProfilePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profileLoadResult = await profileLoader.Load(input.ProfilePath!, cancellationToken).ConfigureAwait(false);
            if (!profileLoadResult.IsSuccess)
            {
                return TestRunConfigurationResolutionResult.Failure([profileLoadResult.Error!]);
            }

            profile = profileLoadResult.Profile;
        }

        MergedTestRunConfiguration mergedConfiguration;
        try
        {
            var resolvedProjectPath = ResolveProjectPath(input, profile);
            mergedConfiguration = TestRunConfigurationMerger.Merge(input, profile, resolvedProjectPath);
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

        cancellationToken.ThrowIfCancellationRequested();
        var unityProjectResolutionResult = unityProjectResolver.Resolve(mergedConfiguration.ProjectPath);
        if (!unityProjectResolutionResult.IsSuccess)
        {
            return TestRunConfigurationResolutionResult.Failure([unityProjectResolutionResult.Error!]);
        }

        var unityProject = unityProjectResolutionResult.Context!;
        cancellationToken.ThrowIfCancellationRequested();
        var unityVersionResolutionResult = unityVersionResolver.Resolve(
            unityProject.UnityProjectRoot,
            mergedConfiguration.UnityVersion);
        if (!unityVersionResolutionResult.IsSuccess)
        {
            return TestRunConfigurationResolutionResult.Failure([unityVersionResolutionResult.Error!]);
        }

        cancellationToken.ThrowIfCancellationRequested();
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
            TestPlatform: mergedConfiguration.TestPlatform!.Value,
            TestFilter: mergedConfiguration.TestFilter,
            TestCategories: mergedConfiguration.TestCategories,
            AssemblyNames: mergedConfiguration.AssemblyNames,
            TestSettingsPath: mergedConfiguration.TestSettingsPath,
            TimeoutMilliseconds: mergedConfiguration.TimeoutMilliseconds);
        return TestRunConfigurationResolutionResult.Success(resolvedConfiguration);
    }

    /// <summary> Resolves the effective project-path input using command, environment, profile, and default precedence. </summary>
    /// <param name="input"> The interpreted command input values. </param>
    /// <param name="profile"> The optional loaded profile values. </param>
    /// <returns> The resolved project-path candidate. </returns>
    private string ResolveProjectPath (
        TestRunConfigurationRequest input,
        TestRunProfile? profile)
    {
        ArgumentNullException.ThrowIfNull(input);

        return projectPathInputResolver.Resolve(
                   input.ProjectPath,
                   profile?.ProjectPath ?? DefaultProjectPath)
               ?? DefaultProjectPath;
    }

    /// <summary> Validates merged configuration values before project and editor resolution. </summary>
    /// <param name="configuration"> The merged configuration values. </param>
    /// <returns> The structured validation errors. </returns>
    private static IReadOnlyList<ExecutionError> ValidateMergedConfiguration (MergedTestRunConfiguration configuration)
    {
        var errors = new List<ExecutionError>();

        if (!configuration.TestPlatform.HasValue)
        {
            errors.Add(ExecutionError.InvalidArgument(
                $"testPlatform must be editmode, playmode, or a Unity BuildTarget literal. Actual: {configuration.RawTestPlatform}"));
        }

        if (configuration.TimeoutMilliseconds.HasValue && configuration.TimeoutMilliseconds.Value < MinTimeoutMilliseconds)
        {
            errors.Add(ExecutionError.InvalidArgument(
                $"timeout must be in range {MinTimeoutMilliseconds}..{int.MaxValue}. Actual: {configuration.TimeoutMilliseconds.Value}"));
        }

        if (!string.IsNullOrWhiteSpace(configuration.TestSettingsPath) && !File.Exists(configuration.TestSettingsPath))
        {
            errors.Add(ExecutionError.InvalidArgument(
                $"testSettingsPath does not exist: {configuration.TestSettingsPath}"));
        }

        return errors;
    }
}
