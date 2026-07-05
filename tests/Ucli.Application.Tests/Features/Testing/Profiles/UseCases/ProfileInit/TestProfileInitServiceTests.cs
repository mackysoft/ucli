using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;
using MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Testing.Profiles;

public sealed class TestProfileInitServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenInputIsValid_WritesDefaultProfileThroughTemplateStore ()
    {
        var expectedResult = TestProfileInitExecutionResult.Success(new TestProfileInitExecutionOutput("/repo/test.profile.json"));
        var templateStore = new RecordingTestProfileTemplateStore(expectedResult);
        var service = new TestProfileInitService(templateStore);

        var result = await service.ExecuteAsync(new TestProfileInitCommandInput(OutputPath: "custom.json", Force: true), CancellationToken.None);

        Assert.Same(expectedResult, result);
        TestProfileTemplateStoreAssert.DefaultProfileWritten(
            templateStore,
            expectedOutputPath: "custom.json",
            expectedForce: true);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenTemplateStoreFails_ReturnsStoreError ()
    {
        var expectedResult = TestProfileInitExecutionResult.Failure(ExecutionError.InvalidArgument("invalid output."));
        var service = new TestProfileInitService(new RecordingTestProfileTemplateStore(expectedResult));

        var result = await service.ExecuteAsync(new TestProfileInitCommandInput(OutputPath: null, Force: false), CancellationToken.None);

        Assert.Same(expectedResult, result);
    }

}
