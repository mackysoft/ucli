namespace MackySoft.Ucli.Tests.Supervisor;

internal static class SupervisorBootstrapperTestSupport
{
    public static readonly TimeSpan SignalWaitTimeout = TimeSpan.FromSeconds(5);

    public static SupervisorInstanceManifest CreateManifest (
        byte sessionTokenDiscriminator = 1,
        int processId = 2468,
        SupervisorTransportEndpoint? endpoint = null,
        DateTimeOffset? issuedAtUtc = null)
    {
        return new SupervisorInstanceManifest(
            processId: processId,
            sessionToken: IpcSessionTokenTestFactory.CreateFromDiscriminator(sessionTokenDiscriminator),
            endpoint: endpoint ?? SupervisorTransportEndpoint.FromNamedPipeAddress("ucli-supervisor-test"),
            issuedAtUtc: issuedAtUtc ?? new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero));
    }
}
