using System.Text.Json;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Execution.References;

public sealed class ExecutionRefTests
{
    private static readonly ExecutionKind Kind = new("programRun");
    private static readonly Guid Id = Guid.Parse("8b8b657d-f631-4509-af40-88f6af40f53b");
    private static readonly Sha256Digest DefinitionDigest =
        Sha256Digest.Parse(new string('c', 64));
    private static readonly ExecutionState State = new("running");
    private static readonly ExecutionStatusLocator StatusLocator =
        new("program-runs/8b8b657df6314509af4088f6af40f53b/status.json");

    [Fact]
    [Trait("Size", "Small")]
    public void LifecycleVariants_RoundTripThroughTheSharedTaggedUnion ()
    {
        var terminalRecord = new PathArtifactRef(
            new ArtifactKind("programRun.terminalRecord"),
            new ArtifactMediaType("application/json"),
            new ArtifactPath("program-runs/terminal-record.json"),
            Sha256Digest.Parse(new string('d', 64)),
            sizeBytes: 128,
            new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero));
        (ExecutionRef Reference, string Lifecycle, string State, string? StatusLocator)[] cases =
        [
            (
                new ActiveExecutionRef(Kind, Id, DefinitionDigest, State, StatusLocator),
                "active",
                "running",
                StatusLocator.Value
            ),
            (
                new RecoveryExecutionRef(
                    Kind,
                    Id,
                    DefinitionDigest,
                    new ExecutionState("cancelling"),
                    StatusLocator),
                "recovery",
                "cancelling",
                StatusLocator.Value
            ),
            (
                new TerminalExecutionRef(
                    Kind,
                    Id,
                    DefinitionDigest,
                    new ExecutionState("completed"),
                    statusLocator: null,
                    terminalRecord),
                "terminal",
                "completed",
                null
            ),
        ];

        foreach (var testCase in cases)
        {
            var json = JsonSerializer.Serialize(
                testCase.Reference,
                IpcJsonSerializerOptions.StrictPropertyNames);
            var roundTripped = JsonSerializer.Deserialize<ExecutionRef>(
                json,
                IpcJsonSerializerOptions.StrictPropertyNames);

            Assert.Equal(testCase.Reference, roundTripped);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var expectedPropertyNames = testCase.Reference is TerminalExecutionRef
                ? new[]
                {
                    "definitionDigest",
                    "id",
                    "kind",
                    "lifecycle",
                    "state",
                    "statusLocator",
                    "terminalRecordRef",
                }
                : new[]
                {
                    "definitionDigest",
                    "id",
                    "kind",
                    "lifecycle",
                    "state",
                    "statusLocator",
                };
            Assert.Equal(
                expectedPropertyNames,
                root.EnumerateObject()
                    .Select(static property => property.Name)
                    .Order(StringComparer.Ordinal));
            Assert.Equal("programRun", root.GetProperty("kind").GetString());
            Assert.Equal(
                "8b8b657d-f631-4509-af40-88f6af40f53b",
                root.GetProperty("id").GetString());
            Assert.Equal(new string('c', 64), root.GetProperty("definitionDigest").GetString());
            Assert.Equal(testCase.Lifecycle, root.GetProperty("lifecycle").GetString());
            Assert.Equal(testCase.State, root.GetProperty("state").GetString());
            Assert.Equal(testCase.StatusLocator, root.GetProperty("statusLocator").GetString());
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void EnsureDefinitionConsistencyWith_RejectsDigestReuseForTheSameIdentity ()
    {
        var established = new ActiveExecutionRef(
            Kind,
            Id,
            DefinitionDigest,
            State,
            StatusLocator);
        var conflicting = new RecoveryExecutionRef(
            new ExecutionKind(Kind.Value),
            Id,
            Sha256Digest.Parse(new string('e', 64)),
            new ExecutionState("cancelling"),
            StatusLocator);

        var exception = Assert.Throws<ArgumentException>(
            () => established.EnsureDefinitionConsistencyWith(conflicting));

        Assert.Equal("candidate", exception.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("program runs/status.json")]
    [Trait("Size", "Small")]
    public void StatusLocator_RejectsEmptyOrWhitespaceText (string value)
    {
        Assert.Throws<ArgumentException>(() => new ExecutionStatusLocator(value));
    }
}
