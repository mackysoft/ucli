using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

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

    private readonly ITestRunPathNormalizer pathNormalizer;

    private readonly ITestRunPathExistenceProbe pathExistenceProbe;

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
        IUnityEditorPathResolver unityEditorPathResolver,
        ITestRunPathNormalizer pathNormalizer,
        ITestRunPathExistenceProbe pathExistenceProbe)
    {
        this.profileLoader = profileLoader ?? throw new ArgumentNullException(nameof(profileLoader));
        this.projectPathInputResolver = projectPathInputResolver ?? throw new ArgumentNullException(nameof(projectPathInputResolver));
        this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
        this.unityVersionResolver = unityVersionResolver ?? throw new ArgumentNullException(nameof(unityVersionResolver));
        this.unityEditorPathResolver = unityEditorPathResolver ?? throw new ArgumentNullException(nameof(unityEditorPathResolver));
        this.pathNormalizer = pathNormalizer ?? throw new ArgumentNullException(nameof(pathNormalizer));
        this.pathExistenceProbe = pathExistenceProbe ?? throw new ArgumentNullException(nameof(pathExistenceProbe));
    }

    /// <summary> Resolves one test-run configuration from command input and optional profile values. </summary>
    /// <param name="input"> The interpreted command input values. </param>
    /// <param name="cancellationToken"> A cancellation token propagated by caller. </param>
    /// <returns> A task that resolves to the configuration resolution result. </returns>
    public async ValueTask<TestRunConfigurationResolutionResult> ResolveAsync (
        TestRunConfigurationRequest input,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(input);

        TestRunProfile? profile = null;
        if (!string.IsNullOrWhiteSpace(input.ProfilePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var profileLoadResult = await profileLoader.LoadAsync(input.ProfilePath!, cancellationToken).ConfigureAwait(false);
            if (!profileLoadResult.IsSuccess)
            {
                return TestRunConfigurationResolutionResult.Failure([profileLoadResult.Error!]);
            }

            profile = profileLoadResult.Profile;
        }

        var resolvedProjectPath = ResolveProjectPath(input, profile);
        var mergedConfiguration = TestRunConfigurationMerger.Merge(input, profile, resolvedProjectPath);
        var validationErrors = ValidateMergedConfigurationValues(mergedConfiguration);
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
        var testSettingsPathNormalizationError = NormalizeTestSettingsPath(
            ref mergedConfiguration,
            unityProject.RepositoryRoot,
            pathNormalizer);
        if (testSettingsPathNormalizationError is not null)
        {
            return TestRunConfigurationResolutionResult.Failure([testSettingsPathNormalizationError]);
        }

        var testSettingsPathExistenceError = ValidateTestSettingsPath(mergedConfiguration, pathExistenceProbe);
        if (testSettingsPathExistenceError is not null)
        {
            return TestRunConfigurationResolutionResult.Failure([testSettingsPathExistenceError]);
        }

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

    private static ExecutionError? NormalizeTestSettingsPath (
        ref MergedTestRunConfiguration configuration,
        string repositoryRoot,
        ITestRunPathNormalizer pathNormalizer)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(pathNormalizer);

        if (configuration.TestSettingsPath is null)
        {
            return null;
        }

        var pathNormalizationResult = pathNormalizer.TryNormalizeRepositoryPath(
            repositoryRoot,
            configuration.TestSettingsPath);
        if (pathNormalizationResult.IsSuccess)
        {
            configuration = configuration with
            {
                TestSettingsPath = pathNormalizationResult.FullPath,
            };
            return null;
        }

        return ExecutionError.InvalidArgument(CreateTestSettingsPathErrorMessage(pathNormalizationResult));
    }

    /// <summary> Validates merged configuration values before project and editor resolution. </summary>
    /// <param name="configuration"> The merged configuration values. </param>
    /// <returns> The structured validation errors. </returns>
    private static IReadOnlyList<ExecutionError> ValidateMergedConfigurationValues (MergedTestRunConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

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

        return errors;
    }

    private static string CreateTestSettingsPathErrorMessage (TestRunPathNormalizationResult pathNormalizationResult)
    {
        if (pathNormalizationResult.IsSuccess)
        {
            throw new ArgumentException("Successful path normalization result does not have an error message.", nameof(pathNormalizationResult));
        }

        var reason = pathNormalizationResult.FailureKind switch
        {
            TestRunPathNormalizationFailureKind.EmptyPath => "Path value is empty.",
            TestRunPathNormalizationFailureKind.InvalidFormat => "Path format is invalid.",
            TestRunPathNormalizationFailureKind.OutsideRepositoryRoot => "Path must be under the repository root.",
            _ => "Path is invalid.",
        };
        return $"testSettingsPath is invalid: {reason}";
    }

    private static ExecutionError? ValidateTestSettingsPath (
        MergedTestRunConfiguration configuration,
        ITestRunPathExistenceProbe pathExistenceProbe)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(pathExistenceProbe);

        if (!string.IsNullOrWhiteSpace(configuration.TestSettingsPath) && !pathExistenceProbe.FileExists(configuration.TestSettingsPath))
        {
            return ExecutionError.InvalidArgument($"testSettingsPath does not exist: {configuration.TestSettingsPath}");
        }

        return null;
    }
}
