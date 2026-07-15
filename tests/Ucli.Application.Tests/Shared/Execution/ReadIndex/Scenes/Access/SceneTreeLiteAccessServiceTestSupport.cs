using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Execution.ReadIndex.Scenes;

internal static class SceneTreeLiteAccessServiceTestSupport
{
    public static SceneTreeLiteAccessService CreateService (
        IReadIndexArtifactReader artifactReader,
        IReadIndexFreshnessEvaluator freshnessEvaluator,
        IMutationReadPostconditionStore mutationReadPostconditionStore,
        ISceneTreeLiteSourceRefreshService sourceRefreshService,
        ISceneTreeLiteSourceProbe sourceProbe,
        ISceneTreeLiteDirtySourceProbeService? dirtySourceProbeService = null)
    {
        return new SceneTreeLiteAccessService(
            artifactReader,
            freshnessEvaluator,
            mutationReadPostconditionStore,
            sourceRefreshService,
            sourceProbe,
            dirtySourceProbeService ?? new RecordingSceneTreeLiteDirtySourceProbeService());
    }

    public static ResolvedUnityProjectContext CreateProject (TestDirectoryScope scope)
    {
        var projectRoot = scope.CreateDirectory("UnityProject");
        return ProjectContextTestFactory.CreateUnityProjectWithPaths(
            unityProjectRoot: projectRoot,
            repositoryRoot: scope.FullPath,
            pathSourceLabel: null,
            unityVersion: ProjectIdentityDefaults.UnknownUnityVersion);
    }

    public static void WriteSceneFile (
        string projectRootPath,
        string scenePath)
    {
        var absolutePath = Path.Combine(projectRootPath, scenePath.Replace('/', Path.DirectorySeparatorChar));
        var directoryPath = Path.GetDirectoryName(absolutePath)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {absolutePath}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(absolutePath, "scene");
    }

    public static ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot> CreateSuccessfulSceneTreeLiteLookupReadResult (
        string scenePath = "Assets/Scenes/Main.unity",
        Sha256Digest? sourceInputsHash = null,
        DateTimeOffset? generatedAtUtc = null,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? roots = null)
    {
        var resolvedGeneratedAtUtc = generatedAtUtc ?? DateTimeOffset.Parse("2026-04-14T00:00:00+00:00");
        var resolvedSourceInputsHash = sourceInputsHash ?? Sha256DigestTestFactory.Compute("scene-hash");
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: resolvedGeneratedAtUtc,
            ScenePath: scenePath,
            SourceInputsHash: resolvedSourceInputsHash.ToString(),
            Roots: roots ?? CreateTree());
        if (!SceneTreeLiteLookupSnapshot.TryCreate(contract, out var snapshot))
        {
            throw new InvalidOperationException("Scene-tree-lite fixture is invalid.");
        }

        return ReadIndexArtifactReadResult<SceneTreeLiteLookupSnapshot>.Success(snapshot);
    }

    public static SceneTreeLiteSourceSnapshot CreateSceneTreeLiteSourceSnapshot (
        DateTimeOffset generatedAtUtc,
        string scenePath,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> roots,
        SceneTreeSourceState sourceState)
    {
        return ReadIndexTypedValueTestFactory.CreateSceneTreeSourceSnapshot(
            new IpcIndexSceneTreeLiteReadResponse(
                generatedAtUtc,
                new UnityScenePath(scenePath),
                roots,
                sourceState));
    }

    public static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> CreateTree ()
    {
        return
        [
            new IndexSceneTreeLiteNodeJsonContract(
                "Root",
                "GlobalObjectId_V1-2-11111111111111111111111111111111-1-0",
                [
                    new IndexSceneTreeLiteNodeJsonContract(
                        "Child",
                        "GlobalObjectId_V1-2-11111111111111111111111111111111-2-0",
                        [
                            new IndexSceneTreeLiteNodeJsonContract(
                                "Grandchild",
                                "GlobalObjectId_V1-2-11111111111111111111111111111111-3-0",
                                Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                                IndexSceneTreeLiteNodeChildrenState.Complete),
                        ],
                        IndexSceneTreeLiteNodeChildrenState.Complete),
                ],
                IndexSceneTreeLiteNodeChildrenState.Complete),
        ];
    }
}
