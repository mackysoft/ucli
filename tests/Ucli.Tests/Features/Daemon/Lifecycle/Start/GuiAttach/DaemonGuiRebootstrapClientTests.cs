using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start.GuiAttach;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;

namespace MackySoft.Ucli.Tests.Daemon;

public sealed class DaemonGuiRebootstrapClientTests
{
    private static readonly DateTimeOffset ProcessStartedAtUtc = new(2026, 5, 9, 1, 2, 3, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenManifestMatchesAndSupervisorAccepts_ReturnsAccepted ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope(nameof(RequestRebootstrapAsync_WhenManifestMatchesAndSupervisorAccepts_ReturnsAccepted));
        var unityProject = CreateUnityProject(scope);
        var manifest = CreateManifest();
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (_, request, _, _) => ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                request,
                new IpcGuiRebootstrapResponse(
                    Accepted: true,
                    ProjectFingerprint: unityProject.ProjectFingerprint,
                    ProcessId: manifest.ProcessId))),
        };
        var client = CreateClient(transportClient);

        var timeout = TimeSpan.FromMilliseconds(500);
        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            timeout,
            CancellationToken.None);

        Assert.True(result.IsAccepted);
        var call = Assert.Single(transportClient.Calls);
        Assert.Equal(manifest.SessionToken, call.Request.SessionToken);
        Assert.Equal(IpcMethodNames.GuiRebootstrap, call.Request.Method);
        Assert.Equal(manifest.EndpointAddress, call.Endpoint.Address);
        Assert.Equal(timeout, call.Timeout);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenManifestMissing_ReturnsUnavailableWithoutIpc ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope(nameof(RequestRebootstrapAsync_WhenManifestMissing_ReturnsUnavailableWithoutIpc));
        var unityProject = CreateUnityProject(scope);
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient();
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            1234,
            ProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        Assert.Empty(transportClient.Calls);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenManifestProcessDoesNotMatch_ReturnsUnavailableWithoutIpc ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope(nameof(RequestRebootstrapAsync_WhenManifestProcessDoesNotMatch_ReturnsUnavailableWithoutIpc));
        var unityProject = CreateUnityProject(scope);
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, CreateManifest());
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient();
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            expectedProcessId: 5678,
            ProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        Assert.Empty(transportClient.Calls);
    }

    public static TheoryData<string, int, string, string, string, string, DateTimeOffset?> InvalidManifestCases => new()
    {
        {
            "schema-version",
            0,
            "fingerprint",
            "supervisor-token",
            IpcTransportKindCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            "/tmp/ucli-gui-supervisor.sock",
            ProcessStartedAtUtc
        },
        {
            "project-fingerprint",
            GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            "other-fingerprint",
            "supervisor-token",
            IpcTransportKindCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            "/tmp/ucli-gui-supervisor.sock",
            ProcessStartedAtUtc
        },
        {
            "started-at",
            GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            "fingerprint",
            "supervisor-token",
            IpcTransportKindCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            "/tmp/ucli-gui-supervisor.sock",
            ProcessStartedAtUtc.AddSeconds(1)
        },
        {
            "session-token",
            GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            "fingerprint",
            "",
            IpcTransportKindCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            "/tmp/ucli-gui-supervisor.sock",
            ProcessStartedAtUtc
        },
        {
            "transport-kind",
            GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            "fingerprint",
            "supervisor-token",
            "",
            "/tmp/ucli-gui-supervisor.sock",
            ProcessStartedAtUtc
        },
        {
            "endpoint-address",
            GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            "fingerprint",
            "supervisor-token",
            IpcTransportKindCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            "",
            ProcessStartedAtUtc
        },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidManifestCases))]
    public async Task RequestRebootstrapAsync_WhenManifestValidationFails_ReturnsUnavailableWithoutIpc (
        string testCaseName,
        int schemaVersion,
        string projectFingerprint,
        string sessionToken,
        string endpointTransportKind,
        string endpointAddress,
        DateTimeOffset? expectedProcessStartedAtUtc)
    {
        using var scope = DaemonServiceTestContext.CreateTempScope($"{nameof(RequestRebootstrapAsync_WhenManifestValidationFails_ReturnsUnavailableWithoutIpc)}-{testCaseName}");
        var unityProject = CreateUnityProject(scope);
        var manifest = CreateManifest() with
        {
            SchemaVersion = schemaVersion,
            ProjectFingerprint = projectFingerprint,
            SessionToken = sessionToken,
            EndpointTransportKind = endpointTransportKind,
            EndpointAddress = endpointAddress,
        };
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient();
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            expectedProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        Assert.Empty(transportClient.Calls);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenManifestTransportKindIsInvalid_ReturnsUnavailableWithoutIpc ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope(nameof(RequestRebootstrapAsync_WhenManifestTransportKindIsInvalid_ReturnsUnavailableWithoutIpc));
        var unityProject = CreateUnityProject(scope);
        await WriteManifestAsync(
            scope.FullPath,
            unityProject.ProjectFingerprint,
            CreateManifest() with
            {
                EndpointTransportKind = "invalid-transport",
            });
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient();
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            expectedProcessId: 1234,
            ProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        Assert.Empty(transportClient.Calls);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenSupervisorIsUnreachable_ReturnsUnavailable ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope(nameof(RequestRebootstrapAsync_WhenSupervisorIsUnreachable_ReturnsUnavailable));
        var unityProject = CreateUnityProject(scope);
        var manifest = CreateManifest();
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (_, _, _, _) => throw new IpcConnectTimeoutException("connect timed out"),
        };
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        Assert.Single(transportClient.Calls);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenSupervisorReturnsInvalidPayload_ReturnsUnavailable ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope(nameof(RequestRebootstrapAsync_WhenSupervisorReturnsInvalidPayload_ReturnsUnavailable));
        var unityProject = CreateUnityProject(scope);
        var manifest = CreateManifest();
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (_, request, _, _) => ValueTask.FromResult(DaemonServiceTestContext.CreateSuccessResponse(
                request,
                new IpcGuiRebootstrapResponse(
                    Accepted: true,
                    ProjectFingerprint: "other-fingerprint",
                    ProcessId: manifest.ProcessId))),
        };
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        Assert.Single(transportClient.Calls);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RequestRebootstrapAsync_WhenSupervisorReturnsError_ReturnsUnavailable ()
    {
        using var scope = DaemonServiceTestContext.CreateTempScope(nameof(RequestRebootstrapAsync_WhenSupervisorReturnsError_ReturnsUnavailable));
        var unityProject = CreateUnityProject(scope);
        var manifest = CreateManifest();
        await WriteManifestAsync(scope.FullPath, unityProject.ProjectFingerprint, manifest);
        var transportClient = new DaemonServiceTestContext.StubIpcTransportClient
        {
            SendHandler = (_, request, _, _) => ValueTask.FromResult(DaemonServiceTestContext.CreateErrorResponse(
                request,
                UcliCoreErrorCodes.InternalError,
                "rebootstrap failed")),
        };
        var client = CreateClient(transportClient);

        var result = await client.RequestRebootstrapAsync(
            unityProject,
            manifest.ProcessId,
            ProcessStartedAtUtc,
            TimeSpan.FromMilliseconds(500),
            CancellationToken.None);

        Assert.False(result.IsAccepted);
        Assert.Equal(DaemonErrorCodes.DaemonEndpointNotRegistered, result.Error!.Code);
        Assert.Single(transportClient.Calls);
    }

    private static DaemonGuiRebootstrapClient CreateClient (DaemonServiceTestContext.StubIpcTransportClient transportClient)
    {
        return new DaemonGuiRebootstrapClient(new GuiSupervisorManifestStore(), transportClient);
    }

    private static ResolvedUnityProjectContext CreateUnityProject (TestDirectoryScope scope)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: Path.Combine(scope.FullPath, "UnityProject"),
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: "fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static GuiSupervisorManifestJsonContract CreateManifest ()
    {
        return new GuiSupervisorManifestJsonContract(
            SchemaVersion: GuiSupervisorManifestJsonContract.CurrentSchemaVersion,
            SessionToken: "supervisor-token",
            ProjectFingerprint: "fingerprint",
            EndpointTransportKind: IpcTransportKindCodec.ToValue(IpcTransportKind.UnixDomainSocket),
            EndpointAddress: "/tmp/ucli-gui-supervisor.sock",
            ProcessId: 1234,
            ProcessStartedAtUtc: ProcessStartedAtUtc,
            IssuedAtUtc: new DateTimeOffset(2026, 5, 9, 1, 2, 4, TimeSpan.Zero));
    }

    private static async Task WriteManifestAsync (
        string storageRoot,
        string projectFingerprint,
        GuiSupervisorManifestJsonContract manifest)
    {
        var manifestPath = UcliStoragePathResolver.ResolveGuiSupervisorManifestPath(storageRoot, projectFingerprint);
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
        await File.WriteAllTextAsync(
            manifestPath,
            JsonSerializer.Serialize(manifest, IpcJsonSerializerOptions.Default));
    }
}
