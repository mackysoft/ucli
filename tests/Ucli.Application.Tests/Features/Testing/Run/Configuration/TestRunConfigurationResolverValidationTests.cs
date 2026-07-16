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
            TimeoutMilliseconds: timeoutMilliseconds);

        var result = await resolver.ResolveAsync(input, CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.Single(result.Errors);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("timeout", error.Message, StringComparison.Ordinal);
    }

}
