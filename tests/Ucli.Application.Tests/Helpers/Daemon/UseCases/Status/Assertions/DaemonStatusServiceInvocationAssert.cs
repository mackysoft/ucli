using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonStatusServiceInvocationAssert
{
    public static void FreshLifecycleSidecarUsed (
        RecordingDaemonLifecycleStore lifecycleStore,
        RecordingDaemonProcessIdentityAssessor processIdentityAssessor,
        DaemonCommandExecutionContext context,
        DaemonSession session)
    {
        LifecycleSidecarRead(lifecycleStore, context);

        var invocation = Assert.Single(processIdentityAssessor.Invocations);
        Assert.Equal(session.ProcessId, invocation.ProcessId);
        Assert.Equal(session.ProcessStartedAtUtc, invocation.ExpectedProcessStartedAtUtc);
    }

    public static void LifecycleSidecarReadWithoutProcessIdentityAssessment (
        RecordingDaemonLifecycleStore lifecycleStore,
        RecordingDaemonProcessIdentityAssessor processIdentityAssessor,
        DaemonCommandExecutionContext context)
    {
        LifecycleSidecarRead(lifecycleStore, context);
        Assert.Empty(processIdentityAssessor.Invocations);
    }

    public static void StaleDiagnosisResolved (
        RecordingDaemonSessionDiagnosisResolver diagnosisResolver,
        DaemonCommandExecutionContext context,
        DaemonSession session,
        DaemonDiagnosis? persistedDiagnosis)
    {
        var invocation = Assert.Single(diagnosisResolver.Invocations);
        Assert.Equal(context.Context.UnityProject, invocation.UnityProject);
        Assert.Equal(session, invocation.Session);
        Assert.Equal(persistedDiagnosis, invocation.PersistedDiagnosis);
    }

    public static void StatusCommandResolvedAndOperationExecuted (
        RecordingDaemonCommandExecutionContextResolver resolver,
        RecordingDaemonStatusOperation daemonStatusOperation,
        DaemonCommandExecutionContext context,
        string? expectedProjectPath,
        int? expectedTimeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var resolverInvocation = Assert.Single(resolver.Invocations);
        Assert.Equal(UcliCommandIds.DaemonStatus, resolverInvocation.TimeoutCommand);
        Assert.Equal(expectedProjectPath, resolverInvocation.ProjectPath);
        Assert.Equal(expectedTimeoutMilliseconds, resolverInvocation.TimeoutMilliseconds);
        Assert.Equal(cancellationToken, resolverInvocation.CancellationToken);

        var operationInvocation = Assert.Single(daemonStatusOperation.Invocations);
        Assert.Equal(context.Context.UnityProject, operationInvocation.UnityProject);
        Assert.Equal(context.Timeout, operationInvocation.Timeout);
        Assert.Equal(cancellationToken, operationInvocation.CancellationToken);
    }

    public static void DaemonPingTelemetryRead (
        RecordingDaemonPingInfoClient pingInfoClient,
        DaemonCommandExecutionContext context,
        TimeSpan? expectedTimeout,
        string expectedSessionToken)
    {
        var invocation = Assert.Single(pingInfoClient.Invocations);
        Assert.Equal(context.Context.UnityProject, invocation.UnityProject);
        if (expectedTimeout.HasValue)
        {
            Assert.Equal(expectedTimeout.Value, invocation.Timeout);
        }

        Assert.Equal(expectedSessionToken, invocation.SessionToken);
        Assert.True(invocation.ValidateProjectFingerprint);
    }

    public static void TimeoutReturnedBeforePingTelemetryRead (
        DaemonStatusExecutionResult result,
        RecordingDaemonPingInfoClient pingInfoClient)
    {
        var error = AssertFailure(result, ExecutionErrorKind.Timeout);
        Assert.Equal("Timed out before daemon ping information read could begin.", error.Message);
        Assert.Empty(pingInfoClient.Invocations);
    }

    public static void TimeoutReturnedBeforeStaleDiagnosisResolution (
        DaemonStatusExecutionResult result,
        RecordingDaemonSessionDiagnosisResolver diagnosisResolver)
    {
        var error = AssertFailure(result, ExecutionErrorKind.Timeout);
        Assert.Equal("Timed out before stale daemon diagnosis could begin.", error.Message);
        Assert.Empty(diagnosisResolver.Invocations);
    }

    public static void StaleDiagnosisResolutionTimedOut (
        DaemonStatusExecutionResult result,
        RecordingDaemonSessionDiagnosisResolver diagnosisResolver,
        DaemonCommandExecutionContext context,
        DaemonSession session)
    {
        var error = AssertFailure(result, ExecutionErrorKind.Timeout);
        Assert.Equal("Timed out while resolving stale daemon diagnosis.", error.Message);
        StaleDiagnosisResolutionAttempted(diagnosisResolver, context, session, persistedDiagnosis: null);
    }

    public static void StaleDiagnosisResolutionFailed (
        DaemonStatusExecutionResult result,
        RecordingDaemonSessionDiagnosisResolver diagnosisResolver,
        DaemonCommandExecutionContext context,
        DaemonSession session)
    {
        var error = AssertFailure(result, ExecutionErrorKind.InternalError);
        Assert.Equal("Failed to resolve stale daemon diagnosis. diagnosis store failed", error.Message);
        StaleDiagnosisResolutionAttempted(diagnosisResolver, context, session, persistedDiagnosis: null);
    }

    private static void LifecycleSidecarRead (
        RecordingDaemonLifecycleStore lifecycleStore,
        DaemonCommandExecutionContext context)
    {
        var invocation = Assert.Single(lifecycleStore.ReadInvocations);
        Assert.Equal(context.Context.UnityProject.RepositoryRoot, invocation.StorageRoot);
        Assert.Equal(context.Context.UnityProject.ProjectFingerprint, invocation.ProjectFingerprint);
    }

    private static void StaleDiagnosisResolutionAttempted (
        RecordingDaemonSessionDiagnosisResolver diagnosisResolver,
        DaemonCommandExecutionContext context,
        DaemonSession session,
        DaemonDiagnosis? persistedDiagnosis)
    {
        var invocation = Assert.Single(diagnosisResolver.Invocations);
        Assert.Equal(context.Context.UnityProject, invocation.UnityProject);
        Assert.Equal(session, invocation.Session);
        Assert.Equal(persistedDiagnosis, invocation.PersistedDiagnosis);
    }

    private static ExecutionError AssertFailure (
        DaemonStatusExecutionResult result,
        ExecutionErrorKind expectedKind)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Output);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(expectedKind, error.Kind);
        return error;
    }
}
