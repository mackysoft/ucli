using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Shared.Context;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonSessionStoreAssert
{
    public static RecordingDaemonSessionStore.ReadInvocation SessionReadRequestedFor (
        RecordingDaemonSessionStore sessionStore,
        ProjectContext expectedContext)
    {
        var invocation = Assert.Single(sessionStore.ReadInvocations);
        Assert.Equal(expectedContext.UnityProject.RepositoryRoot, invocation.StorageRoot);
        Assert.Equal(expectedContext.UnityProject.ProjectFingerprint, invocation.ProjectFingerprint);
        return invocation;
    }

    public static DaemonSession InitialSessionWrittenFor (
        RecordingDaemonSessionStore sessionStore,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonEditorMode expectedEditorMode)
    {
        var session = SingleSessionWrittenFor(sessionStore, expectedUnityProject);
        Assert.NotEqual(Guid.Empty, session.SessionGenerationId);
        Assert.Equal(expectedUnityProject.ProjectFingerprint, session.ProjectFingerprint);
        Assert.Equal(expectedEditorMode, session.EditorMode);
        Assert.Null(session.ProcessId);
        Assert.Null(session.ProcessStartedAtUtc);
        return session;
    }

    public static DaemonSession ProcessIdentityWrittenFor (
        RecordingDaemonSessionStore sessionStore,
        ResolvedUnityProjectContext expectedUnityProject,
        DaemonSession expectedBaseSession,
        int expectedProcessId,
        DateTimeOffset expectedProcessStartedAtUtc)
    {
        var session = SingleSessionWrittenFor(sessionStore, expectedUnityProject);
        Assert.Equal(expectedBaseSession.SessionGenerationId, session.SessionGenerationId);
        Assert.Equal(expectedBaseSession.SessionToken, session.SessionToken);
        Assert.Equal(expectedBaseSession.ProjectFingerprint, session.ProjectFingerprint);
        Assert.Equal(expectedBaseSession.IssuedAtUtc, session.IssuedAtUtc);
        Assert.Equal(expectedBaseSession.EditorMode, session.EditorMode);
        Assert.Equal(expectedBaseSession.OwnerKind, session.OwnerKind);
        Assert.Equal(expectedBaseSession.CanShutdownProcess, session.CanShutdownProcess);
        Assert.Equal(expectedBaseSession.Endpoint, session.Endpoint);
        Assert.Equal(expectedProcessId, session.ProcessId);
        Assert.Equal(expectedProcessStartedAtUtc, session.ProcessStartedAtUtc);
        Assert.Equal(expectedBaseSession.OwnerProcessId, session.OwnerProcessId);
        Assert.Equal(expectedBaseSession.EditorInstanceId, session.EditorInstanceId);
        return session;
    }

    private static DaemonSession SingleSessionWrittenFor (
        RecordingDaemonSessionStore sessionStore,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        var invocation = Assert.Single(sessionStore.WriteInvocations);
        Assert.Equal(expectedUnityProject.RepositoryRoot, invocation.StorageRoot);
        return invocation.Session;
    }
}
