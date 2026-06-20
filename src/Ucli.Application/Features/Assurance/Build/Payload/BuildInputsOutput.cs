using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the resolved build input source and derived BuildPipeline inputs. </summary>
internal sealed record BuildInputsOutput (
    string InputKind,
    string BuildTarget,
    BuildScenesOutput Scenes,
    BuildOptionsOutput Options,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    BuildUnityBuildProfileOutput? UnityBuildProfile);
