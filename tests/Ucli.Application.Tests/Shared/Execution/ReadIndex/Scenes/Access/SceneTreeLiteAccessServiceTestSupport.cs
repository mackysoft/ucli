using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

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
        return ProjectContextTestFactory.CreateUnityProject(
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

    public static ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract> CreateSuccessfulSceneTreeLiteLookupReadResult (
        string scenePath = "Assets/Scenes/Main.unity",
        string sourceInputsHash = "scene-hash",
        DateTimeOffset? generatedAtUtc = null,
        IReadOnlyList<IndexSceneTreeLiteNodeJsonContract>? roots = null)
    {
        return ReadIndexArtifactReadResult<IndexSceneTreeLiteLookupJsonContract>.Success(
            new IndexSceneTreeLiteLookupJsonContract(
                SchemaVersion: 1,
                GeneratedAtUtc: generatedAtUtc ?? DateTimeOffset.Parse("2026-04-14T00:00:00+00:00"),
                ScenePath: scenePath,
                SourceInputsHash: sourceInputsHash,
                Roots: roots ?? CreateTree()));
    }

    public static IReadOnlyList<IndexSceneTreeLiteNodeJsonContract> CreateTree ()
    {
        return
        [
            new IndexSceneTreeLiteNodeJsonContract(
                "Root",
                "GlobalObjectId_V1-1-1-1",
                [
                    new IndexSceneTreeLiteNodeJsonContract(
                        "Child",
                        "GlobalObjectId_V1-1-1-2",
                        [
                            new IndexSceneTreeLiteNodeJsonContract(
                                "Grandchild",
                                "GlobalObjectId_V1-1-1-3",
                                Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                                IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                        ],
                        IndexSceneTreeLiteNodeChildrenStateValues.Complete),
                ],
                IndexSceneTreeLiteNodeChildrenStateValues.Complete),
        ];
    }
}
