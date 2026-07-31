using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Protocol;

public sealed class IpcNestedTaggedUnionNormalizationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void PayloadCodec_WithNestedTerminalExecutionAndArtifactReferences_RoundTrips ()
    {
        var executionId =
            Guid.Parse("3ee818fc-41af-4a98-ab74-fcfb374ac2f8");
        var definition = new LifecycleExecutionDefinition(
            LifecycleExecutionKind.Compile);
        var terminalRecordReference = new PathArtifactRef(
            LifecycleExecutionArtifactContract.TerminalRecordKind,
            LifecycleExecutionArtifactContract.TerminalRecordMediaType,
            new ArtifactPath(
                $".ucli/local/artifacts/lifecycle-execution/compile/{executionId:N}/terminal.json"),
            Sha256Digest.Parse(new string('d', 64)),
            sizeBytes: 512,
            new DateTimeOffset(2026, 7, 31, 1, 2, 3, TimeSpan.Zero));
        var executionReference = new TerminalExecutionRef(
            definition.ExecutionKind,
            executionId,
            LifecycleExecutionDefinitionDigest.Calculate(definition),
            new ExecutionState(
                TextVocabulary.GetText(LifecycleExecutionState.Failed)),
            statusLocator: null,
            terminalRecordReference);
        var payload = new IpcCompileErrorResponse(
            executionReference,
            ExecutionApplicationState.Unknown,
            result: null,
            observedLifecycle: null);

        var element = IpcPayloadCodec.SerializeToElement(payload);
        var success = IpcPayloadCodec.TryDeserialize(
            element,
            out IpcCompileErrorResponse roundTripped,
            out var error);

        Assert.True(success, error.Message);
        Assert.Equal(executionReference, roundTripped.LifecycleExecutionRef);
        var terminal = Assert.IsType<TerminalExecutionRef>(
            roundTripped.LifecycleExecutionRef);
        Assert.Equal(terminalRecordReference, terminal.TerminalRecordRef);
    }
}
