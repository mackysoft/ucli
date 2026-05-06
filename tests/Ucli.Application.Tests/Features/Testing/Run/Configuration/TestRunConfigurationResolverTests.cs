using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Application.Shared.Unity.Resolution;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunConfigurationResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WithCliOverridesProfileValues_ReturnsMergedCliValues ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "cli-overrides-profile");
        var testSettingsPath = scope.GetPath("ProjectSettings/TestSettings.json");

        var profile = new TestRunProfile
        {
            SchemaVersion = 1,
            ProjectPath = "./profile-project",
            UnityVersion = "6000.1.3f1",
            UnityEditorPath = "./profile-editor/Unity",
            TestPlatform = "playmode",
            TestFilter = "Category=Smoke",
            TestCategories = ["profile"],
            AssemblyNames = ["Profile.Tests"],
            TestSettingsPath = testSettingsPath,
            Timeout = 30,
        };

        var unityProject = CreateUnityProjectContext(scope, "cli-project");
        var profileLoader = new StubProfileLoader(TestRunProfileLoadResult.Success(profile));
        var unityProjectResolver = new StubUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var unityEditorPathResolver = new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity")));

        var resolver = new TestRunConfigurationResolver(
            profileLoader,
            new StubProjectPathInputResolver(static (commandOptionProjectPath, fallbackProjectPath) => commandOptionProjectPath ?? fallbackProjectPath),
            unityProjectResolver,
            unityVersionResolver,
            unityEditorPathResolver,
            new StubPathNormalizer(),
            new StubPathExistenceProbe(testSettingsPath));

        var input = new TestRunConfigurationRequest(
            ProjectPath: unityProject.UnityProjectRoot,
            ProfilePath: scope.GetPath("test.profile.json"),
            Mode: UnityExecutionMode.Oneshot,
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: "Name~Smoke",
            TestCategory: ["smoke", "quick"],
            AssemblyName: ["Cli.Tests"],
            TestSettingsPath: testSettingsPath,
            TimeoutMilliseconds: 120);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var configuration = Assert.IsType<ResolvedTestRunConfiguration>(result.Configuration);
        Assert.Equal(UnityExecutionMode.Oneshot, configuration.Mode);
        Assert.Equal(TestRunPlatform.EditMode, configuration.TestPlatform);
        Assert.Equal("Name~Smoke", configuration.TestFilter);
        Assert.Equal(["smoke", "quick"], configuration.TestCategories);
        Assert.Equal(["Cli.Tests"], configuration.AssemblyNames);
        Assert.Equal(testSettingsPath, configuration.TestSettingsPath);
        Assert.Equal(120, configuration.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenProfileListValuesContainWhitespaceAndDuplicates_TrimsAndDeduplicatesValues ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "profile-list-values");
        var profile = new TestRunProfile
        {
            SchemaVersion = 1,
            ProjectPath = "./profile-project",
            UnityVersion = null,
            UnityEditorPath = null,
            TestPlatform = "editmode",
            TestFilter = null,
            TestCategories = [" smoke ", "smoke", "", "nightly"],
            AssemblyNames = [" Game.Tests ", "Game.Tests", "Game.MoreTests"],
            TestSettingsPath = null,
            Timeout = 30,
        };

        var resolver = new TestRunConfigurationResolver(
            new StubProfileLoader(TestRunProfileLoadResult.Success(profile)),
            new StubProjectPathInputResolver(static (commandOptionProjectPath, fallbackProjectPath) => commandOptionProjectPath ?? fallbackProjectPath),
            new StubUnityProjectResolver(UnityProjectResolutionResult.Success(CreateUnityProjectContext(scope, "profile-project"))),
            new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity"))),
            new StubPathNormalizer(),
            new StubPathExistenceProbe());

        var input = new TestRunConfigurationRequest(
            ProjectPath: null,
            ProfilePath: scope.GetPath("test.profile.json"),
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: null,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutMilliseconds: null);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["smoke", "nightly"], result.Configuration!.TestCategories);
        Assert.Equal(["Game.Tests", "Game.MoreTests"], result.Configuration.AssemblyNames);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenProjectPathInputResolverReturnsExternalPath_UsesResolvedPathBeforeProfile ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "environment-before-profile");
        var environmentProjectPath = scope.GetPath("EnvironmentProject");
        var profile = new TestRunProfile
        {
            SchemaVersion = 1,
            ProjectPath = "./profile-project",
            UnityVersion = "6000.1.3f1",
            UnityEditorPath = "./profile-editor/Unity",
            TestPlatform = "editmode",
            TestFilter = null,
            TestCategories = [],
            AssemblyNames = [],
            TestSettingsPath = null,
            Timeout = 30,
        };

        var unityProject = CreateUnityProjectContext(scope, "EnvironmentProject");
        var unityProjectResolver = new StubUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject));
        var projectPathInputResolver = new StubProjectPathInputResolver((_, _) => environmentProjectPath);
        var resolver = new TestRunConfigurationResolver(
            new StubProfileLoader(TestRunProfileLoadResult.Success(profile)),
            projectPathInputResolver,
            unityProjectResolver,
            new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity"))),
            new StubPathNormalizer(),
            new StubPathExistenceProbe());

        var input = new TestRunConfigurationRequest(
            ProjectPath: null,
            ProfilePath: scope.GetPath("test.profile.json"),
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutMilliseconds: 30);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(environmentProjectPath), unityProjectResolver.LastProjectPath);
        Assert.Null(projectPathInputResolver.LastCommandOptionProjectPath);
        Assert.Equal("./profile-project", projectPathInputResolver.LastFallbackProjectPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenCommandOptionProjectPathIsSpecified_PrefersCommandOptionOverEnvironmentVariable ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "command-option-before-environment");
        var commandProjectPath = scope.GetPath("CommandProject");
        var environmentProjectPath = scope.GetPath("EnvironmentProject");
        var unityProject = CreateUnityProjectContext(scope, "CommandProject");
        var unityProjectResolver = new StubUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject));
        var projectPathInputResolver = new StubProjectPathInputResolver((commandOptionProjectPath, _) => commandOptionProjectPath ?? environmentProjectPath);
        var resolver = new TestRunConfigurationResolver(
            new StubProfileLoader(TestRunProfileLoadResult.Success(new TestRunProfile
            {
                SchemaVersion = 1,
                ProjectPath = "./profile-project",
                UnityVersion = "6000.1.3f1",
                UnityEditorPath = "./profile-editor/Unity",
                TestPlatform = "editmode",
                TestFilter = null,
                TestCategories = [],
                AssemblyNames = [],
                TestSettingsPath = null,
                Timeout = 30,
            })),
            projectPathInputResolver,
            unityProjectResolver,
            new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity"))),
            new StubPathNormalizer(),
            new StubPathExistenceProbe());

        var input = new TestRunConfigurationRequest(
            ProjectPath: commandProjectPath,
            ProfilePath: scope.GetPath("test.profile.json"),
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutMilliseconds: 30);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(commandProjectPath), unityProjectResolver.LastProjectPath);
        Assert.Equal(commandProjectPath, projectPathInputResolver.LastCommandOptionProjectPath);
        Assert.Equal("./profile-project", projectPathInputResolver.LastFallbackProjectPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WithPlayerTargetLiteral_ReturnsPlayerPlatform ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "player-target");

        var resolver = CreateResolverWithSuccessfulDependencies(scope);
        var input = new TestRunConfigurationRequest(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatform.Player("Android"),
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutMilliseconds: 30);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(TestRunPlatform.Player("Android"), result.Configuration!.TestPlatform);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Resolve_WithNonPositiveTimeout_ReturnsInvalidArgument (int timeoutMilliseconds)
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", $"timeout-{timeoutMilliseconds}");

        var resolver = CreateResolverWithSuccessfulDependencies(scope);
        var input = new TestRunConfigurationRequest(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutMilliseconds: timeoutMilliseconds);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WithMissingTestSettingsPath_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "missing-test-settings");

        var resolver = CreateResolverWithSuccessfulDependencies(scope);
        var input = new TestRunConfigurationRequest(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: scope.GetPath("ProjectSettings/TestSettings.json"),
            TimeoutMilliseconds: 30);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("testSettingsPath", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WithRelativeTestSettingsPath_ReturnsRepositoryRootBasedFullPath ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "relative-test-settings");
        var relativeTestSettingsPath = Path.Combine("ProjectSettings", "TestSettings.json");
        var normalizedTestSettingsPath = Path.GetFullPath(Path.Combine(scope.FullPath, relativeTestSettingsPath));

        var resolver = CreateResolverWithSuccessfulDependencies(scope, new StubPathExistenceProbe(normalizedTestSettingsPath));
        var input = new TestRunConfigurationRequest(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: relativeTestSettingsPath,
            TimeoutMilliseconds: 30);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(normalizedTestSettingsPath, result.Configuration!.TestSettingsPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WithInvalidTestSettingsPathFormat_ReturnsInvalidArgumentWithoutDiagnosticLeak ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "invalid-test-settings-format");
        var resolver = new TestRunConfigurationResolver(
            new StubProfileLoader(TestRunProfileLoadResult.Success(new TestRunProfile())),
            new StubProjectPathInputResolver(static (commandOptionProjectPath, fallbackProjectPath) => commandOptionProjectPath ?? fallbackProjectPath),
            new StubUnityProjectResolver(UnityProjectResolutionResult.Success(CreateUnityProjectContext(scope, "Unity"))),
            new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity"))),
            new StubPathNormalizer(TestRunPathNormalizationResult.Failure(
                TestRunPathNormalizationFailureKind.InvalidFormat,
                "diagnostic path details")),
            new StubPathExistenceProbe());
        var input = new TestRunConfigurationRequest(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: "invalid\0path",
            TimeoutMilliseconds: 30);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("testSettingsPath is invalid: Path format is invalid.", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("diagnostic path details", error.Message, StringComparison.Ordinal);
    }

    private static TestRunConfigurationResolver CreateResolverWithSuccessfulDependencies (TestDirectoryScope scope)
    {
        return CreateResolverWithSuccessfulDependencies(scope, new StubPathExistenceProbe());
    }

    private static TestRunConfigurationResolver CreateResolverWithSuccessfulDependencies (
        TestDirectoryScope scope,
        ITestRunPathExistenceProbe pathExistenceProbe)
    {
        var unityProject = CreateUnityProjectContext(scope, "Unity");

        return new TestRunConfigurationResolver(
            new StubProfileLoader(TestRunProfileLoadResult.Success(new TestRunProfile())),
            new StubProjectPathInputResolver(static (commandOptionProjectPath, fallbackProjectPath) => commandOptionProjectPath ?? fallbackProjectPath),
            new StubUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject)),
            new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity"))),
            new StubPathNormalizer(),
            pathExistenceProbe);
    }

    private static ResolvedUnityProjectContext CreateUnityProjectContext (
        TestDirectoryScope scope,
        string relativePath)
    {
        var projectPath = scope.GetPath(relativePath);
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: projectPath,
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private sealed class StubProfileLoader : ITestRunProfileLoader
    {
        private readonly TestRunProfileLoadResult result;

        public StubProfileLoader (TestRunProfileLoadResult result)
        {
            this.result = result;
        }

        public ValueTask<TestRunProfileLoadResult> LoadAsync (
            string profilePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubProjectPathInputResolver : IProjectPathInputResolver
    {
        private readonly Func<ProjectContextResolutionInput, ProjectPathCandidate> resolve;

        public StubProjectPathInputResolver (Func<string?, string?, string?> resolve)
            : this(input => new ProjectPathCandidate(
                resolve(input.CommandOptionProjectPath, input.FallbackProjectPath) ?? Environment.CurrentDirectory,
                ResolveSource(input, resolve)))
        {
        }

        public StubProjectPathInputResolver (Func<ProjectContextResolutionInput, ProjectPathCandidate> resolve)
        {
            this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
        }

        public string? LastCommandOptionProjectPath { get; private set; }

        public string? LastFallbackProjectPath { get; private set; }

        public ProjectPathCandidate? LastProjectPathCandidate { get; private set; }

        public ProjectPathCandidate Resolve (ProjectContextResolutionInput input)
        {
            LastCommandOptionProjectPath = input.CommandOptionProjectPath;
            LastFallbackProjectPath = input.FallbackProjectPath;
            LastProjectPathCandidate = resolve(input);
            return LastProjectPathCandidate;
        }

        private static UnityProjectPathSource ResolveSource (
            ProjectContextResolutionInput input,
            Func<string?, string?, string?> resolve)
        {
            var resolvedPath = resolve(input.CommandOptionProjectPath, input.FallbackProjectPath);
            if (string.Equals(resolvedPath, input.CommandOptionProjectPath, StringComparison.Ordinal))
            {
                return UnityProjectPathSource.CommandOption;
            }

            if (string.Equals(resolvedPath, input.FallbackProjectPath, StringComparison.Ordinal))
            {
                return UnityProjectPathSource.Fallback;
            }

            return UnityProjectPathSource.EnvironmentVariable;
        }
    }

    private sealed class StubPathExistenceProbe : ITestRunPathExistenceProbe
    {
        private readonly HashSet<string> existingPaths;

        public StubPathExistenceProbe (params string[] existingPaths)
        {
            this.existingPaths = existingPaths
                .Select(Path.GetFullPath)
                .ToHashSet(StringComparer.Ordinal);
        }

        public bool FileExists (string path)
        {
            return existingPaths.Contains(Path.GetFullPath(path));
        }
    }

    private sealed class StubPathNormalizer : ITestRunPathNormalizer
    {
        private readonly TestRunPathNormalizationResult? result;

        public StubPathNormalizer ()
        {
        }

        public StubPathNormalizer (TestRunPathNormalizationResult result)
        {
            this.result = result;
        }

        public TestRunPathNormalizationResult TryNormalizeRepositoryPath (
            string repositoryRoot,
            string path)
        {
            return result ?? TestRunPathNormalizationResult.Success(Path.GetFullPath(Path.Combine(repositoryRoot, path)));
        }
    }

    private sealed class StubUnityProjectResolver : IUnityProjectResolver
    {
        private readonly UnityProjectResolutionResult result;

        public StubUnityProjectResolver (UnityProjectResolutionResult result)
        {
            this.result = result;
        }

        public ProjectPathCandidate? LastProjectPathCandidate { get; private set; }

        public string? LastProjectPath => LastProjectPathCandidate?.Path;

        public UnityProjectResolutionResult Resolve (ProjectPathCandidate projectPathCandidate)
        {
            LastProjectPathCandidate = projectPathCandidate;
            return result;
        }
    }

    private sealed class StubUnityVersionResolver : IUnityVersionResolver
    {
        private readonly UnityVersionResolutionResult result;

        public StubUnityVersionResolver (UnityVersionResolutionResult result)
        {
            this.result = result;
        }

        public UnityVersionResolutionResult Resolve (
            string projectPath,
            string? preferredUnityVersion)
        {
            return result;
        }
    }

    private sealed class StubUnityEditorPathResolver : IUnityEditorPathResolver
    {
        private readonly UnityEditorPathResolutionResult result;

        public StubUnityEditorPathResolver (UnityEditorPathResolutionResult result)
        {
            this.result = result;
        }

        public UnityEditorPathResolutionResult Resolve (
            string unityVersion,
            string? preferredUnityEditorPath)
        {
            return result;
        }
    }
}
