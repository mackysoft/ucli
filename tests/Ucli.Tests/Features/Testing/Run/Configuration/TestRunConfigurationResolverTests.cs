using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Testing.Run;
using MackySoft.Ucli.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Shared.EnvironmentVariables;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Shared.Foundation;
using MackySoft.Ucli.UnityIntegration.Project;
using MackySoft.Ucli.UnityIntegration.Resolution;
using static MackySoft.Ucli.Tests.Helpers.Cli.CommandOptionNormalizationTestHelper;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunConfigurationResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WithCliOverridesProfileValues_ReturnsMergedCliValues ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "cli-overrides-profile");
        var testSettingsPath = scope.WriteFile("ProjectSettings/TestSettings.json", "{}");

        var profile = new TestRunProfile
        {
            SchemaVersion = 1,
            ProjectPath = "./profile-project",
            UnityVersion = "6000.1.3f1",
            UnityEditorPath = "./profile-editor/Unity",
            TestPlatform = "playmode",
            BuildTarget = null,
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
            new ProjectPathInputResolver(new StubEnvironmentVariableReader()),
            unityProjectResolver,
            unityVersionResolver,
            unityEditorPathResolver);

        var input = new TestRunCommandInput(
            ProjectPath: unityProject.UnityProjectRoot,
            ProfilePath: scope.GetPath("test.profile.json"),
            Mode: NormalizeMode("oneshot"),
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: NormalizeTestPlatform("editmode"),
            BuildTarget: null,
            TestFilter: "Name~Smoke",
            TestCategory: ["smoke,quick"],
            AssemblyName: ["Cli.Tests"],
            TestSettingsPath: testSettingsPath,
            TimeoutMilliseconds: 120);

        var result = await resolver.Resolve(input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var configuration = Assert.IsType<ResolvedTestRunConfiguration>(result.Configuration);
        Assert.Equal(UnityExecutionMode.Oneshot, configuration.Mode);
        Assert.Equal(IpcTestRunPlatform.EditMode, configuration.TestPlatform);
        Assert.Equal("editmode", configuration.RawTestPlatform);
        Assert.Null(configuration.BuildTarget);
        Assert.Equal("Name~Smoke", configuration.TestFilter);
        Assert.Equal(["smoke", "quick"], configuration.TestCategories);
        Assert.Equal(["Cli.Tests"], configuration.AssemblyNames);
        Assert.Equal(testSettingsPath, configuration.TestSettingsPath);
        Assert.Equal(120, configuration.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WhenCommandOptionProjectPathIsMissing_UsesEnvironmentVariableBeforeProfile ()
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
            BuildTarget = null,
            TestFilter = null,
            TestCategories = [],
            AssemblyNames = [],
            TestSettingsPath = null,
            Timeout = 30,
        };

        var unityProject = CreateUnityProjectContext(scope, "EnvironmentProject");
        var unityProjectResolver = new StubUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject));
        var resolver = new TestRunConfigurationResolver(
            new StubProfileLoader(TestRunProfileLoadResult.Success(profile)),
            new ProjectPathInputResolver(new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [UcliEnvironmentVariableNames.ProjectPath] = environmentProjectPath,
            })),
            unityProjectResolver,
            new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity"))));

        var input = new TestRunCommandInput(
            ProjectPath: null,
            ProfilePath: scope.GetPath("test.profile.json"),
            Mode: NormalizeMode("auto"),
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: NormalizeTestPlatform("editmode"),
            BuildTarget: null,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutMilliseconds: 30);

        var result = await resolver.Resolve(input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(environmentProjectPath), unityProjectResolver.LastProjectPath);
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
        var resolver = new TestRunConfigurationResolver(
            new StubProfileLoader(TestRunProfileLoadResult.Success(new TestRunProfile
            {
                SchemaVersion = 1,
                ProjectPath = "./profile-project",
                UnityVersion = "6000.1.3f1",
                UnityEditorPath = "./profile-editor/Unity",
                TestPlatform = "editmode",
                BuildTarget = null,
                TestFilter = null,
                TestCategories = [],
                AssemblyNames = [],
                TestSettingsPath = null,
                Timeout = 30,
            })),
            new ProjectPathInputResolver(new StubEnvironmentVariableReader(new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                [UcliEnvironmentVariableNames.ProjectPath] = environmentProjectPath,
            })),
            unityProjectResolver,
            new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity"))));

        var input = new TestRunCommandInput(
            ProjectPath: commandProjectPath,
            ProfilePath: scope.GetPath("test.profile.json"),
            Mode: NormalizeMode("auto"),
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: NormalizeTestPlatform("editmode"),
            BuildTarget: null,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutMilliseconds: 30);

        var result = await resolver.Resolve(input, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(Path.GetFullPath(commandProjectPath), unityProjectResolver.LastProjectPath);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Resolve_WithEditModeAndBuildTarget_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "editmode-buildtarget");

        var resolver = CreateResolverWithSuccessfulDependencies(scope);
        var input = new TestRunCommandInput(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: NormalizeMode("auto"),
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: NormalizeTestPlatform("editmode"),
            BuildTarget: "Android",
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutMilliseconds: 30);

        var result = await resolver.Resolve(input, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("buildTarget", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Resolve_WithNonPositiveTimeout_ReturnsInvalidArgument (int timeoutMilliseconds)
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", $"timeout-{timeoutMilliseconds}");

        var resolver = CreateResolverWithSuccessfulDependencies(scope);
        var input = new TestRunCommandInput(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: NormalizeMode("auto"),
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: NormalizeTestPlatform("editmode"),
            BuildTarget: null,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutMilliseconds: timeoutMilliseconds);

        var result = await resolver.Resolve(input, CancellationToken.None);

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
        var input = new TestRunCommandInput(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: NormalizeMode("auto"),
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: NormalizeTestPlatform("editmode"),
            BuildTarget: null,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: scope.GetPath("ProjectSettings/TestSettings.json"),
            TimeoutMilliseconds: 30);

        var result = await resolver.Resolve(input, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("testSettingsPath", error.Message, StringComparison.Ordinal);
    }

    private static TestRunConfigurationResolver CreateResolverWithSuccessfulDependencies (TestDirectoryScope scope)
    {
        var unityProject = CreateUnityProjectContext(scope, "Unity");

        return new TestRunConfigurationResolver(
            new StubProfileLoader(TestRunProfileLoadResult.Success(new TestRunProfile())),
            new ProjectPathInputResolver(new StubEnvironmentVariableReader()),
            new StubUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject)),
            new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity"))));
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

        public ValueTask<TestRunProfileLoadResult> Load (
            string profilePath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class StubUnityProjectResolver : IUnityProjectResolver
    {
        private readonly UnityProjectResolutionResult result;

        public StubUnityProjectResolver (UnityProjectResolutionResult result)
        {
            this.result = result;
        }

        public string? LastProjectPath { get; private set; }

        public UnityProjectResolutionResult Resolve (string? projectPath)
        {
            LastProjectPath = projectPath;
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