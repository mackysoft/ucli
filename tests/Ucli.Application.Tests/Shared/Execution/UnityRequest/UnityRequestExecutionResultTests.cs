using MackySoft.Ucli.Contracts.Execution;
using static MackySoft.Ucli.Application.Tests.Features.Assurance.Compile.CompileServiceTestSupport;

namespace MackySoft.Ucli.Application.Tests.Shared.Execution.UnityRequest;

public sealed class UnityRequestExecutionResultTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Failure_WhenConfirmedHostExitDoesNotMatchFixedStartProcess_RejectsObservation ()
    {
        var start = CreateStart();
        var mismatchedProcess = new ProcessIdentity(
            start.Host.Process.ProcessId,
            start.Host.Process.Generation == ulong.MaxValue
                ? start.Host.Process.Generation - 1
                : start.Host.Process.Generation + 1);

        Assert.Throws<ArgumentException>(
            () => UnityRequestExecutionResult.Failure(
                new UnityRequestFailure(
                    UnityRequestFailureKind.General,
                    EditorLifecycleErrorCodes.EditorUnavailable,
                    "A different process generation exited."),
                start,
                lifecycleActionDispatched: false,
                new LifecycleExecutionHostExitObservation(
                    mismatchedProcess)));
    }
}
