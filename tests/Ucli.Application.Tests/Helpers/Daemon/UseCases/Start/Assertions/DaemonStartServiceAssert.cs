using MackySoft.FileSystem;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonStartServiceAssert
{
    public static void StartNotAttemptedAfterContextResolutionFailure (
        RecordingDaemonProjectLifecycleGateway supervisorProjectGateway,
        CollectingCommandProgressSink progressSink)
    {
        Assert.Empty(supervisorProjectGateway.EnsureRunningInvocations);
        Assert.Empty(progressSink.Entries);
    }

    public static void PluginVerificationFailureStoppedBeforeSupervisorBootstrap (
        RecordingUnityPluginVerifier pluginVerifier,
        RecordingDaemonProjectLifecycleGateway supervisorProjectGateway,
        AbsolutePath expectedUnityProjectRoot)
    {
        UnityPluginVerifierAssert.VerificationRequestedFor(pluginVerifier, expectedUnityProjectRoot);
        Assert.Empty(supervisorProjectGateway.EnsureRunningInvocations);
    }

    public static void PluginVerificationTimeoutStoppedBeforeSupervisorBootstrap (
        RecordingUnityPluginVerifier pluginVerifier,
        RecordingDaemonProjectLifecycleGateway supervisorProjectGateway)
    {
        Assert.True(pluginVerifier.ObservedCancellation);
        Assert.Empty(supervisorProjectGateway.EnsureRunningInvocations);
    }
}
