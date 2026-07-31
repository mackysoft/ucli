using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Application.Tests;

internal static class MutationReadPostconditionStoreAssert
{
    public static TestMutationReadPostconditionStore.WriteInvocation WrittenOnceForProject (
        TestMutationReadPostconditionStore store,
        string expectedStorageRoot,
        ProjectFingerprint expectedProjectFingerprint)
    {
        var invocation = Assert.Single(store.WriteInvocations);
        Assert.Equal(expectedStorageRoot, invocation.StorageRoot.Value);
        Assert.Equal(expectedProjectFingerprint, invocation.ProjectFingerprint);
        return invocation;
    }

    public static ExecutionReadPostconditionRequirement WrittenSceneTreeLiteRequirement (
        TestMutationReadPostconditionStore store,
        string expectedStorageRoot,
        ProjectFingerprint expectedProjectFingerprint,
        string expectedScenePath)
    {
        var invocation = WrittenOnceForProject(store, expectedStorageRoot, expectedProjectFingerprint);
        var requirement = Assert.Single(invocation.ReadPostcondition.Requirements);
        Assert.Equal(ExecutionReadPostconditionSurface.SceneTreeLite, requirement.Surface);
        Assert.Equal(new UnityScenePath(expectedScenePath), requirement.ScenePath);
        return requirement;
    }

    public static ExecutionReadPostconditionRequirement WrittenAssetSearchRequirement (
        TestMutationReadPostconditionStore store,
        string expectedStorageRoot,
        ProjectFingerprint expectedProjectFingerprint,
        DateTimeOffset expectedMinSafeGeneratedAtUtc)
    {
        var invocation = WrittenOnceForProject(store, expectedStorageRoot, expectedProjectFingerprint);
        var requirement = Assert.Single(invocation.ReadPostcondition.Requirements);
        Assert.Equal(ExecutionReadPostconditionSurface.AssetSearch, requirement.Surface);
        Assert.Equal(expectedMinSafeGeneratedAtUtc, requirement.MinSafeGeneratedAtUtc);
        return requirement;
    }
}
