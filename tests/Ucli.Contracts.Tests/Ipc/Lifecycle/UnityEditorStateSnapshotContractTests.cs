using System.Text.Json;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Lifecycle;

public sealed class UnityEditorStateSnapshotContractTests
{
    private static readonly ProjectFingerprint ProjectFingerprint =
        new("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GenerationConstructor_WhenCounterIsNegative_ThrowsArgumentOutOfRangeException (int counterIndex)
    {
        var values = new long[] { 0, 0, 0, 0 };
        values[counterIndex] = -1;

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new IpcUnityGenerationSnapshot(values[0], values[1], values[2], values[3]);
        });
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("compileGeneration")]
    [InlineData("domainReloadGeneration")]
    [InlineData("assetRefreshGeneration")]
    [InlineData("playModeGeneration")]
    public void GenerationJson_WhenRequiredCounterIsMissing_ThrowsJsonException (string propertyName)
    {
        var json = JsonSerializer.SerializeToNode(
            CreateGenerations(),
            IpcJsonSerializerOptions.Default)!.AsObject();
        Assert.True(json.Remove(propertyName));

        Assert.Throws<JsonException>(() =>
        {
            _ = json.Deserialize<IpcUnityGenerationSnapshot>(IpcJsonSerializerOptions.Default);
        });
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("state")]
    [InlineData("transition")]
    [InlineData("isPlaying")]
    [InlineData("isPlayingOrWillChangePlaymode")]
    public void PlayModeJson_WhenRequiredFieldIsMissing_ThrowsJsonException (string propertyName)
    {
        var json = JsonSerializer.SerializeToNode(
            CreatePlayMode(),
            IpcJsonSerializerOptions.Default)!.AsObject();
        Assert.True(json.Remove(propertyName));

        Assert.Throws<JsonException>(() =>
        {
            _ = json.Deserialize<IpcPlayModeSnapshot>(IpcJsonSerializerOptions.Default);
        });
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("editorMode")]
    [InlineData("lifecycleState")]
    [InlineData("compileState")]
    [InlineData("generations")]
    [InlineData("playMode")]
    public void EditorStateJson_WhenRequiredFieldIsMissing_IsRejected (string propertyName)
    {
        var json = JsonSerializer.SerializeToNode(
            CreateState(),
            IpcJsonSerializerOptions.Default)!.AsObject();
        Assert.True(json.Remove(propertyName));

        var exception = Record.Exception(() =>
        {
            _ = json.Deserialize<UnityEditorStateSnapshot>(IpcJsonSerializerOptions.Default);
        });

        AssertInvalidContractException(exception);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(1)]
    public void UnityEditorObservationConstructor_WhenVersionIsWhitespace_ThrowsArgumentException (
        int versionIndex)
    {
        var versions = new[] { "server", "unity" };
        versions[versionIndex] = " ";

        Assert.Throws<ArgumentException>(() =>
        {
            _ = new IpcUnityEditorObservation(
                versions[0],
                versions[1],
                ProjectFingerprint,
                CreateState(),
                DateTimeOffset.UnixEpoch);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityEditorObservationConstructor_WhenObservedAtUtcIsDefault_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = new IpcUnityEditorObservation(
                "server",
                "unity",
                ProjectFingerprint,
                CreateState(),
                default);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityEditorObservationJson_UsesCaseInsensitiveConstructorParameterBinding ()
    {
        var expected = new IpcUnityEditorObservation(
            "server",
            "unity",
            ProjectFingerprint,
            CreateState(),
            DateTimeOffset.UnixEpoch);
        var json = JsonSerializer.Serialize(expected, IpcJsonSerializerOptions.Default);

        var actual = JsonSerializer.Deserialize<IpcUnityEditorObservation>(
            json,
            IpcJsonSerializerOptions.Default);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("observation")]
    [InlineData("screenshot")]
    public void LifecycleContractConstructor_WhenStateIsNull_ThrowsArgumentNullException (string contractName)
    {
        Func<object> construction = contractName switch
        {
            "observation" => () => new IpcUnityEditorObservation(
                "server",
                "unity",
                ProjectFingerprint,
                null!,
                DateTimeOffset.UnixEpoch),
            "screenshot" => () => new IpcScreenshotCapture(
                IpcScreenshotTarget.Game,
                IpcScreenshotSizeMode.CurrentSurface,
                null,
                null,
                1,
                1,
                IpcScreenshotColorSpace.Linear,
                null!),
            _ => throw new ArgumentOutOfRangeException(nameof(contractName), contractName, "Unknown contract name."),
        };

        Assert.Throws<ArgumentNullException>(construction);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("observation", true)]
    [InlineData("observation", false)]
    [InlineData("screenshot", true)]
    [InlineData("screenshot", false)]
    public void LifecycleContractJson_WhenStateIsNullOrMissing_IsRejected (string contractName, bool omitState)
    {
        var (contract, contractType) = CreateLifecycleContract(contractName);
        var json = JsonSerializer.SerializeToNode(
            contract,
            contractType,
            IpcJsonSerializerOptions.Default)!.AsObject();

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
            _ = JsonSerializer.Deserialize(json, contractType, IpcJsonSerializerOptions.Default);
        });

        AssertInvalidContractException(exception);
    }

    private static (object Contract, Type ContractType) CreateLifecycleContract (string contractName)
    {
        var state = CreateState();
        object contract = contractName switch
        {
            "observation" => new IpcUnityEditorObservation(
                "server",
                "unity",
                ProjectFingerprint,
                state,
                DateTimeOffset.UnixEpoch),
            "screenshot" => new IpcScreenshotCapture(
                IpcScreenshotTarget.Game,
                IpcScreenshotSizeMode.CurrentSurface,
                null,
                null,
                1,
                1,
                IpcScreenshotColorSpace.Linear,
                state),
            _ => throw new ArgumentOutOfRangeException(nameof(contractName), contractName, "Unknown contract name."),
        };
        return (contract, contract.GetType());
    }

    private static UnityEditorStateSnapshot CreateState ()
    {
        return new UnityEditorStateSnapshot(
            DaemonEditorMode.Gui,
            IpcEditorLifecycleState.Ready,
            IpcCompileState.Ready,
            CreateGenerations(),
            CreatePlayMode());
    }

    private static IpcUnityGenerationSnapshot CreateGenerations ()
    {
        return new IpcUnityGenerationSnapshot(1, 2, 3, 4);
    }

    private static IpcPlayModeSnapshot CreatePlayMode ()
    {
        return new IpcPlayModeSnapshot(
            IpcPlayModeState.Stopped,
            IpcPlayModeTransition.None,
            IsPlaying: false,
            IsPlayingOrWillChangePlaymode: false);
    }

    private static void AssertInvalidContractException (Exception? exception)
    {
        Assert.True(
            exception is JsonException or ArgumentNullException,
            $"Expected the state contract to reject invalid JSON, but got: {exception}");
    }
}
