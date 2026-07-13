using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class DaemonLifecycleJsonContractSerializerTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenStateIsNull_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() =>
        {
            _ = new DaemonLifecycleJsonContract(
                processId: 123,
                processStartedAtUtc: DateTimeOffset.UnixEpoch,
                state: null!,
                observedAtUtc: DateTimeOffset.UnixEpoch,
                actionRequired: null,
                primaryDiagnostic: null);
        });
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(true)]
    [InlineData(false)]
    public void Deserialize_WhenStateIsNullOrMissing_IsRejected (bool omitState)
    {
        var validJson = DaemonLifecycleJsonContractSerializer.Serialize(CreateContract());
        var json = JsonNode.Parse(validJson)!.AsObject();

        if (omitState)
        {
            Assert.True(json.Remove("state"));
        }
        else
        {
            json["state"] = null;
        }

        var exception = Record.Exception(() =>
        {
            _ = DaemonLifecycleJsonContractSerializer.Deserialize(json.ToJsonString());
        });

        Assert.True(
            exception is JsonException or ArgumentNullException,
            $"Expected the lifecycle contract to reject invalid JSON, but got: {exception}");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Deserialize_WithState_ReturnsCompleteObservation ()
    {
        var expected = CreateContract();
        var json = DaemonLifecycleJsonContractSerializer.Serialize(expected);

        var actual = DaemonLifecycleJsonContractSerializer.Deserialize(json);

        Assert.NotNull(actual);
        Assert.Equal(expected.State, actual.State);
    }

    private static DaemonLifecycleJsonContract CreateContract ()
    {
        return new DaemonLifecycleJsonContract(
            processId: 123,
            processStartedAtUtc: DateTimeOffset.UnixEpoch,
            state: new UnityEditorStateSnapshot(
                DaemonEditorMode.Gui,
                IpcEditorLifecycleState.Ready,
                IpcCompileState.Ready,
                new IpcUnityGenerationSnapshot(1, 2, 3, 4),
                new IpcPlayModeSnapshot(
                    IpcPlayModeState.Stopped,
                    IpcPlayModeTransition.None,
                    IsPlaying: false,
                    IsPlayingOrWillChangePlaymode: false)),
            observedAtUtc: DateTimeOffset.UnixEpoch,
            actionRequired: null,
            primaryDiagnostic: null);
    }
}
