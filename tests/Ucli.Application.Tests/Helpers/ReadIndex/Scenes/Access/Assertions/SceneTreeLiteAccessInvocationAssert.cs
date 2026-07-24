using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class SceneTreeLiteAccessInvocationAssert
{
    public static void DirtyLoadedSourceReturnedBeforeIndexLookup (
        SceneTreeLiteReadResult result,
        RecordingReadIndexArtifactReader indexReader,
        RecordingSceneTreeLiteDirtySourceProbeService dirtySourceProbeService,
        ResolvedUnityProjectContext expectedProject,
        UcliCommand expectedCommand,
        UnityExecutionMode expectedMode,
        string expectedScenePath,
        string expectedRootName)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        Assert.Equal(SceneTreeSourceStateKind.LoadedScene, result.Output.SourceState.Kind);
        Assert.True(result.Output.SourceState.IsDirty);
        Assert.Equal(expectedRootName, result.Output.Roots[0].Name);
        DirtySourceProbeAttemptedFor(
            dirtySourceProbeService,
            expectedProject,
            expectedCommand,
            expectedMode,
            expectedScenePath);
        Assert.Empty(indexReader.ReadInvocations);
    }

    public static void SourceRefreshReturnedWithoutIndexLookup (
        SceneTreeLiteReadResult result,
        RecordingReadIndexArtifactReader indexReader,
        RecordingSceneTreeLiteSourceRefreshService refreshService,
        ResolvedUnityProjectContext expectedProject,
        UcliCommand expectedCommand,
        UnityExecutionMode expectedMode,
        string expectedScenePath,
        string expectedFallbackReasonFragment,
        bool? expectedFailFast = null)
    {
        Assert.True(result.IsSuccess);
        Assert.Equal(SceneTreeLiteSource.Source, result.Output!.AccessInfo.Source);
        Assert.Contains(expectedFallbackReasonFragment, result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Empty(indexReader.ReadInvocations);
        SourceRefreshAttemptedFor(
            refreshService,
            expectedProject,
            expectedCommand,
            expectedMode,
            expectedScenePath,
            expectedFailFast);
    }

    public static void InvalidSceneRejectedBeforeIndexLookup (
        SceneTreeLiteReadResult result,
        RecordingReadIndexArtifactReader indexReader,
        string expectedMessageFragment)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Contains(expectedMessageFragment, result.Message, StringComparison.Ordinal);
        Assert.Empty(indexReader.ReadInvocations);
    }

    public static void FreshnessObservedFor (
        RecordingReadIndexFreshnessEvaluator freshnessEvaluator,
        ResolvedUnityProjectContext expectedUnityProject,
        string expectedScenePath,
        Sha256Digest expectedPersistedSourceInputsHash)
    {
        var invocation = Assert.Single(freshnessEvaluator.SceneTreeLiteObserveInvocations);
        Assert.Same(expectedUnityProject.UnityProjectRoot, invocation.SourcePaths.SceneFilePath.BoundaryRoot);
        Assert.Equal(new SceneAssetPath(expectedScenePath), invocation.SourcePaths.SceneAssetPath);
        Assert.Equal(expectedScenePath, invocation.SourcePaths.SceneFilePath.RelativePath.Value);
        Assert.Equal(expectedScenePath + ".meta", invocation.SourcePaths.MetaFilePath.RelativePath.Value);
        Assert.Equal(expectedPersistedSourceInputsHash, invocation.PersistedSourceInputsHash);
    }

    public static RecordingSceneTreeLiteDirtySourceProbeService.Invocation DirtySourceProbeAttemptedFor (
        RecordingSceneTreeLiteDirtySourceProbeService dirtySourceProbeService,
        ResolvedUnityProjectContext expectedProject,
        UcliCommand expectedCommand,
        UnityExecutionMode expectedMode,
        string expectedScenePath)
    {
        var invocation = Assert.Single(dirtySourceProbeService.Invocations);
        Assert.Same(expectedProject, invocation.Project);
        Assert.Equal(expectedCommand, invocation.Command);
        Assert.Equal(expectedMode, invocation.Mode);
        Assert.Equal(expectedScenePath, invocation.ScenePath.Value);
        return invocation;
    }

    public static RecordingReadIndexArtifactReader.ReadInvocation SceneTreeLiteLookupReadFor (
        RecordingReadIndexArtifactReader artifactReader,
        ResolvedUnityProjectContext expectedUnityProject)
    {
        var invocation = Assert.Single(artifactReader.ReadInvocations);
        Assert.Equal(RecordingReadIndexArtifactReader.ReadIndexArtifactKind.SceneTreeLiteLookup, invocation.Kind);
        Assert.Same(expectedUnityProject, invocation.UnityProject);
        return invocation;
    }

    public static RecordingSceneTreeLiteSourceRefreshService.Invocation SourceRefreshAttemptedFor (
        RecordingSceneTreeLiteSourceRefreshService refreshService,
        ResolvedUnityProjectContext expectedProject,
        UcliCommand expectedCommand,
        UnityExecutionMode expectedMode,
        string expectedScenePath,
        bool? expectedFailFast = null)
    {
        var invocation = Assert.Single(refreshService.Invocations);
        Assert.Same(expectedProject, invocation.Project);
        Assert.Equal(expectedCommand, invocation.Command);
        Assert.Equal(expectedMode, invocation.Mode);
        Assert.Equal(expectedScenePath, invocation.ScenePath.Value);
        if (SceneAssetPath.TryParse(expectedScenePath, out var expectedIndexScenePath))
        {
            Assert.NotNull(invocation.IndexSourcePaths);
            Assert.Equal(expectedIndexScenePath, invocation.IndexSourcePaths.SceneAssetPath);
            Assert.Same(expectedProject.UnityProjectRoot, invocation.IndexSourcePaths.SceneFilePath.BoundaryRoot);
            Assert.Equal(expectedScenePath, invocation.IndexSourcePaths.SceneFilePath.RelativePath.Value);
            Assert.Equal(expectedScenePath + ".meta", invocation.IndexSourcePaths.MetaFilePath.RelativePath.Value);
        }
        else
        {
            Assert.Null(invocation.IndexSourcePaths);
        }

        if (expectedFailFast.HasValue)
        {
            Assert.Equal(expectedFailFast.Value, invocation.FailFast);
        }

        return invocation;
    }
}
