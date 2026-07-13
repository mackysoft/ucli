namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using static MackySoft.Ucli.Tests.Daemon.DaemonGuiRebootstrapClientTestSupport;

public sealed class DaemonGuiRebootstrapClientManifestValidationTests
{
    public static TheoryData<InvalidManifestCase> InvalidManifestCases =>
    [
        new(
            "schema-version",
            0,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            IpcSessionTokenTestFactory.Create("schema-version-token").GetEncodedValue(),
            ContractLiteralCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            "/tmp/ucli-gui-supervisor.sock",
            ProcessStartedAtUtc),
        new(
            "project-fingerprint",
            GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            ProjectFingerprintTestFactory.Create("other-fingerprint"),
            IpcSessionTokenTestFactory.Create("project-fingerprint-token").GetEncodedValue(),
            ContractLiteralCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            "/tmp/ucli-gui-supervisor.sock",
            ProcessStartedAtUtc),
        new(
            "started-at",
            GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            IpcSessionTokenTestFactory.Create("started-at-token").GetEncodedValue(),
            ContractLiteralCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            "/tmp/ucli-gui-supervisor.sock",
            ProcessStartedAtUtc.Add(DaemonProcessStartTimeMatcher.Tolerance).AddMilliseconds(1)),
        new(
            "session-token",
            GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            "",
            ContractLiteralCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            "/tmp/ucli-gui-supervisor.sock",
            ProcessStartedAtUtc),
        new(
            "transport-kind",
            GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            IpcSessionTokenTestFactory.Create("transport-kind-token").GetEncodedValue(),
            "",
            "/tmp/ucli-gui-supervisor.sock",
            ProcessStartedAtUtc),
        new(
            "endpoint-address",
            GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            ProjectFingerprintTestFactory.Create("fingerprint"),
            IpcSessionTokenTestFactory.Create("endpoint-address-token").GetEncodedValue(),
            ContractLiteralCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            "",
            ProcessStartedAtUtc),
    ];

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenManifestMissing_ReturnsUnavailableWithoutIpc ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenManifestMissing_ReturnsUnavailableWithoutIpc));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        var transportClient = new StubIpcTransportClient();
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            expectedProcessId: 1234,
            ProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        AssertUnavailableWithoutIpc(result, transportClient);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenManifestProcessDoesNotMatch_ReturnsUnavailableWithoutIpc ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenManifestProcessDoesNotMatch_ReturnsUnavailableWithoutIpc));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, CreateManifest());
        var transportClient = new StubIpcTransportClient();
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            expectedProcessId: 5678,
            ProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        AssertUnavailableWithoutIpc(result, transportClient);
    }

    [Theory]
    [MemberData(nameof(InvalidManifestCases))]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenManifestValidationFails_ReturnsUnavailableWithoutIpc (
        InvalidManifestCase testCase)
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            $"{nameof(RequestRebootstrapAsync_WhenManifestValidationFails_ReturnsUnavailableWithoutIpc)}-{testCase.Name}");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        var manifest = CreateManifest();
        await WriteManifestAsync(
            scope.FullPath,
            unityProject.ProjectFingerprint,
            new
            {
                testCase.SchemaVersion,
                testCase.SessionToken,
                ProjectFingerprint = testCase.ProjectFingerprint.ToString(),
                testCase.EndpointTransportKind,
                testCase.EndpointAddress,
                manifest.ProcessId,
                manifest.ProcessStartedAtUtc,
                manifest.IssuedAtUtc,
            });
        var transportClient = new StubIpcTransportClient();
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            testCase.ExpectedProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        AssertUnavailableWithoutIpc(result, transportClient);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RequestRebootstrapAsync_WhenManifestTransportKindIsInvalid_ReturnsUnavailableWithoutIpc ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "daemon-command-service",
            nameof(RequestRebootstrapAsync_WhenManifestTransportKindIsInvalid_ReturnsUnavailableWithoutIpc));
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath, ProjectFingerprintTestFactory.Create("fingerprint"));
        var manifest = CreateManifest();
        await WriteManifestAsync(
            scope.FullPath,
            unityProject.ProjectFingerprint,
            new
            {
                EndpointTransportKind = "invalid-transport",
                manifest.SchemaVersion,
                manifest.SessionToken,
                ProjectFingerprint = manifest.ProjectFingerprint.ToString(),
                manifest.EndpointAddress,
                manifest.ProcessId,
                manifest.ProcessStartedAtUtc,
                manifest.IssuedAtUtc,
            });
        var transportClient = new StubIpcTransportClient();
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            expectedProcessId: 1234,
            ProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        AssertUnavailableWithoutIpc(result, transportClient);
    }

    private static void AssertUnavailableWithoutIpc (
        DaemonGuiRebootstrapRequestResult result,
        StubIpcTransportClient transportClient)
    {
        Assert.False(result.IsAccepted);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        DaemonGuiRebootstrapTransportAssert.NoIpcRequestWasSent(transportClient);
    }

    public readonly record struct InvalidManifestCase (
        string Name,
        int SchemaVersion,
        ProjectFingerprint ProjectFingerprint,
        string SessionToken,
        string EndpointTransportKind,
        string EndpointAddress,
        DateTimeOffset? ExpectedProcessStartedAtUtc);
}
