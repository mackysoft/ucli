using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal static class TestRunConfigurationResolverTestSupport
{
    public const string ProfileProjectPathSourceLabel = "testRunProfile.projectPath";

    public static TestRunConfigurationResolver CreateResolverWithSuccessfulDependencies (TestDirectoryScope scope)
    {
        var unityProject = CreateUnityProjectContext(scope, "Unity");

        return new TestRunConfigurationResolver(
            new StubTestRunProfileLoader(TestRunProfileLoadResult.Success(new TestRunProfile())),
            new RecordingProjectPathInputResolver(static (commandOptionProjectPath, fallbackProjectPath) => commandOptionProjectPath ?? fallbackProjectPath),
            new RecordingUnityProjectResolver(UnityProjectResolutionResult.Success(unityProject)),
            new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(
                AbsolutePath.Parse(scope.GetPath("Editors/6000.1.4f1/Editor/Unity")))));
    }

    public static ResolvedUnityProjectContext CreateUnityProjectContext (
        TestDirectoryScope scope,
        string relativePath)
    {
        var projectPath = scope.GetPath(relativePath);
        return ProjectContextTestFactory.CreateUnityProjectWithPaths(
            unityProjectRoot: projectPath,
            repositoryRoot: scope.FullPath,
            projectFingerprint: ProjectFingerprintTestFactory.Create("fingerprint"),
            pathSourceLabel: null,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
    }
}
