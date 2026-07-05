using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Start;

namespace MackySoft.Ucli.Application.Tests.Daemon;

internal static class DaemonStartServiceTestSupport
{
    public static DaemonStartService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        RecordingDaemonProjectLifecycleGateway supervisorProjectGateway,
        IUnityPluginVerifier? pluginVerifier = null,
        TimeProvider? timeProvider = null)
    {
        pluginVerifier ??= new RecordingUnityPluginVerifier();
        return new DaemonStartService(
            resolver,
            supervisorProjectGateway,
            pluginVerifier,
            new DaemonSessionOutputMapper(),
            new DaemonDiagnosisOutputMapper(),
            timeProvider);
    }
}
