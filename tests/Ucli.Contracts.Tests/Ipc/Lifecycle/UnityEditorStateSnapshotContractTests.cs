using System.Text.Json;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Storage;

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
                DateTimeOffset.UnixEpoch,
                actionRequired: null,
                primaryDiagnostic: null);
        });
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityEditorObservationConstructor_WhenObservedAtUtcIsNotCanonicalUtc_ThrowsArgumentException ()
    {
        foreach (var invalidTimestamp in new[]
                 {
                     default,
                     new DateTimeOffset(2026, 7, 15, 9, 0, 0, TimeSpan.FromHours(9)),
                 })
        {
            var exception = Assert.Throws<ArgumentException>(() => new IpcUnityEditorObservation(
                "server",
                "unity",
                ProjectFingerprint,
                CreateState(),
                invalidTimestamp,
                actionRequired: null,
                primaryDiagnostic: null));

            Assert.Equal("observedAtUtc", exception.ParamName);
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("actionRequired")]
    [InlineData("primaryDiagnosticKind")]
    public void LifecycleDiagnosticConstructor_WhenFiniteValueIsUndefined_ThrowsArgumentOutOfRangeException (string fieldName)
    {
        Func<object> construction = fieldName switch
        {
            "actionRequired" => () => new IpcUnityEditorObservation(
                "server",
                "unity",
                ProjectFingerprint,
                CreateState(),
                DateTimeOffset.UnixEpoch,
                actionRequired: (DaemonDiagnosisActionRequired)0,
                primaryDiagnostic: null),
            "primaryDiagnosticKind" => () => new IpcPrimaryDiagnostic(
                Kind: (DaemonDiagnosisPrimaryDiagnosticKind)0,
                Code: null,
                File: null,
                Line: null,
                Column: null,
                Message: null),
            _ => throw new ArgumentOutOfRangeException(nameof(fieldName), fieldName, "Unknown field name."),
        };

        Assert.Throws<ArgumentOutOfRangeException>(construction);
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData("kind")]
    [InlineData("code")]
    [InlineData("file")]
    [InlineData("line")]
    [InlineData("column")]
    [InlineData("message")]
    public void PrimaryDiagnosticJson_WhenRequiredFieldIsMissing_ThrowsJsonException (string propertyName)
    {
        var json = JsonSerializer.SerializeToNode(
            new IpcPrimaryDiagnostic(
                DaemonDiagnosisPrimaryDiagnosticKind.Compiler,
                null,
                null,
                null,
                null,
                null),
            IpcJsonSerializerOptions.Default)!.AsObject();
        Assert.True(json.Remove(propertyName));

        Assert.Throws<JsonException>(() =>
        {
            _ = json.Deserialize<IpcPrimaryDiagnostic>(IpcJsonSerializerOptions.Default);
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
            DateTimeOffset.UnixEpoch,
            actionRequired: null,
            primaryDiagnostic: null);
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
                DateTimeOffset.UnixEpoch,
                actionRequired: null,
                primaryDiagnostic: null),
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
                DateTimeOffset.UnixEpoch,
                actionRequired: null,
                primaryDiagnostic: null),
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
            exception is JsonException or ArgumentException,
            $"Expected the state contract to reject invalid JSON, but got: {exception}");
    }
}
