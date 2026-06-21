using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the resolved build inputs emitted in public output. </summary>
internal sealed record BuildInputsOutput (
    string InputKind,
    BuildTargetOutput Target,
    BuildScenesOutput Scenes,
    BuildOptionsOutput Options,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    BuildUnityBuildProfileOutput? UnityBuildProfile);
