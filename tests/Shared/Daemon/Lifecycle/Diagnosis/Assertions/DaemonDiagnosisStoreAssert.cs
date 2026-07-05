using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;

namespace MackySoft.Ucli.TestSupport;

internal static class DaemonDiagnosisStoreAssert
{
    public static DaemonDiagnosis WrittenOnce (RecordingDaemonDiagnosisStore diagnosisStore)
    {
        return Assert.Single(diagnosisStore.WriteInvocations).Diagnosis;
    }

    public static DaemonDiagnosis WrittenOnceWithReason (
        RecordingDaemonDiagnosisStore diagnosisStore,
        string expectedReason)
    {
        var diagnosis = WrittenOnce(diagnosisStore);
        Assert.Equal(expectedReason, diagnosis.Reason);
        return diagnosis;
    }

    public static DaemonDiagnosis DiagnosisWrittenFor (
        RecordingDaemonDiagnosisStore diagnosisStore,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        var invocation = Assert.Single(diagnosisStore.WriteInvocations);
        AssertWriteTarget(invocation, expectedUnityProject);
        return invocation.Diagnosis;
    }

    public static DaemonDiagnosis LatestDiagnosisWrittenFor (
        RecordingDaemonDiagnosisStore diagnosisStore,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        Assert.NotEmpty(diagnosisStore.WriteInvocations);
        var invocation = diagnosisStore.WriteInvocations[^1];
        AssertWriteTarget(invocation, expectedUnityProject);
        return invocation.Diagnosis;
    }

    public static void DeleteAttemptedFor (
        RecordingDaemonDiagnosisStore diagnosisStore,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        var invocation = Assert.Single(diagnosisStore.DeleteInvocations);
        Assert.Equal(expectedUnityProject.RepositoryRoot, invocation.StorageRoot);
        Assert.Equal(expectedUnityProject.ProjectFingerprint, invocation.ProjectFingerprint);
    }

    private static void AssertWriteTarget (
        RecordingDaemonDiagnosisStore.WriteInvocation invocation,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        Assert.Equal(expectedUnityProject.RepositoryRoot, invocation.StorageRoot);
        Assert.Equal(expectedUnityProject.ProjectFingerprint, invocation.ProjectFingerprint);
    }
}
