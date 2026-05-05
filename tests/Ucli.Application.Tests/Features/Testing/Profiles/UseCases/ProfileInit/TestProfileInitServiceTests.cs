using MackySoft.Ucli.Application.Features.Testing.Profiles;
using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Profiles.Ports;
using MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Testing.Profiles;

public sealed class TestProfileInitServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WritesDefaultProfileThroughTemplateStore ()
    {
        var expectedResult = TestProfileInitExecutionResult.Success(new TestProfileInitExecutionOutput("/repo/test.profile.json"));
        var templateStore = new StubTestProfileTemplateStore(expectedResult);
        var service = new TestProfileInitService(templateStore);

        var result = await service.ExecuteAsync(new TestProfileInitCommandInput(OutputPath: "custom.json", Force: true), CancellationToken.None);

        Assert.Same(expectedResult, result);
        Assert.Equal("custom.json", templateStore.LastOutputPath);
        Assert.True(templateStore.LastForce);
        var profile = Assert.IsType<TestProfile>(templateStore.LastProfile);
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal(".", profile.ProjectPath);
        Assert.Null(profile.UnityVersion);
        Assert.Null(profile.UnityEditorPath);
        Assert.Equal("editmode", profile.TestPlatform);
        Assert.Null(profile.TestFilter);
        Assert.Empty(profile.TestCategories);
        Assert.Empty(profile.AssemblyNames);
        Assert.Null(profile.TestSettingsPath);
        Assert.Equal(1800000, profile.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ExecuteAsync_WhenTemplateStoreFails_ReturnsStoreError ()
    {
        var expectedResult = TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument("invalid output."));
        var service = new TestProfileInitService(new StubTestProfileTemplateStore(expectedResult));

        var result = await service.ExecuteAsync(new TestProfileInitCommandInput(OutputPath: null, Force: false), CancellationToken.None);

        Assert.Same(expectedResult, result);
    }

    private sealed class StubTestProfileTemplateStore : ITestProfileTemplateStore
    {
        private readonly TestProfileInitExecutionResult result;

        public StubTestProfileTemplateStore (TestProfileInitExecutionResult result)
        {
            this.result = result;
        }

        public TestProfile? LastProfile { get; private set; }

        public string? LastOutputPath { get; private set; }

        public bool LastForce { get; private set; }

        public ValueTask<TestProfileInitExecutionResult> WriteAsync (
            TestProfile profile,
            string? outputPath,
            bool force,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastProfile = profile;
            LastOutputPath = outputPath;
            LastForce = force;
            return ValueTask.FromResult(result);
        }
    }
}
