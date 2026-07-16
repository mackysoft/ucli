using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class ReadIndexFreshnessInvocationAssert
{
    public static RecordingReadIndexInputFingerprintProvider.Invocation CoreInputFingerprintComputedOnce (
        RecordingReadIndexInputFingerprintProvider inputProvider,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        return InputFingerprintComputedOnce(
            inputProvider,
            RecordingReadIndexInputFingerprintProvider.InvocationKind.Core,
            expectedUnityProject);
    }

    public static RecordingReadIndexInputFingerprintProvider.Invocation FullInputFingerprintComputedOnce (
        RecordingReadIndexInputFingerprintProvider inputProvider,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        return InputFingerprintComputedOnce(
            inputProvider,
            RecordingReadIndexInputFingerprintProvider.InvocationKind.Full,
            expectedUnityProject);
    }

    public static RecordingReadIndexSceneSourceHashProvider.Invocation SceneSourceHashComputedOnce (
        RecordingReadIndexSceneSourceHashProvider sceneHashProvider,
        ResolvedUnityProjectContext expectedUnityProject,
        SceneAssetPath expectedScenePath)
    {
        var invocation = Assert.Single(sceneHashProvider.Invocations);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedScenePath, invocation.ScenePath);
        return invocation;
    }

    public static RecordingReadIndexFreshnessEvaluator.ObserveInvocation LookupFreshnessObservedOnce (
        RecordingReadIndexFreshnessEvaluator freshnessEvaluator,
        ResolvedUnityProjectContext expectedUnityProject,
        IndexFreshnessTarget expectedTarget,
        Sha256Digest expectedPersistedSourceInputsHash)
    {
        return FreshnessObservedOnce(
            freshnessEvaluator,
            expectedUnityProject,
            expectedTarget,
            expectedPersistedSourceInputsHash);
    }

    public static RecordingReadIndexFreshnessEvaluator.ObserveInvocation FreshnessObservedOnce (
        RecordingReadIndexFreshnessEvaluator freshnessEvaluator,
        ResolvedUnityProjectContext expectedUnityProject,
        IndexFreshnessTarget expectedTarget,
        Sha256Digest expectedPersistedSourceInputsHash)
    {
        var invocation = Assert.Single(freshnessEvaluator.ObserveInvocations);
        Assert.Same(expectedUnityProject, invocation.UnityProject);
        Assert.Equal(expectedTarget, invocation.Target);
        Assert.Equal(expectedPersistedSourceInputsHash, invocation.PersistedSourceInputsHash);
        return invocation;
    }

    private static RecordingReadIndexInputFingerprintProvider.Invocation InputFingerprintComputedOnce (
        RecordingReadIndexInputFingerprintProvider inputProvider,
        RecordingReadIndexInputFingerprintProvider.InvocationKind expectedKind,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        var invocation = Assert.Single(inputProvider.Invocations);
        Assert.Equal(expectedKind, invocation.Kind);
        Assert.Equal(expectedUnityProject, invocation.UnityProject);
        return invocation;
    }
}
