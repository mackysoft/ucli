using System.Text;
using MackySoft.Tests;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;

namespace MackySoft.Ucli.Tests.Index;

public sealed class FileReadIndexArtifactReaderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOpsCatalog_ReturnsContract_WhenCatalogExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-success");
        var reader = new FileReadIndexArtifactReader();
        const string fingerprint = "fingerprint";
        var project = CreateProject(scope, fingerprint);
        var contract = new IndexOpsCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexOpsCatalogEntryJsonContract(
                    Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe,
                    Kind: "query",
                    Policy: "safe",
                    Description: "Returns a GameObject description.",
                    DescribeKey: new string('a', 64),
                    DescribeHash: new string('b', 64)),
            ]);
        WriteText(UcliStoragePathResolver.ResolveOpsCatalogPath(scope.FullPath, fingerprint), Write(contract));

        var result = await reader.ReadOpsCatalogAsync(project, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.SchemaVersion);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Equal("Returns a GameObject description.", result.Value.Entries[0].Description);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOpsDescribe_ReturnsContract_WhenDetailExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-describe-success");
        var reader = new FileReadIndexArtifactReader();
        const string fingerprint = "fingerprint";
        const string sourceInputsHash = "source-hash";
        var project = CreateProject(scope, fingerprint);
        var operation = CreateGoDescribeEntry();
        var catalogEntry = WriteOpsDescribe(scope.FullPath, fingerprint, operation, sourceInputsHash);

        var result = await reader.ReadOpsDescribeAsync(project, catalogEntry, sourceInputsHash, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(sourceInputsHash, result.Value.SourceInputsHash);
        Assert.Equal(UcliPrimitiveOperationNames.GoDescribe, result.Value.Operation!.Name);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOpsDescribe_ReturnsBootstrapFailed_WhenDetailDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-describe-missing");
        var reader = new FileReadIndexArtifactReader();
        const string fingerprint = "fingerprint";
        var project = CreateProject(scope, fingerprint);
        var catalogEntry = new IndexOpsCatalogEntryJsonContract(
            UcliPrimitiveOperationNames.GoDescribe,
            "query",
            "safe",
            "Returns a GameObject description.",
            new string('a', 64),
            new string('b', 64));

        var result = await reader.ReadOpsDescribeAsync(project, catalogEntry, "source-hash", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexBootstrapFailed, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOpsDescribe_ReturnsFormatInvalid_WhenDescribeHashDoesNotMatch ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-describe-hash-mismatch");
        var reader = new FileReadIndexArtifactReader();
        const string fingerprint = "fingerprint";
        var project = CreateProject(scope, fingerprint);
        var operation = CreateGoDescribeEntry();
        var catalogEntry = WriteOpsDescribe(scope.FullPath, fingerprint, operation, "source-hash");
        catalogEntry = catalogEntry with { DescribeHash = new string('0', 64) };

        var result = await reader.ReadOpsDescribeAsync(project, catalogEntry, "source-hash", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
        Assert.Contains("describeHash", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOpsDescribe_ReturnsFormatInvalid_WhenSourceInputsHashDoesNotMatch ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-describe-source-mismatch");
        var reader = new FileReadIndexArtifactReader();
        const string fingerprint = "fingerprint";
        var project = CreateProject(scope, fingerprint);
        var operation = CreateGoDescribeEntry();
        var catalogEntry = WriteOpsDescribe(scope.FullPath, fingerprint, operation, "other-source-hash");

        var result = await reader.ReadOpsDescribeAsync(project, catalogEntry, "source-hash", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
        Assert.Contains("sourceInputsHash", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadOpsDescribe_ReturnsFormatInvalid_WhenOperationDescriptorDoesNotMatch ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "ops-describe-descriptor-mismatch");
        var reader = new FileReadIndexArtifactReader();
        const string fingerprint = "fingerprint";
        var project = CreateProject(scope, fingerprint);
        var operation = CreateGoDescribeEntry() with { Name = "ucli.test.detail" };
        var catalogEntry = WriteOpsDescribe(scope.FullPath, fingerprint, operation, "source-hash");
        catalogEntry = catalogEntry with { Name = UcliPrimitiveOperationNames.GoDescribe };

        var result = await reader.ReadOpsDescribeAsync(project, catalogEntry, "source-hash", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
        Assert.Contains("operation descriptor", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadTypesCatalog_ReturnsContract_WhenCatalogExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "types-success");
        var reader = new FileReadIndexArtifactReader();
        const string fingerprint = "fingerprint";
        var project = CreateProject(scope, fingerprint);
        var contract = new IndexTypesCatalogJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "source-hash",
            Entries:
            [
                new IndexTypeEntryJsonContract(
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    DisplayName: "Spawner",
                    Namespace: "Game",
                    AssemblyName: "Assembly-CSharp",
                    BaseTypeId: "UnityEngine.MonoBehaviour, UnityEngine.CoreModule",
                    Flags: new IndexTypeFlagsJsonContract(
                        IsAbstract: false,
                        IsGenericDefinition: false,
                        IsUnityObject: true,
                        IsComponent: true,
                        IsScriptableObject: false,
                        IsSerializeReferenceCandidate: false)),
            ]);
        WriteText(UcliStoragePathResolver.ResolveTypesCatalogPath(scope.FullPath, fingerprint), Write(contract));

        var result = await reader.ReadTypesCatalogAsync(project, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(1, result.Value.SchemaVersion);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadSchemasCatalog_ReturnsReadIndexBootstrapFailed_WhenCatalogDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "schemas-missing");
        var reader = new FileReadIndexArtifactReader();
        var project = CreateProject(scope, "fingerprint");

        var result = await reader.ReadSchemasCatalogAsync(project, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexBootstrapFailed, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadSchemasCatalog_ReturnsReadIndexFormatInvalid_WhenCatalogJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "schemas-malformed-json");
        var reader = new FileReadIndexArtifactReader();
        var project = CreateProject(scope, "fingerprint");
        var catalogPath = UcliStoragePathResolver.ResolveSchemasCatalogPath(scope.FullPath, "fingerprint");
        WriteText(catalogPath, "{");

        var result = await reader.ReadSchemasCatalogAsync(project, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAssetSearchLookup_ReturnsContract_WhenLookupExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "asset-search-success");
        var reader = new FileReadIndexArtifactReader();
        const string fingerprint = "fingerprint";
        var project = CreateProject(scope, fingerprint);
        var contract = new IndexAssetSearchLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: "asset-search-hash",
            Entries:
            [
                new IndexAssetSearchEntryJsonContract(
                    AssetPath: "Assets/Data/Spawner.asset",
                    AssetGuid: "11111111111111111111111111111111",
                    Name: "Spawner",
                    TypeId: "Game.Spawner, Assembly-CSharp",
                    SearchTypeIds:
                    [
                        "Game.Spawner, Assembly-CSharp",
                        "UnityEngine.ScriptableObject, UnityEngine.CoreModule",
                        "UnityEngine.Object, UnityEngine.CoreModule",
                    ]),
            ]);
        WriteText(UcliStoragePathResolver.ResolveAssetSearchLookupPath(scope.FullPath, fingerprint), Write(contract));

        var result = await reader.ReadAssetSearchLookupAsync(project, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.NotNull(result.Value.Entries);
        Assert.Single(result.Value.Entries);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadGuidPathLookup_ReturnsReadIndexFormatInvalid_WhenLookupJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "guid-path-malformed");
        var reader = new FileReadIndexArtifactReader();
        var project = CreateProject(scope, "fingerprint");
        var lookupPath = UcliStoragePathResolver.ResolveGuidPathLookupPath(scope.FullPath, "fingerprint");
        WriteText(lookupPath, "{");

        var result = await reader.ReadGuidPathLookupAsync(project, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadSceneTreeLiteLookup_ReturnsContract_WhenLookupExists ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "scene-tree-lite-success");
        var reader = new FileReadIndexArtifactReader();
        const string fingerprint = "fingerprint";
        var project = CreateProject(scope, fingerprint);
        const string scenePath = "Assets/Scenes/Sample.unity";
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: scenePath,
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ]);
        WriteText(UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(scope.FullPath, fingerprint, scenePath), Write(contract));

        var result = await reader.ReadSceneTreeLiteLookupAsync(project, scenePath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(scenePath, result.Value.ScenePath);
        Assert.NotNull(result.Value.Roots);
        Assert.Single(result.Value.Roots);
        Assert.Null(result.Error);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadSceneTreeLiteLookup_ReturnsReadIndexFormatInvalid_WhenScenePathDoesNotMatchRequestedScene ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "scene-tree-lite-mismatch");
        var reader = new FileReadIndexArtifactReader();
        const string fingerprint = "fingerprint";
        var project = CreateProject(scope, fingerprint);
        const string requestedScenePath = "Assets/Scenes/Sample.unity";
        var contract = new IndexSceneTreeLiteLookupJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            ScenePath: "Assets/Scenes/Other.unity",
            SourceInputsHash: "scene-hash",
            Roots:
            [
                new IndexSceneTreeLiteNodeJsonContract(
                    name: "Root",
                    globalObjectId: "GlobalObjectId_V1-2-3-4-5-6",
                    children: Array.Empty<IndexSceneTreeLiteNodeJsonContract>(),
                    childrenState: IndexSceneTreeLiteNodeChildrenStateValues.Complete),
            ]);
        WriteText(UcliStoragePathResolver.ResolveSceneTreeLiteLookupPath(scope.FullPath, fingerprint, requestedScenePath), Write(contract));

        var result = await reader.ReadSceneTreeLiteLookupAsync(project, requestedScenePath, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadInputsManifest_ReturnsReadIndexFormatInvalid_WhenContractIsIncomplete ()
    {
        using var scope = TestDirectories.CreateTempScope("index-catalog-reader", "inputs-incomplete-contract");
        var reader = new FileReadIndexArtifactReader();
        var project = CreateProject(scope, "fingerprint");
        var manifestPath = UcliStoragePathResolver.ResolveIndexInputsManifestPath(scope.FullPath, "fingerprint");
        WriteText(
            manifestPath,
            """
            {
              "schemaVersion": 1,
              "generatedAtUtc": "2026-03-03T00:00:00+00:00",
              "scriptAssembliesHash": "hash",
              "packagesManifestHash": null,
              "packagesLockHash": "hash",
              "assemblyDefinitionHash": "hash",
              "assetsContentHash": "hash",
              "assetSearchHash": "hash",
              "guidPathHash": "hash",
              "combinedHash": "hash"
            }
            """);

        var result = await reader.ReadInputsManifestAsync(project, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.NotNull(result.Error);
        Assert.Equal(ReadIndexErrorCodes.ReadIndexFormatInvalid, result.Error.Code);
    }

    private static void WriteText (
        string path,
        string contents)
    {
        var directoryPath = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException($"Directory path could not be resolved: {path}");
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(path, contents);
    }

    private static ResolvedUnityProjectContext CreateProject (
        TestDirectoryScope scope,
        string fingerprint)
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: scope.CreateDirectory("UnityProject"),
            RepositoryRoot: scope.FullPath,
            ProjectFingerprint: fingerprint,
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private static IndexOpsCatalogEntryJsonContract WriteOpsDescribe (
        string storageRoot,
        string fingerprint,
        IndexOpEntryJsonContract operation,
        string sourceInputsHash)
    {
        var describeKey = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(operation.Name!));
        var contract = new IndexOpsDescribeJsonContract(
            SchemaVersion: 1,
            GeneratedAtUtc: DateTimeOffset.Parse("2026-03-03T00:00:00+00:00"),
            SourceInputsHash: sourceInputsHash,
            Operation: operation);
        var json = Write(contract);
        var describeHash = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(json));
        WriteText(UcliStoragePathResolver.ResolveOpsDescribePath(storageRoot, fingerprint, describeKey), json);
        return new IndexOpsCatalogEntryJsonContract(
            operation.Name,
            operation.Kind,
            operation.Policy,
            operation.Description,
            describeKey,
            describeHash);
    }

    private static IndexOpEntryJsonContract CreateGoDescribeEntry ()
    {
        return new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: "query",
            Policy: "safe",
            ArgsSchemaJson: """{"type":"object"}""",
            ResultSchemaJson: """{"type":"object"}""")
        {
            Description = "Returns a GameObject description including components and child hierarchy.",
            Inputs = Array.Empty<UcliOperationInputContract>(),
            ResultContract = UcliOperationResultContract.One<GameObjectDescriptionResult>("GameObject description result."),
            Assurance = new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<string>(),
                touchedKinds: Array.Empty<string>(),
                planMode: "observesLiveUnity",
                planSemantics: "Validate arguments and observe Unity state without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>()),
        };
    }

    private static string Write (IndexOpsCatalogJsonContract contract)
    {
        return new IndexOpsCatalogJsonContractWriter().Write(contract);
    }

    private static string Write (IndexOpsDescribeJsonContract contract)
    {
        return new IndexOpsDescribeJsonContractWriter().Write(contract);
    }

    private static string Write (IndexTypesCatalogJsonContract contract)
    {
        return new IndexTypesCatalogJsonContractWriter().Write(contract);
    }

    private static string Write (IndexAssetSearchLookupJsonContract contract)
    {
        return new IndexAssetSearchLookupJsonContractWriter().Write(contract);
    }

    private static string Write (IndexSceneTreeLiteLookupJsonContract contract)
    {
        return new IndexSceneTreeLiteLookupJsonContractWriter().Write(contract);
    }
}
