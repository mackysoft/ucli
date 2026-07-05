using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Testing.Run.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Testing;
using static MackySoft.Ucli.Application.Tests.TestRunConfigurationResolverTestSupport;

namespace MackySoft.Ucli.Application.Tests;

public sealed class TestRunConfigurationResolverValidationTests
{
    [Theory]
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
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
    [Trait("Size", "Medium")]
    public async Task Resolve_WithRelativeTestSettingsPath_ReturnsRepositoryRootBasedFullPath ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "relative-test-settings");
        var relativeTestSettingsPath = Path.Combine("ProjectSettings", "TestSettings.json");
        var normalizedTestSettingsPath = Path.GetFullPath(Path.Combine(scope.FullPath, relativeTestSettingsPath));

        var resolver = CreateResolverWithSuccessfulDependencies(scope, new StubTestRunPathExistenceProbe(normalizedTestSettingsPath));
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
    [Trait("Size", "Medium")]
    public async Task Resolve_WithInvalidTestSettingsPathFormat_ReturnsInvalidArgumentWithoutDiagnosticLeak ()
    {
        using var scope = TestDirectories.CreateTempScope("test-run-config-resolver", "invalid-test-settings-format");
        var resolver = new TestRunConfigurationResolver(
            new StubTestRunProfileLoader(TestRunProfileLoadResult.Success(new TestRunProfile())),
            new RecordingProjectPathInputResolver(static (commandOptionProjectPath, fallbackProjectPath) => commandOptionProjectPath ?? fallbackProjectPath),
            new RecordingUnityProjectResolver(UnityProjectResolutionResult.Success(CreateUnityProjectContext(scope, "Unity"))),
            new RecordingUnityVersionResolver(UnityVersionResolutionResult.Success("6000.1.4f1")),
            new StubUnityEditorPathResolver(UnityEditorPathResolutionResult.Success(scope.GetPath("Editors/6000.1.4f1/Editor/Unity"))),
            new StubTestRunPathNormalizer(TestRunPathNormalizationResult.Failure(
                TestRunPathNormalizationFailureKind.InvalidFormat,
                "diagnostic path details")),
            new StubTestRunPathExistenceProbe());
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
}
