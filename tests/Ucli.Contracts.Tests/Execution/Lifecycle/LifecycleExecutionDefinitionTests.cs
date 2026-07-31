using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Contracts.Tests.Execution.Lifecycle;

public sealed class LifecycleExecutionDefinitionTests
{
    public static TheoryData<LifecycleExecutionKind, string, string> Definitions => new()
    {
        {
            LifecycleExecutionKind.Refresh,
            """{"kind":"refresh"}""",
            "2480b127449c544d62ee5da11a5b316caf49307d0e7ca22bd533af209de19a3c"
        },
        {
            LifecycleExecutionKind.Compile,
            """{"kind":"compile"}""",
            "bc29f185eb2b6fd7e5f11211fc0abc479c91ab5eb826c3dabb27f49549bb3597"
        },
        {
            LifecycleExecutionKind.PlayEnter,
            """{"kind":"play.enter"}""",
            "56ba34e7257ec928c1e44c252ca73cbf0e026f95411cdfe4881dce3ee6a50d68"
        },
        {
            LifecycleExecutionKind.PlayExit,
            """{"kind":"play.exit"}""",
            "441aedf5d46c462b989ab0ad91484888949371f1131d5c05228356dca7fd1623"
        },
    };

    [Theory]
    [MemberData(nameof(Definitions))]
    [Trait("Size", "Small")]
    public void Definition_SerializesToFixedJsonAndCalculatesRfc8785Digest (
        LifecycleExecutionKind kind,
        string expectedJson,
        string expectedDigest)
    {
        var definition = new LifecycleExecutionDefinition(kind);

        var json = JsonSerializer.Serialize(
            definition,
            IpcJsonSerializerOptions.StrictPropertyNames);
        var digest = LifecycleExecutionDefinitionDigest.Calculate(definition);

        Assert.Equal(expectedJson, json);
        Assert.Equal(expectedDigest, digest.ToString());
        Assert.Equal(
            TextVocabulary.GetText(kind),
            definition.ExecutionKind.Value);
    }

    [Theory]
    [InlineData((LifecycleExecutionKind)0)]
    [InlineData((LifecycleExecutionKind)100)]
    [Trait("Size", "Small")]
    public void Constructor_WhenKindIsUndefined_RejectsValue (
        LifecycleExecutionKind kind)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new LifecycleExecutionDefinition(kind));

        Assert.Equal("kind", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ArtifactContract_UsesItsFiniteVocabulary ()
    {
        Assert.Equal(
            "lifecycleExecutionTerminalRecord",
            LifecycleExecutionArtifactContract.TerminalRecordKind.Value);
        Assert.Equal(
            "application/json",
            LifecycleExecutionArtifactContract.TerminalRecordMediaType.Value);
    }
}
