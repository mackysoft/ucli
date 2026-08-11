using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Testing;
using static MackySoft.Ucli.Application.Tests.TestRunConfigurationResolverTestSupport;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunConfigurationResolverInputProjectionTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Resolve_WithCliOverridesProfileValues_ReturnsMergedCliValues ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "cli-overrides-profile");
        var profileUnityEditorPath = AbsolutePath.Parse(scope.GetPath("profile-editor/Unity"));

        var profile = new TestRunProfile
        {
            SchemaVersion = 1,
            ProjectPath = "./profile-project",
            UnityVersion = "6000.1.3f1",
            UnityEditorPath = profileUnityEditorPath,
            TestPlatform = "playmode",
            TestFilter = "Category=Smoke",
            TestCategories = ["profile"],
            AssemblyNames = ["Profile.Tests"],
            Timeout = 30,
        };

        var unityProject = CreateUnityProjectContext(scope, "cli-project");
        var profileLoader = new StubTestRunProfileLoader(TestRunProfileLoadResult.Success(profile));
        var unityProjectResolver = new RecordingUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject));
        var unityVersionResolver = new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1"));
        var unityEditorPathResolver = new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(
            AbsolutePath.Parse(scope.GetPath("Editors/6000.1.4f1/Editor/Unity"))));

        var resolver = new TestRunConfigurationResolver(
            profileLoader,
            new RecordingProjectPathInputResolver(static input => RecordingProjectPathInputResolver.Success(
                input.CommandOptionProjectPath ?? input.FallbackProjectPath ?? AbsolutePath.Parse(Environment.CurrentDirectory),
                input.CommandOptionProjectPath is not null
                    ? UnityProjectPathSource.CommandOption
                    : input.FallbackProjectPath is not null
                        ? UnityProjectPathSource.Fallback
                        : UnityProjectPathSource.CurrentDirectory)),
            unityProjectResolver,
            unityVersionResolver,
            unityEditorPathResolver);

        var input = new TestRunConfigurationRequest(
            ProjectPath: unityProject.UnityProjectRoot,
            ProfilePath: AbsolutePath.Parse(scope.GetPath("test.profile.json")),
            Mode: UnityExecutionMode.Oneshot,
            UnityVersion: "6000.1.4f1",
            UnityEditorPath: AbsolutePath.Parse(scope.GetPath("Editors/6000.1.4f1/Editor/Unity")),
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: "Name~Smoke",
            TestCategory: ["smoke", "quick"],
            AssemblyName: ["Cli.Tests"],
            TimeoutMilliseconds: 120);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        var succeeded = Assert.IsType<TestRunConfigurationResolutionResult.Succeeded>(result);
        var configuration = succeeded.Configuration;
        Assert.Equal(UnityExecutionMode.Oneshot, configuration.Mode);
        Assert.Equal(TestRunPlatform.EditMode, configuration.TestPlatform);
        Assert.Equal("Name~Smoke", configuration.TestFilter);
        Assert.Equal(["smoke", "quick"], configuration.TestCategories);
        Assert.Equal(["Cli.Tests"], configuration.AssemblyNames);
        Assert.Equal(120, configuration.TimeoutMilliseconds);
    }

    [Fact]
    [Trait("Size", "Medium")]
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
            Timeout = 30,
        };

        var resolver = new TestRunConfigurationResolver(
            new StubTestRunProfileLoader(TestRunProfileLoadResult.Success(profile)),
            new RecordingProjectPathInputResolver(static input => RecordingProjectPathInputResolver.Success(
                input.CommandOptionProjectPath ?? input.FallbackProjectPath ?? AbsolutePath.Parse(Environment.CurrentDirectory),
                input.CommandOptionProjectPath is not null
                    ? UnityProjectPathSource.CommandOption
                    : input.FallbackProjectPath is not null
                        ? UnityProjectPathSource.Fallback
                        : UnityProjectPathSource.CurrentDirectory)),
            new RecordingUnityProjectResolver(UnityProjectResolutionResult.Success(CreateUnityProjectContext(scope, "profile-project"))),
            new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(
                AbsolutePath.Parse(scope.GetPath("Editors/6000.1.4f1/Editor/Unity")))));

        var input = new TestRunConfigurationRequest(
            ProjectPath: null,
            ProfilePath: AbsolutePath.Parse(scope.GetPath("test.profile.json")),
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: null,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TimeoutMilliseconds: null);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        var succeeded = Assert.IsType<TestRunConfigurationResolutionResult.Succeeded>(result);
        Assert.Equal(["smoke", "nightly"], succeeded.Configuration.TestCategories);
        Assert.Equal(["Game.Tests", "Game.MoreTests"], succeeded.Configuration.AssemblyNames);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Resolve_WithPlayerTargetLiteral_ReturnsPlayerPlatform ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "player-target");

        var resolver = CreateResolverWithSuccessfulDependencies(scope);
        var input = new TestRunConfigurationRequest(
            ProjectPath: AbsolutePath.Parse(scope.GetPath("Unity")),
            ProfilePath: null,
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatform.Player("Android"),
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TimeoutMilliseconds: 30);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        var succeeded = Assert.IsType<TestRunConfigurationResolutionResult.Succeeded>(result);
        Assert.Equal(TestRunPlatform.Player("Android"), succeeded.Configuration.TestPlatform);
    }
}
