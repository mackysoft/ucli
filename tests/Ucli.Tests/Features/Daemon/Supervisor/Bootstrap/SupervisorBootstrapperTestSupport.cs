namespace MackySoft.Ucli.Tests.Supervisor;

internal static class SupervisorBootstrapperTestSupport
{
    public static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    public static SupervisorInstanceManifest CreateManifest (
        int processId = 2468,
        string endpointAddress = "/tmp/ucli-supervisor-test.sock")
    {
        return new SupervisorInstanceManifest(
            ProcessId: processId,
            SessionToken: "supervisor-session-token",
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: endpointAddress,
            IssuedAtUtc: new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
    }
}
