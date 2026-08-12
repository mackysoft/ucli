using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests.Features.Assurance.Ready;

using static MackySoft.Ucli.Application.Tests.Features.Assurance.Ready.ReadyServiceTestSupport;

public sealed class ReadyServiceInputValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithReadIndexModeOnNonReadIndexTarget_ReturnsInvalidArgument ()
    {
        var service = CreateService();
        var input = new ReadyCommandInput(
            ProjectPath: null,
            Target: ReadyTarget.Execution,
            Mode: UnityExecutionMode.Auto,
            TimeoutMilliseconds: 10000,
            ReadIndexMode: ReadIndexMode.RequireFresh,
            IsReadIndexModeSpecified: true,
            FailFast: false);

        var result = await service.ExecuteAsync(input);

        var failed = Assert.IsType<ReadyExecutionResult.FailedResult>(result);
        var error = failed.Failure;
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithExplicitRuntimeModeOnReadIndexTarget_ReturnsInvalidArgument ()
    {
        var service = CreateService(readIndexArtifactReader: ReadyReadIndexArtifactReaderFactory.CreateReadyArtifacts());

        var result = await service.ExecuteAsync(CreateReadIndexInput(
            mode: UnityExecutionMode.Daemon,
            readIndexMode: ReadIndexMode.AllowStale));

        var failed = Assert.IsType<ReadyExecutionResult.FailedResult>(result);
        var error = failed.Failure;
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WithReadIndexDisabled_ReturnsInvalidArgument ()
    {
        var service = CreateService(readIndexArtifactReader: ReadyReadIndexArtifactReaderFactory.CreateReadyArtifacts());

        var result = await service.ExecuteAsync(CreateReadIndexInput(
            mode: UnityExecutionMode.Auto,
            readIndexMode: ReadIndexMode.Disabled));

        var failed = Assert.IsType<ReadyExecutionResult.FailedResult>(result);
        var error = failed.Failure;
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, error.Code);
    }
}
