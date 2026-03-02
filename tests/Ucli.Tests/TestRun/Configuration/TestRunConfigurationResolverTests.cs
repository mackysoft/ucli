using MackySoft.Tests;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.TestRun;
using MackySoft.Ucli.TestRun.Configuration;
using MackySoft.Ucli.UnityProject;
using MackySoft.Ucli.UnityProject.Resolution;

namespace MackySoft.Ucli.Tests;

public sealed class TestRunConfigurationResolverTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithCliOverridesProfileValues_ReturnsMergedCliValues ()
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
            TimeoutSeconds = 30,
        };

        var unityProject = CreateUnityProjectContext(scope, "cli-project");
        var profileLoader = new StubProfileLoader(TestRunProfileLoadResult.Success(profile));
        var unityProjectResolver = new StubUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject));
        var unityVersionResolver = new StubUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var unityEditorPathResolver = new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity")));

        var resolver = new TestRunConfigurationResolver(
            profileLoader,
            unityProjectResolver,
            unityVersionResolver,
            unityEditorPathResolver);

        var input = new TestRunCommandInput(
            ProjectPath: unityProject.UnityProjectRoot,
            ProfilePath: scope.GetPath("test.profile.json"),
            Mode: "oneshot",
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: scope.GetPath("Editors/6000.1.4f1/Editor/Unity"),
            TestPlatform: "editmode",
            BuildTarget: null,
            TestFilter: "Name~Smoke",
            TestCategory: ["smoke,quick"],
            AssemblyName: ["Cli.Tests"],
            TestSettingsPath: testSettingsPath,
            TimeoutSeconds: 120);

        var result = resolver.Resolve(input);

        Assert.True(result.IsSuccess);
        var configuration = Assert.IsType<ResolvedTestRunConfiguration>(result.Configuration);
        Assert.Equal("oneshot", configuration.Mode);
        Assert.Equal(TestRunPlatform.EditMode, configuration.TestPlatform);
        Assert.Equal("editmode", configuration.RawTestPlatform);
        Assert.Null(configuration.BuildTarget);
        Assert.Equal("Name~Smoke", configuration.TestFilter);
        Assert.Equal(["smoke", "quick"], configuration.TestCategories);
        Assert.Equal(["Cli.Tests"], configuration.AssemblyNames);
        Assert.Equal(testSettingsPath, configuration.TestSettingsPath);
        Assert.Equal(120, configuration.TimeoutSeconds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithInvalidTestPlatform_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "invalid-platform");

        var resolver = CreateResolverWithSuccessfulDependencies(scope);
        var input = new TestRunCommandInput(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: "auto",
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: "unknown",
            BuildTarget: null,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutSeconds: 30);

        var result = resolver.Resolve(input);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("testPlatform", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithEditModeAndBuildTarget_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "editmode-buildtarget");

        var resolver = CreateResolverWithSuccessfulDependencies(scope);
        var input = new TestRunCommandInput(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: "auto",
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: "editmode",
            BuildTarget: "Android",
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutSeconds: 30);

        var result = resolver.Resolve(input);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("buildTarget", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(86401)]
    public void Resolve_WithTimeoutOutOfRange_ReturnsInvalidArgument (int timeoutSeconds)
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", $"timeout-{timeoutSeconds}");

        var resolver = CreateResolverWithSuccessfulDependencies(scope);
        var input = new TestRunCommandInput(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: "auto",
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: "editmode",
            BuildTarget: null,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: null,
            TimeoutSeconds: timeoutSeconds);

        var result = resolver.Resolve(input);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeoutSeconds", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Resolve_WithMissingTestSettingsPath_ReturnsInvalidArgument ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "missing-test-settings");

        var resolver = CreateResolverWithSuccessfulDependencies(scope);
        var input = new TestRunCommandInput(
            ProjectPath: scope.GetPath("Unity"),
            ProfilePath: null,
            Mode: "auto",
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: "editmode",
            BuildTarget: null,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TestSettingsPath: scope.GetPath("ProjectSettings/TestSettings.json"),
            TimeoutSeconds: 30);

        var result = resolver.Resolve(input);

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

        public TestRunProfileLoadResult Load (string profilePath)
        {
            return result;
        }
    }

    private sealed class StubUnityProjectResolver : IUnityProjectResolver
    {
        private readonly UnityProjectResolutionResult result;

        public StubUnityProjectResolver (UnityProjectResolutionResult result)
        {
            this.result = result;
        }

        public UnityProjectResolutionResult Resolve (string? projectPath)
        {
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