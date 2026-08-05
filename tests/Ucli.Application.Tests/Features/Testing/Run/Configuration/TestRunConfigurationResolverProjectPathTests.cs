using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Testing;
using static MackySoft.Ucli.Application.Tests.TestRunConfigurationResolverTestSupport;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunConfigurationResolverProjectPathTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Resolve_WhenProjectPathInputResolverReturnsExternalPath_UsesResolvedPathBeforeProfile ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "environment-before-profile");
        var environmentProjectPath = AbsolutePath.Parse(scope.GetPath("EnvironmentProject"));
        var profileUnityEditorPath = AbsolutePath.Parse(scope.GetPath("profile-editor/Unity"));
        var profile = new TestRunProfile
        {
            SchemaVersion = 1,
            ProjectPath = "./profile-project",
            UnityVersion = "6000.1.3f1",
            UnityEditorPath = profileUnityEditorPath,
            TestPlatform = "editmode",
            TestFilter = null,
            TestCategories = [],
            AssemblyNames = [],
            Timeout = 30,
        };

        var unityProject = CreateUnityProjectContext(scope, "EnvironmentProject");
        var unityProjectResolver = new RecordingUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject));
        var projectPathInputResolver = new RecordingProjectPathInputResolver(_ =>
            RecordingProjectPathInputResolver.Success(
                environmentProjectPath,
                UnityProjectPathSource.EnvironmentVariable));
        var resolver = new TestRunConfigurationResolver(
            new StubTestRunProfileLoader(TestRunProfileLoadResult.Success(profile)),
            projectPathInputResolver,
            unityProjectResolver,
            new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(
                AbsolutePath.Parse(scope.GetPath("Editors/6000.1.4f1/Editor/Unity")))));

        var input = new TestRunConfigurationRequest(
            ProjectPath: null,
            ProfilePath: AbsolutePath.Parse(scope.GetPath("test.profile.json")),
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TimeoutMilliseconds: 30);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.IsType<TestRunConfigurationResolutionResult.Succeeded>(result);
        ProjectPathInputResolverAssert.ResolvedOnceFor(
            projectPathInputResolver,
            expectedCommandOptionProjectPath: null,
            expectedFallbackProjectPath: AbsolutePath.Parse(Path.GetFullPath("./profile-project")),
            expectedFallbackSourceLabel: ProfileProjectPathSourceLabel,
            expectedResolvedPath: environmentProjectPath,
            expectedSource: UnityProjectPathSource.EnvironmentVariable);
        UnityProjectResolverAssert.ResolvedOnceFor(
            unityProjectResolver,
            environmentProjectPath,
            UnityProjectPathSource.EnvironmentVariable);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Resolve_WhenCommandOptionProjectPathIsSpecified_PrefersCommandOptionOverEnvironmentVariable ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "command-option-before-environment");
        var commandProjectPath = AbsolutePath.Parse(scope.GetPath("CommandProject"));
        var environmentProjectPath = AbsolutePath.Parse(scope.GetPath("EnvironmentProject"));
        var profileUnityEditorPath = AbsolutePath.Parse(scope.GetPath("profile-editor/Unity"));
        var unityProject = CreateUnityProjectContext(scope, "CommandProject");
        var unityProjectResolver = new RecordingUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject));
        var projectPathInputResolver = new RecordingProjectPathInputResolver(input =>
            RecordingProjectPathInputResolver.Success(
                input.CommandOptionProjectPath ?? environmentProjectPath,
                input.CommandOptionProjectPath is not null
                    ? UnityProjectPathSource.CommandOption
                    : UnityProjectPathSource.EnvironmentVariable));
        var resolver = new TestRunConfigurationResolver(
            new StubTestRunProfileLoader(TestRunProfileLoadResult.Success(new TestRunProfile
            {
                SchemaVersion = 1,
                ProjectPath = "./profile-project",
                UnityVersion = "6000.1.3f1",
                UnityEditorPath = profileUnityEditorPath,
                TestPlatform = "editmode",
                TestFilter = null,
                TestCategories = [],
                AssemblyNames = [],
                Timeout = 30,
            })),
            projectPathInputResolver,
            unityProjectResolver,
            new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(
                AbsolutePath.Parse(scope.GetPath("Editors/6000.1.4f1/Editor/Unity")))));

        var input = new TestRunConfigurationRequest(
            ProjectPath: commandProjectPath,
            ProfilePath: AbsolutePath.Parse(scope.GetPath("test.profile.json")),
            Mode: UnityExecutionMode.Auto,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatform.EditMode,
            TestFilter: null,
            TestCategory: null,
            AssemblyName: null,
            TimeoutMilliseconds: 30);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.IsType<TestRunConfigurationResolutionResult.Succeeded>(result);
        ProjectPathInputResolverAssert.ResolvedOnceFor(
            projectPathInputResolver,
            expectedCommandOptionProjectPath: commandProjectPath,
            expectedFallbackProjectPath: AbsolutePath.Parse(Path.GetFullPath("./profile-project")),
            expectedFallbackSourceLabel: ProfileProjectPathSourceLabel,
            expectedResolvedPath: commandProjectPath,
            expectedSource: UnityProjectPathSource.CommandOption);
        UnityProjectResolverAssert.ResolvedOnceFor(
            unityProjectResolver,
            commandProjectPath,
            UnityProjectPathSource.CommandOption);
    }
}
