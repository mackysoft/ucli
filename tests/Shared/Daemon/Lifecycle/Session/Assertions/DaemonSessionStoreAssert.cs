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
        Assert.Equal(expectedUnityProject.ProjectFingerprint, session.ProjectFingerprint);
        Assert.Equal(ContractLiteralCodec.ToValue(expectedEditorMode), session.EditorMode);
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
        Assert.Equal(expectedBaseSession with
        {
            ProcessId = expectedProcessId,
            ProcessStartedAtUtc = expectedProcessStartedAtUtc,
        }, session);
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
