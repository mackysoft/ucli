using System.Text.Json;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Infrastructure.Execution.ReadPostcondition;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Tests;

public sealed class MutationReadPostconditionStoreTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryAdmitEvalCall_PersistsConsumedBindingAndThreeBroadFences ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-journal", "admit");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint-1");
        var journal = new MutationReadPostconditionJournal();
        var startedAtUtc = DateTimeOffset.UtcNow;

        const string planToken = "raw-plan-token";
        var result = await journal.TryAdmitEvalCallAsync(storageRoot, fingerprint, CreateAdmission("nonce-1", planToken), CancellationToken.None);

        var postcondition = Assert.IsType<ExecutionReadPostcondition>(result.ReadPostcondition);
        Assert.True(result.IsAdmitted);
        Assert.False(result.IsReplay);
        Assert.Equal(3, postcondition.Requirements.Count);
        Assert.All(postcondition.Requirements, requirement => Assert.True(requirement.MinSafeGeneratedAtUtc >= startedAtUtc));
        Assert.Contains(postcondition.Requirements, static requirement => requirement.Surface == ExecutionReadPostconditionSurface.AssetSearch && requirement.ScenePath is null);
        Assert.Contains(postcondition.Requirements, static requirement => requirement.Surface == ExecutionReadPostconditionSurface.GuidPath && requirement.ScenePath is null);
        Assert.Contains(postcondition.Requirements, static requirement => requirement.Surface == ExecutionReadPostconditionSurface.SceneTreeLite && requirement.ScenePath is null);

        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(storageRoot, fingerprint);
        using var document = JsonDocument.Parse(File.ReadAllText(documentPath.Value));
        JsonAssert.For(document.RootElement)
            .HasInt32("schemaVersion", 2)
            .HasArrayLength("requirements", 3)
            .HasArrayLength("consumedEvalCalls", 1);
        Assert.False(File.ReadAllText(documentPath.Value).Contains(planToken, StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryAdmitEvalCall_RejectsReplayAfterJournalIsRecreated ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-journal", "replay");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint-1");

        var initial = await new MutationReadPostconditionJournal().TryAdmitEvalCallAsync(storageRoot, fingerprint, CreateAdmission("nonce-1", "token-1"), CancellationToken.None);
        var replay = await new MutationReadPostconditionJournal().TryAdmitEvalCallAsync(storageRoot, fingerprint, CreateAdmission("nonce-1", "token-1"), CancellationToken.None);

        Assert.True(initial.IsAdmitted);
        Assert.False(replay.IsAdmitted);
        Assert.True(replay.IsReplay);
        Assert.Null(replay.Failure);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryAdmitEvalCall_WhenCalledConcurrently_AdmitsOnlyOneCall ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-journal", "concurrent-admit");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint-1");
        var admission = CreateAdmission("nonce-1", "token-1");

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => new MutationReadPostconditionJournal().TryAdmitEvalCallAsync(storageRoot, fingerprint, admission, CancellationToken.None).AsTask()));

        Assert.Single(results, static result => result.IsAdmitted);
        Assert.Equal(7, results.Count(static result => result.IsReplay));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryAdmitEvalCall_UsesNextTickAfterExistingRequirementAndMergesIt ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-journal", "monotonic-fence");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint-1");
        var existingFenceUtc = DateTimeOffset.UtcNow.AddMinutes(10);
        var journal = new MutationReadPostconditionJournal();
        await journal.WriteMergedAsync(storageRoot, fingerprint, new ExecutionReadPostcondition([
            new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.AssetSearch, existingFenceUtc, null),
            new ExecutionReadPostconditionRequirement(ExecutionReadPostconditionSurface.SceneTreeLite, existingFenceUtc, new UnityScenePath("Assets/Scenes/Main.unity")),
        ]), CancellationToken.None);

        var result = await journal.TryAdmitEvalCallAsync(storageRoot, fingerprint, CreateAdmission("nonce-1", "token-1"), CancellationToken.None);

        var expectedFenceUtc = existingFenceUtc.AddTicks(1);
        Assert.All(Assert.IsType<ExecutionReadPostcondition>(result.ReadPostcondition).Requirements, requirement => Assert.Equal(expectedFenceUtc, requirement.MinSafeGeneratedAtUtc));
        var read = await journal.ReadOrNullAsync(storageRoot, fingerprint, CancellationToken.None);
        Assert.Contains(Assert.IsType<ExecutionReadPostcondition>(read.ReadPostcondition).Requirements, requirement => requirement.ScenePath == new UnityScenePath("Assets/Scenes/Main.unity") && requirement.MinSafeGeneratedAtUtc == existingFenceUtc);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task TryAdmitEvalCall_WhenJournalIsMalformed_FailsClosedWithoutReplacingIt ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-journal", "malformed");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var fingerprint = ProjectFingerprintTestFactory.Create("fingerprint-1");
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(storageRoot, fingerprint);
        scope.WriteFile(Path.GetRelativePath(scope.FullPath, documentPath.Value), "{\"schemaVersion\":1}");

        var result = await new MutationReadPostconditionJournal().TryAdmitEvalCallAsync(storageRoot, fingerprint, CreateAdmission("nonce-1", "token-1"), CancellationToken.None);

        Assert.False(result.IsAdmitted);
        Assert.False(result.IsReplay);
        Assert.Equal(MutationReadPostconditionJournalFailureKind.InvalidDocument, Assert.IsType<MutationReadPostconditionJournalFailure>(result.Failure).Kind);
        Assert.Equal("{\"schemaVersion\":1}", File.ReadAllText(documentPath.Value));
    }

    private static EvalCallAdmission CreateAdmission (string nonce, string tokenText)
    {
        return new EvalCallAdmission(
            nonce,
            Sha256Digest.Compute(System.Text.Encoding.UTF8.GetBytes(tokenText)),
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Sha256Digest.Compute(System.Text.Encoding.UTF8.GetBytes("source")),
            Sha256Digest.Compute(System.Text.Encoding.UTF8.GetBytes("execution")),
            42,
            DateTimeOffset.Parse("2026-01-01T00:00:00+00:00"),
            DateTimeOffset.Parse("2026-01-01T00:05:00+00:00"));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadOrNull_ReturnsNull_WhenFileDoesNotExist ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "missing");
        var store = new MutationReadPostconditionStore();

        var result = await store.ReadOrNullAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprintTestFactory.Create("fingerprint-1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Null(result.ReadPostcondition);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteMerged_ThenRead_MergesLatestRequirementPerKey ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "merge-roundtrip");
        var store = new MutationReadPostconditionStore();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(AbsolutePath.Parse(scope.FullPath), ProjectFingerprintTestFactory.Create("fingerprint-1"));

        var firstWrite = await store.WriteMergedAsync(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprintTestFactory.Create("fingerprint-1"),
            new ExecutionReadPostcondition(
            [
                new ExecutionReadPostconditionRequirement(
                    Surface: ExecutionReadPostconditionSurface.AssetSearch,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    ScenePath: null),
                new ExecutionReadPostconditionRequirement(
                    Surface: ExecutionReadPostconditionSurface.SceneTreeLite,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    ScenePath: new UnityScenePath(@"Assets\Scenes\Main.unity")),
            ]),
            CancellationToken.None);
        var secondWrite = await store.WriteMergedAsync(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprintTestFactory.Create("fingerprint-1"),
            new ExecutionReadPostcondition(
            [
                new ExecutionReadPostconditionRequirement(
                    Surface: ExecutionReadPostconditionSurface.AssetSearch,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-24T00:00:00+00:00"),
                    ScenePath: null),
                new ExecutionReadPostconditionRequirement(
                    Surface: ExecutionReadPostconditionSurface.GuidPath,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-24T00:00:00+00:00"),
                    ScenePath: null),
            ]),
            CancellationToken.None);

        Assert.True(firstWrite.IsSuccess);
        Assert.True(secondWrite.IsSuccess);

        var readResult = await store.ReadOrNullAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprintTestFactory.Create("fingerprint-1"), CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        var readPostcondition = Assert.IsType<ExecutionReadPostcondition>(readResult.ReadPostcondition);
        Assert.Equal(3, readPostcondition.Requirements.Count);
        Assert.Contains(
            readPostcondition.Requirements,
            static requirement => requirement.Surface == ExecutionReadPostconditionSurface.AssetSearch
                && requirement.MinSafeGeneratedAtUtc == DateTimeOffset.Parse("2026-04-24T00:00:00+00:00"));
        Assert.Contains(
            readPostcondition.Requirements,
            static requirement => requirement.Surface == ExecutionReadPostconditionSurface.GuidPath
                && requirement.MinSafeGeneratedAtUtc == DateTimeOffset.Parse("2026-04-24T00:00:00+00:00"));
        Assert.Contains(
            readPostcondition.Requirements,
            static requirement => requirement.Surface == ExecutionReadPostconditionSurface.SceneTreeLite
                && requirement.ScenePath == new UnityScenePath("Assets/Scenes/Main.unity"));

        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(documentPath.Value));
        JsonAssert.For(jsonDocument.RootElement)
            .HasInt32("schemaVersion", 2)
            .HasArrayLength("requirements", 3)
            .HasArrayLength("consumedEvalCalls", 0);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteMerged_WhenStoresWriteConcurrently_PreservesEveryDistinctRequirement ()
    {
        const int writerCount = 16;
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "concurrent-merge");
        var projectFingerprint = ProjectFingerprintTestFactory.Create("fingerprint-1");
        using var startBarrier = new Barrier(writerCount);
        var writeTasks = Enumerable
            .Range(0, writerCount)
            .Select(index => Task.Factory.StartNew(
                () =>
                {
                    if (!startBarrier.SignalAndWait(TimeSpan.FromSeconds(10)))
                    {
                        throw new TimeoutException("Concurrent mutation read-postcondition writers did not reach the start barrier.");
                    }

                    var store = new MutationReadPostconditionStore();
                    return store.WriteMergedAsync(
                            AbsolutePath.Parse(scope.FullPath),
                            projectFingerprint,
                            new ExecutionReadPostcondition(
                            [
                                new ExecutionReadPostconditionRequirement(
                                    Surface: ExecutionReadPostconditionSurface.SceneTreeLite,
                                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00").AddMinutes(index),
                                    ScenePath: new UnityScenePath($"Assets/Scenes/Concurrent-{index:D2}.unity")),
                            ]),
                            CancellationToken.None)
                        .AsTask()
                        .GetAwaiter()
                        .GetResult();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        var writeResults = await Task.WhenAll(writeTasks);
        Assert.All(writeResults, static result => Assert.True(result.IsSuccess, result.Error?.Message));

        var readResult = await new MutationReadPostconditionStore().ReadOrNullAsync(
            AbsolutePath.Parse(scope.FullPath),
            projectFingerprint,
            CancellationToken.None);

        Assert.True(readResult.IsSuccess, readResult.Error?.Message);
        var readPostcondition = Assert.IsType<ExecutionReadPostcondition>(readResult.ReadPostcondition);
        Assert.Equal(writerCount, readPostcondition.Requirements.Count);
        for (var index = 0; index < writerCount; index++)
        {
            var expectedScenePath = new UnityScenePath($"Assets/Scenes/Concurrent-{index:D2}.unity");
            Assert.Contains(
                readPostcondition.Requirements,
                requirement => requirement.Surface == ExecutionReadPostconditionSurface.SceneTreeLite
                    && requirement.ScenePath == expectedScenePath);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task WriteMerged_WhenSceneTreeLiteHasNoScenePath_PersistsWildcardRequirement ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "scene-tree-lite-wildcard");
        var store = new MutationReadPostconditionStore();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(AbsolutePath.Parse(scope.FullPath), ProjectFingerprintTestFactory.Create("fingerprint-1"));

        var writeResult = await store.WriteMergedAsync(
            AbsolutePath.Parse(scope.FullPath),
            ProjectFingerprintTestFactory.Create("fingerprint-1"),
            new ExecutionReadPostcondition(
            [
                new ExecutionReadPostconditionRequirement(
                    Surface: ExecutionReadPostconditionSurface.SceneTreeLite,
                    MinSafeGeneratedAtUtc: DateTimeOffset.Parse("2026-04-23T00:00:00+00:00"),
                    ScenePath: null),
            ]),
            CancellationToken.None);

        Assert.True(writeResult.IsSuccess);

        var readResult = await store.ReadOrNullAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprintTestFactory.Create("fingerprint-1"), CancellationToken.None);

        Assert.True(readResult.IsSuccess);
        var readPostcondition = Assert.IsType<ExecutionReadPostcondition>(readResult.ReadPostcondition);
        var requirement = Assert.Single(readPostcondition.Requirements);
        Assert.Equal(ExecutionReadPostconditionSurface.SceneTreeLite, requirement.Surface);
        Assert.Null(requirement.ScenePath);

        using var jsonDocument = JsonDocument.Parse(File.ReadAllText(documentPath.Value));
        Assert.False(jsonDocument.RootElement.GetProperty("requirements")[0].TryGetProperty("scenePath", out _));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ReadOrNull_ReturnsInvalidArgument_WhenJsonIsMalformed ()
    {
        using var scope = TestDirectories.CreateTempScope("mutation-read-postcondition-store", "malformed-json");
        var store = new MutationReadPostconditionStore();
        var documentPath = UcliStoragePathResolver.ResolveMutationReadPostconditionPath(AbsolutePath.Parse(scope.FullPath), ProjectFingerprintTestFactory.Create("fingerprint-1"));
        var relativePath = Path.GetRelativePath(scope.FullPath, documentPath.Value);
        scope.WriteFile(relativePath, "{");

        var result = await store.ReadOrNullAsync(AbsolutePath.Parse(scope.FullPath), ProjectFingerprintTestFactory.Create("fingerprint-1"), CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
    }
}
