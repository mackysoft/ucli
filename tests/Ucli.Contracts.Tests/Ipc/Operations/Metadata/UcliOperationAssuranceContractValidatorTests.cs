using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UcliOperationAssuranceContractValidatorTests
{
    private const string ValidAssetMutationJson = """
        {
          "sideEffects": ["assetContentMutation"],
          "mayDirty": true,
          "mayPersist": false,
          "touchedKinds": ["asset"],
          "planMode": "observesLiveUnity",
          "planSemantics": "Observe the target without mutation.",
          "callSemantics": "Mutate the target asset.",
          "touchedContract": "Reports the mutated asset.",
          "readPostconditionContract": "Asset reads may be stale.",
          "failureSemantics": "Failure may leave a partial mutation.",
          "dangerousNotes": ["The operation can dirty an asset."]
        }
        """;

    [Theory]
    [InlineData((UcliTouchedResourceKind)0)]
    [InlineData((UcliTouchedResourceKind)int.MaxValue)]
    [Trait("Size", "Small")]
    public void Constructor_WhenTouchedKindIsUnsupported_RejectsInvalidValue (UcliTouchedResourceKind touchedKind)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateAssurance(
            Array.Empty<UcliOperationSideEffect>(),
            [touchedKind],
            UcliOperationPlanMode.ValidationOnly,
            Array.Empty<string>()));

        Assert.Equal("touchedKinds", exception.ParamName);
    }

    [Theory]
    [InlineData((UcliOperationSideEffect)(-1))]
    [InlineData((UcliOperationSideEffect)int.MaxValue)]
    [Trait("Size", "Small")]
    public void Constructor_WhenSideEffectIsUnsupported_RejectsInvalidValue (UcliOperationSideEffect sideEffect)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateAssurance(
            [sideEffect],
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ValidationOnly,
            Array.Empty<string>()));

        Assert.Equal("sideEffects", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenPlanModeIsUnsupported_RejectsInvalidValue ()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateAssurance(
            Array.Empty<UcliOperationSideEffect>(),
            Array.Empty<UcliTouchedResourceKind>(),
            (UcliOperationPlanMode)int.MaxValue,
            Array.Empty<string>()));

        Assert.Equal("planMode", exception.ParamName);
    }

    [Theory]
    [InlineData("planSemantics")]
    [InlineData("callSemantics")]
    [InlineData("touchedContract")]
    [InlineData("readPostconditionContract")]
    [InlineData("failureSemantics")]
    [Trait("Size", "Small")]
    public void Constructor_WhenSemanticTextIsBlank_RejectsInvalidValue (string fieldName)
    {
        var exception = Assert.Throws<ArgumentException>(() => new UcliOperationAssuranceContract(
            Array.Empty<UcliOperationSideEffect>(),
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ValidationOnly,
            planSemantics: fieldName == "planSemantics" ? " " : "Validate arguments.",
            callSemantics: fieldName == "callSemantics" ? " " : "Execute the operation.",
            touchedContract: fieldName == "touchedContract" ? " " : "Reports touched resources.",
            readPostconditionContract: fieldName == "readPostconditionContract" ? " " : "Reports stale reads.",
            failureSemantics: fieldName == "failureSemantics" ? " " : "Reports failure.",
            dangerousNotes: Array.Empty<string>()));

        Assert.Equal(fieldName, exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenRequiredTouchedKindIsMissing_RejectsInvalidCombination ()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateAssurance(
            [UcliOperationSideEffect.AssetContentMutation],
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ValidationOnly,
            ["Mutates asset content."]));

        Assert.Equal("touchedKinds", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenPersistingSideEffectHasNoTouchedKind_RejectsInvalidCombination ()
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateAssurance(
            [UcliOperationSideEffect.FilesystemWrite],
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ValidationOnly,
            ["Writes to the filesystem."]));

        Assert.Equal("touchedKinds", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenFiniteValuesAreDuplicated_RejectsInvalidCombination ()
    {
        var sideEffectException = Assert.Throws<ArgumentException>(() => CreateAssurance(
            [UcliOperationSideEffect.ObservesUnityState, UcliOperationSideEffect.ObservesUnityState],
            Array.Empty<UcliTouchedResourceKind>(),
            UcliOperationPlanMode.ValidationOnly,
            Array.Empty<string>()));
        var touchedKindException = Assert.Throws<ArgumentException>(() => CreateAssurance(
            Array.Empty<UcliOperationSideEffect>(),
            [UcliTouchedResourceKind.Asset, UcliTouchedResourceKind.Asset],
            UcliOperationPlanMode.ValidationOnly,
            Array.Empty<string>()));

        Assert.Equal("sideEffects", sideEffectException.ParamName);
        Assert.Equal("touchedKinds", touchedKindException.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_SnapshotsCollectionInputsAndDerivesProjectionFromSnapshot ()
    {
        var sideEffects = new[] { UcliOperationSideEffect.AssetContentMutation };
        var touchedKinds = new[] { UcliTouchedResourceKind.Asset };
        var dangerousNotes = new[] { "The operation can dirty an asset." };

        var assurance = CreateAssurance(
            sideEffects,
            touchedKinds,
            UcliOperationPlanMode.ObservesLiveUnity,
            dangerousNotes);

        sideEffects[0] = UcliOperationSideEffect.ObservesUnityState;
        touchedKinds[0] = UcliTouchedResourceKind.Scene;
        dangerousNotes[0] = "Changed after construction.";

        Assert.Equal([UcliOperationSideEffect.AssetContentMutation], assurance.SideEffects);
        Assert.True(assurance.MayDirty);
        Assert.False(assurance.MayPersist);
        Assert.Equal([UcliTouchedResourceKind.Asset], assurance.TouchedKinds);
        Assert.Equal(["The operation can dirty an asset."], assurance.DangerousNotes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Collections_WhenReadThroughContract_CannotMutateContractState ()
    {
        var assurance = CreateAssurance(
            [UcliOperationSideEffect.AssetContentMutation],
            [UcliTouchedResourceKind.Asset],
            UcliOperationPlanMode.ObservesLiveUnity,
            ["The operation can dirty an asset."]);

        Assert.Throws<NotSupportedException>(
            () => ((IList<UcliOperationSideEffect>)assurance.SideEffects)[0] = UcliOperationSideEffect.ObservesUnityState);
        Assert.Throws<NotSupportedException>(
            () => ((IList<UcliTouchedResourceKind>)assurance.TouchedKinds)[0] = UcliTouchedResourceKind.Scene);
        Assert.Throws<NotSupportedException>(
            () => ((IList<string>)assurance.DangerousNotes)[0] = "Changed after construction.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonConverter_WhenContractIsValid_RoundTripsTypedValuesAndProjection ()
    {
        var assurance = JsonSerializer.Deserialize<UcliOperationAssuranceContract>(
            ValidAssetMutationJson,
            IpcJsonSerializerOptions.Default);

        Assert.NotNull(assurance);
        Assert.Equal([UcliOperationSideEffect.AssetContentMutation], assurance.SideEffects);
        Assert.True(assurance.MayDirty);
        Assert.False(assurance.MayPersist);
        Assert.Equal([UcliTouchedResourceKind.Asset], assurance.TouchedKinds);
        Assert.Equal(UcliOperationPlanMode.ObservesLiveUnity, assurance.PlanMode);

        var serialized = JsonSerializer.Serialize(assurance, IpcJsonSerializerOptions.Default);
        using var document = JsonDocument.Parse(serialized);
        Assert.Equal("assetContentMutation", document.RootElement.GetProperty("sideEffects")[0].GetString());
        Assert.True(document.RootElement.GetProperty("mayDirty").GetBoolean());
        Assert.Equal("asset", document.RootElement.GetProperty("touchedKinds")[0].GetString());
        Assert.Equal("observesLiveUnity", document.RootElement.GetProperty("planMode").GetString());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonConverter_WhenSideEffectIsUnsupported_RejectsAtBoundary ()
    {
        var json = ValidAssetMutationJson.Replace("assetContentMutation", "not-supported", StringComparison.Ordinal);

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<UcliOperationAssuranceContract>(json, IpcJsonSerializerOptions.Default));
    }

    [Theory]
    [InlineData("\"mayDirty\": true", "\"mayDirty\": false")]
    [InlineData("\"mayPersist\": false", "\"mayPersist\": true")]
    [Trait("Size", "Small")]
    public void JsonConverter_WhenProjectionDoesNotMatchSideEffects_RejectsAtBoundary (
        string currentProjection,
        string invalidProjection)
    {
        var json = ValidAssetMutationJson.Replace(currentProjection, invalidProjection, StringComparison.Ordinal);

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<UcliOperationAssuranceContract>(json, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonConverter_WhenRequiredPropertyIsMissing_RejectsAtBoundary ()
    {
        var json = ValidAssetMutationJson.Replace(
            "  \"failureSemantics\": \"Failure may leave a partial mutation.\",\n",
            string.Empty,
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<UcliOperationAssuranceContract>(json, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonConverter_WhenPropertyIsDuplicated_RejectsAtBoundary ()
    {
        var json = ValidAssetMutationJson.Replace(
            "  \"mayDirty\": true,",
            "  \"mayDirty\": true,\n  \"mayDirty\": true,",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<UcliOperationAssuranceContract>(json, IpcJsonSerializerOptions.Default));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void JsonConverter_WhenUnknownPropertyExists_RejectsAtBoundary ()
    {
        var json = ValidAssetMutationJson.Replace(
            "  \"sideEffects\":",
            "  \"unknown\": true,\n  \"sideEffects\":",
            StringComparison.Ordinal);

        Assert.Throws<JsonException>(
            () => JsonSerializer.Deserialize<UcliOperationAssuranceContract>(json, IpcJsonSerializerOptions.Default));
    }

    private static UcliOperationAssuranceContract CreateAssurance (
        IReadOnlyList<UcliOperationSideEffect> sideEffects,
        IReadOnlyList<UcliTouchedResourceKind> touchedKinds,
        UcliOperationPlanMode planMode,
        IReadOnlyList<string> dangerousNotes)
    {
        return UcliOperationDescribeContractValidatorTestData.CreateAssurance(
            sideEffects,
            touchedKinds,
            planMode,
            dangerousNotes);
    }
}
