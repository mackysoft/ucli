using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

/// <summary>Represents the user-authored request document accepted by request commands.</summary>
[Title("uCLI request")]
[Description("A sequence of operation and edit steps executed in array order.")]
internal readonly record struct UcliRequestJsonContract (
    [property: JsonRequired] IReadOnlyList<UcliRequestStepJsonContract> Steps);

/// <summary>Represents the internal execute arguments transported from the CLI to Unity.</summary>
internal sealed class IpcExecuteArgumentsJsonContract
{
    /// <summary>Gets the IPC protocol version.</summary>
    [JsonRequired]
    public int ProtocolVersion { get; init; }

    /// <summary>Gets the ordered request steps.</summary>
    [JsonRequired]
    public IReadOnlyList<UcliRequestStepJsonContract> Steps { get; init; } =
        Array.Empty<UcliRequestStepJsonContract>();
}

/// <summary>Represents one tagged request step.</summary>
internal abstract class UcliRequestStepJsonContract
{
}

/// <summary>Represents one direct operation invocation.</summary>
[Description("Invokes one operation from the current operation catalog.")]
internal sealed class UcliOperationRequestStepJsonContract : UcliRequestStepJsonContract
{
    /// <summary>Gets the operation name.</summary>
    [JsonRequired]
    public string Op { get; init; } = null!;

    /// <summary>Gets the operation-specific argument object.</summary>
    [JsonRequired]
    public Dictionary<string, JsonElement> Args { get; init; } = null!;
}

/// <summary>Represents one edit step that is lowered to primitive operations.</summary>
[Title("uCLI edit step")]
[Description("Selects targets in one edit context, applies one or more actions, and declares a save boundary.")]
internal sealed class UcliEditRequestStepJsonContract : UcliRequestStepJsonContract
{
    /// <summary>Gets the edit persistence context.</summary>
    [JsonRequired]
    public UcliEditContextJsonContract On { get; init; } = null!;

    /// <summary>Gets the target selection.</summary>
    [JsonRequired]
    public UcliEditSelectionJsonContract Select { get; init; } = null!;

    /// <summary>Gets the non-empty action sequence.</summary>
    [JsonRequired]
    [ItemCount(1, int.MaxValue)]
    public IReadOnlyList<UcliEditActionJsonContract> Actions { get; init; } =
        Array.Empty<UcliEditActionJsonContract>();

    /// <summary>Gets the save boundary applied after the actions.</summary>
    [JsonRequired]
    public IpcEditStepContract.CommitKind Commit { get; init; }
}

/// <summary>Represents one tagged edit persistence context.</summary>
internal abstract class UcliEditContextJsonContract
{
}

internal sealed class UcliSceneEditContextJsonContract : UcliEditContextJsonContract
{
    [JsonRequired]
    public string Path { get; init; } = null!;
}

internal sealed class UcliPrefabEditContextJsonContract : UcliEditContextJsonContract
{
    [JsonRequired]
    public string Path { get; init; } = null!;
}

internal sealed class UcliAssetEditContextJsonContract : UcliEditContextJsonContract
{
    [JsonRequired]
    public string Path { get; init; } = null!;
}

internal sealed class UcliProjectEditContextJsonContract : UcliEditContextJsonContract
{
}

/// <summary>Represents one tagged edit target selection.</summary>
internal abstract class UcliEditSelectionJsonContract
{
    [JsonRequired]
    public IpcEditStepContract.CardinalityKind Cardinality { get; init; }
}

internal sealed class UcliGameObjectEditSelectionJsonContract : UcliEditSelectionJsonContract
{
    [JsonRequired]
    public string Path { get; init; } = null!;

    public string? Component { get; init; }
}

internal sealed class UcliSelfEditSelectionJsonContract : UcliEditSelectionJsonContract
{
}

internal sealed class UcliProjectAssetEditSelectionJsonContract : UcliEditSelectionJsonContract
{
    [JsonRequired]
    public string Path { get; init; } = null!;
}

internal sealed class UcliFromEditSelectionJsonContract : UcliEditSelectionJsonContract
{
    [JsonRequired]
    public UcliEditSelectionSourceOperation Op { get; init; }

    [JsonRequired]
    public UcliEditSceneQueryArgsJsonContract Args { get; init; } = null!;
}

/// <summary>Defines the supported tagged edit-selection forms.</summary>
[VocabularyDefinition]
internal enum UcliEditSelectionJsonKind
{
    [VocabularyText("gameObject")]
    GameObject = 0,

    [VocabularyText("self")]
    Self,

    [VocabularyText("projectAsset")]
    ProjectAsset,

    [VocabularyText("from")]
    From,
}

/// <summary>Defines the only operation that can supply edit-selection candidates.</summary>
[VocabularyDefinition]
internal enum UcliEditSelectionSourceOperation
{
    [VocabularyText(UcliPrimitiveOperationNames.SceneQuery)]
    SceneQuery = 0,
}

/// <summary>
/// Represents scene-query arguments whose scene is supplied by the enclosing edit context.
/// The same value is consumed from <c>select.from.args</c> during request execution.
/// </summary>
internal sealed class UcliEditSceneQueryArgsJsonContract
{
    public string? PathPrefix { get; init; }

    public string? ComponentType { get; init; }
}

/// <summary>Represents one tagged edit action.</summary>
internal abstract class UcliEditActionJsonContract
{
}

internal sealed class UcliSetEditActionJsonContract : UcliEditActionJsonContract
{
    public string? Target { get; init; }

    [JsonRequired]
    [PropertyCount(1, int.MaxValue)]
    public Dictionary<string, JsonElement> Values { get; init; } = null!;
}

internal sealed class UcliEnsureComponentEditActionJsonContract : UcliEditActionJsonContract
{
    public string? Target { get; init; }

    [JsonRequired]
    public string Type { get; init; } = null!;

    [JsonPropertyName("as")]
    public string? Alias { get; init; }
}

internal sealed class UcliCreateObjectEditActionJsonContract : UcliEditActionJsonContract
{
    [JsonRequired]
    public string Name { get; init; } = null!;

    [JsonPropertyName("as")]
    public string? Alias { get; init; }
}

internal sealed class UcliCreateAssetEditActionJsonContract : UcliEditActionJsonContract
{
    [JsonRequired]
    public string Type { get; init; } = null!;

    [JsonRequired]
    public string Path { get; init; } = null!;
}

internal sealed class UcliCreatePrefabEditActionJsonContract : UcliEditActionJsonContract
{
    public string? Target { get; init; }

    [JsonRequired]
    public string Path { get; init; } = null!;
}

internal abstract class UcliPrefabOverridesEditActionJsonContract : UcliEditActionJsonContract
{
    public string? Target { get; init; }

    [JsonRequired]
    public string TargetAssetPath { get; init; } = null!;

    [ItemCount(1, int.MaxValue)]
    public IReadOnlyList<string>? PropertyPaths { get; init; }
}

internal sealed class UcliApplyPrefabOverridesEditActionJsonContract : UcliPrefabOverridesEditActionJsonContract
{
}

internal sealed class UcliRevertPrefabOverridesEditActionJsonContract : UcliPrefabOverridesEditActionJsonContract
{
}

internal sealed class UcliDeleteEditActionJsonContract : UcliEditActionJsonContract
{
    public string? Target { get; init; }
}

internal sealed class UcliReparentEditActionJsonContract : UcliEditActionJsonContract
{
    public string? Target { get; init; }

    [JsonRequired]
    public string Parent { get; init; } = null!;
}
