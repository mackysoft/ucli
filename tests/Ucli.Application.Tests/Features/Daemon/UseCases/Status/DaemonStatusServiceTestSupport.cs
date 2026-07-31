using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Application.Tests.Daemon;

internal static class DaemonStatusServiceTestSupport
{
    public static DaemonStatusService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IDaemonStatusOperation daemonStatusOperation)
    {
        return new DaemonStatusService(
            resolver,
            daemonStatusOperation,
            new DaemonSessionOutputMapper(),
            new DaemonDiagnosisOutputMapper());
    }

    public static UnityEditorObservation CreatePingResponse ()
    {
        return UnityEditorObservationTestFactory.Create();
    }
}
