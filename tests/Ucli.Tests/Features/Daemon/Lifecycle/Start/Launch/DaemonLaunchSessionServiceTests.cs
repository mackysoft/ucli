using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
namespace MackySoft.Ucli.Tests.Daemon;

using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Tests.Helpers.Daemon;

public sealed class DaemonLaunchSessionServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Initialize_WhenSessionWriteSucceeds_UsesInjectedGenerationIdentityAndTime ()
    {
        var expectedSessionGenerationId = Guid.Parse("00000001-0000-0000-0000-000000000000");
        var expectedIssuedAtUtc = new DateTimeOffset(2026, 7, 15, 3, 4, 5, TimeSpan.Zero);
        var sessionStore = new RecordingDaemonSessionStore();
        var service = new DaemonLaunchSessionService(
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StaticDaemonSessionTokenGenerator(),
            sessionGenerationIdGenerator: new SequentialGuidGenerator(),
            timeProvider: new ManualTimeProvider(expectedIssuedAtUtc));
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-session-init"));

        var result = await service.InitializeAsync(context, DaemonEditorMode.Batchmode, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var session = Assert.IsType<DaemonSession>(result.Session);
        Assert.Equal(expectedSessionGenerationId, session.SessionGenerationId);
        Assert.Equal(expectedIssuedAtUtc, session.IssuedAtUtc);
        var writtenSession = DaemonSessionStoreAssert.InitialSessionWrittenFor(sessionStore, context, DaemonEditorMode.Batchmode);
        Assert.Equal(session, writtenSession);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Initialize_WhenSessionWriteFails_ReturnsFailure ()
    {
        var expectedError = ExecutionError.InternalError("initial write failed");
        var sessionStore = new RecordingDaemonSessionStore
        {
            WriteResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var service = new DaemonLaunchSessionService(
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StaticDaemonSessionTokenGenerator(),
            sessionGenerationIdGenerator: new SequentialGuidGenerator(),
            timeProvider: TimeProvider.System);
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-session-init-fail"));

        var result = await service.InitializeAsync(context, DaemonEditorMode.Batchmode, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        DaemonSessionStoreAssert.InitialSessionWrittenFor(sessionStore, context, DaemonEditorMode.Batchmode);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Initialize_WhenEditorModeIsGui_WritesGuiSession ()
    {
        var sessionStore = new RecordingDaemonSessionStore();
        var service = new DaemonLaunchSessionService(
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StaticDaemonSessionTokenGenerator(),
            sessionGenerationIdGenerator: new SequentialGuidGenerator(),
            timeProvider: TimeProvider.System);
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-session-gui"));

        var result = await service.InitializeAsync(context, DaemonEditorMode.Gui, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var writtenSession = DaemonSessionStoreAssert.InitialSessionWrittenFor(sessionStore, context, DaemonEditorMode.Gui);
        Assert.Equal(result.Session, writtenSession);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateProcessId_WhenProcessIdIsNull_ReturnsOriginalSessionWithoutWrite ()
    {
        var sessionStore = new UnexpectedDaemonSessionStore("Missing launched process id should return the original session without writing.");
        var service = new DaemonLaunchSessionService(
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StaticDaemonSessionTokenGenerator(),
            sessionGenerationIdGenerator: new SequentialGuidGenerator(),
            timeProvider: TimeProvider.System);
        var session = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: "session-token",
            endpointAddress: "ucli-daemon-test-endpoint");

        var result = await service.UpdateProcessIdAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-session-no-update")),
            session,
            processId: null,
            processStartedAtUtc: null,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(session, result.Session);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateProcessId_WhenProcessIdWriteFails_ReturnsFailure ()
    {
        var expectedError = ExecutionError.InternalError("update write failed");
        var sessionStore = new RecordingDaemonSessionStore
        {
            WriteResult = DaemonSessionStoreOperationResult.Failure(expectedError),
        };
        var service = new DaemonLaunchSessionService(
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StaticDaemonSessionTokenGenerator(),
            sessionGenerationIdGenerator: new SequentialGuidGenerator(),
            timeProvider: TimeProvider.System);
        var session = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: "session-token",
            endpointAddress: "ucli-daemon-test-endpoint");
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-session-update-fail"));
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero);

        var result = await service.UpdateProcessIdAsync(
            context,
            session,
            processId: 4321,
            processStartedAtUtc: processStartedAtUtc,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expectedError, result.Error);
        DaemonSessionStoreAssert.ProcessIdentityWrittenFor(sessionStore, context, session, 4321, processStartedAtUtc);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateProcessId_WhenProcessStartedAtUtcIsMissing_ReturnsFailureWithoutWrite ()
    {
        var sessionStore = new UnexpectedDaemonSessionStore("Missing process start time should fail before writing the session.");
        var service = new DaemonLaunchSessionService(
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StaticDaemonSessionTokenGenerator(),
            sessionGenerationIdGenerator: new SequentialGuidGenerator(),
            timeProvider: TimeProvider.System);
        var session = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: "session-token",
            endpointAddress: "ucli-daemon-test-endpoint");

        var result = await service.UpdateProcessIdAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-session-update-missing-start")),
            session,
            processId: 4321,
            processStartedAtUtc: null,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("processStartedAtUtc", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task UpdateProcessId_WhenProcessIdWriteSucceeds_ReturnsUpdatedSession ()
    {
        var sessionStore = new RecordingDaemonSessionStore();
        var service = new DaemonLaunchSessionService(
            daemonSessionStore: sessionStore,
            sessionTokenGenerator: new StaticDaemonSessionTokenGenerator(),
            sessionGenerationIdGenerator: new SequentialGuidGenerator(),
            timeProvider: TimeProvider.System);
        var session = DaemonSessionTestFactory.Create(
            processId: null,
            sessionToken: "session-token",
            endpointAddress: "ucli-daemon-test-endpoint");
        var processStartedAtUtc = new DateTimeOffset(2026, 03, 12, 0, 0, 0, TimeSpan.Zero);
        var context = ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext(ProjectFingerprintTestFactory.Create("fingerprint-session-update-success"));

        var result = await service.UpdateProcessIdAsync(
            context,
            session,
            processId: 8765,
            processStartedAtUtc: processStartedAtUtc,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var writtenSession = DaemonSessionStoreAssert.ProcessIdentityWrittenFor(sessionStore, context, session, 8765, processStartedAtUtc);
        Assert.Equal(result.Session, writtenSession);
    }

}
