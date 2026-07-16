using System.Text.Json;
using System.Text.Json.Nodes;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Contracts.Tests.Storage;

public sealed class DaemonLifecycleJsonContractSerializerTests
{
    private static readonly Guid SidecarGenerationId =
        Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid SessionGenerationId =
        Guid.Parse("33333333-3333-3333-3333-333333333333");

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
                primaryDiagnostic: null,
                sidecarGenerationId: SidecarGenerationId,
                serverVersion: null,
                editorInstanceId: null,
                recoveryLease: null);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RecoveryLeaseConstructor_WhenSessionGenerationIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new DaemonLifecycleRecoveryLease(
            Guid.Empty,
            DateTimeOffset.UnixEpoch.AddMinutes(1)));

        Assert.Equal("sessionGenerationId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RecoveryLeaseConstructor_WhenExpirationIsDefault_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new DaemonLifecycleRecoveryLease(
            SessionGenerationId,
            default));

        Assert.Equal("expiresAtUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RecoveryLeaseConstructor_WhenExpirationHasNonUtcOffset_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => new DaemonLifecycleRecoveryLease(
            SessionGenerationId,
            new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.FromHours(9))));

        Assert.Equal("expiresAtUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenSidecarGenerationIdIsEmpty_ThrowsArgumentException ()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateContract(Guid.Empty));

        Assert.Equal("sidecarGenerationId", exception.ParamName);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_WhenPresentTimestampHasNonUtcOffset_ThrowsArgumentException (bool useProcessStartTimestamp)
    {
        var nonUtcTimestamp = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(9));

        var exception = Assert.Throws<ArgumentException>(() => new DaemonLifecycleJsonContract(
            processId: 123,
            processStartedAtUtc: useProcessStartTimestamp ? nonUtcTimestamp : DateTimeOffset.UnixEpoch,
            state: CreateState(IpcEditorLifecycleState.Ready),
            observedAtUtc: useProcessStartTimestamp ? DateTimeOffset.UnixEpoch : nonUtcTimestamp,
            actionRequired: null,
            primaryDiagnostic: null,
            sidecarGenerationId: SidecarGenerationId,
            serverVersion: null,
            editorInstanceId: null,
            recoveryLease: null));

        Assert.Equal(
            useProcessStartTimestamp ? "processStartedAtUtc" : "observedAtUtc",
            exception.ParamName);
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

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(true)]
    [InlineData(false)]
    public void Deserialize_WhenSidecarGenerationIdIsEmptyOrMissing_IsRejected (bool omitSidecarGenerationId)
    {
        var validJson = DaemonLifecycleJsonContractSerializer.Serialize(CreateContract());
        var json = JsonNode.Parse(validJson)!.AsObject();

        if (omitSidecarGenerationId)
        {
            Assert.True(json.Remove("sidecarGenerationId"));
        }
        else
        {
            json["sidecarGenerationId"] = Guid.Empty;
        }

        var exception = Record.Exception(() =>
        {
            _ = DaemonLifecycleJsonContractSerializer.Deserialize(json.ToJsonString());
        });

        Assert.True(
            exception is JsonException or ArgumentException,
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
        Assert.Equal(SidecarGenerationId, actual.SidecarGenerationId);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Serialize_WithRecoveryLease_RoundTripsTypedLease ()
    {
        var observedAtUtc = DateTimeOffset.UnixEpoch;
        var expectedLease = new DaemonLifecycleRecoveryLease(
            SessionGenerationId,
            observedAtUtc + TimeSpan.FromMinutes(5));
        var contract = CreateContract(
            state: CreateState(IpcEditorLifecycleState.Recovering),
            recoveryLease: expectedLease);

        var json = DaemonLifecycleJsonContractSerializer.Serialize(contract);
        var actual = DaemonLifecycleJsonContractSerializer.Deserialize(json);

        Assert.NotNull(actual);
        Assert.Equal(expectedLease, actual.RecoveryLease);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenRecoveryLeaseIsAttachedToNonRecoveringState_ThrowsArgumentException ()
    {
        var recoveryLease = new DaemonLifecycleRecoveryLease(
            SessionGenerationId,
            DateTimeOffset.UnixEpoch.AddMinutes(5));

        var exception = Assert.Throws<ArgumentException>(() => CreateContract(
            state: CreateState(IpcEditorLifecycleState.Ready),
            recoveryLease: recoveryLease));

        Assert.Equal("recoveryLease", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenRecoveryLeaseDoesNotExpireAfterObservation_ThrowsArgumentException ()
    {
        var recoveryLease = new DaemonLifecycleRecoveryLease(
            SessionGenerationId,
            DateTimeOffset.UnixEpoch);

        var exception = Assert.Throws<ArgumentException>(() => CreateContract(
            state: CreateState(IpcEditorLifecycleState.Recovering),
            recoveryLease: recoveryLease));

        Assert.Equal("recoveryLease", exception.ParamName);
    }

    private static DaemonLifecycleJsonContract CreateContract (
        Guid? sidecarGenerationId = null,
        UnityEditorStateSnapshot? state = null,
        DaemonLifecycleRecoveryLease? recoveryLease = null)
    {
        return new DaemonLifecycleJsonContract(
            processId: 123,
            processStartedAtUtc: DateTimeOffset.UnixEpoch,
            state: state ?? CreateState(IpcEditorLifecycleState.Ready),
            observedAtUtc: DateTimeOffset.UnixEpoch,
            actionRequired: null,
            primaryDiagnostic: null,
            sidecarGenerationId: sidecarGenerationId ?? SidecarGenerationId,
            serverVersion: null,
            editorInstanceId: null,
            recoveryLease: recoveryLease);
    }

    private static UnityEditorStateSnapshot CreateState (IpcEditorLifecycleState lifecycleState)
    {
        return new UnityEditorStateSnapshot(
            DaemonEditorMode.Gui,
            lifecycleState,
            IpcCompileState.Ready,
            new IpcUnityGenerationSnapshot(1, 2, 3, 4),
            new IpcPlayModeSnapshot(
                IpcPlayModeState.Stopped,
                IpcPlayModeTransition.None,
                IsPlaying: false,
                IsPlayingOrWillChangePlaymode: false));
    }
}
