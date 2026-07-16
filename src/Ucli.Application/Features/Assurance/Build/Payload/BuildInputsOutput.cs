using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents the resolved build inputs emitted in public output. </summary>
internal sealed record BuildInputsOutput
{
    public BuildInputsOutput (
        BuildProfileInputsKind InputKind,
        BuildTargetOutput Target,
        BuildScenesOutput Scenes,
        BuildOptionsOutput Options,
        BuildUnityBuildProfileOutput? UnityBuildProfile)
    {
        if (!ContractLiteralCodec.IsDefined(InputKind))
        {
            throw new ArgumentOutOfRangeException(nameof(InputKind), InputKind, "Build input kind must be specified.");
        }

        this.InputKind = InputKind;
        this.Target = Target ?? throw new ArgumentNullException(nameof(Target));
        this.Scenes = Scenes ?? throw new ArgumentNullException(nameof(Scenes));
        this.Options = Options ?? throw new ArgumentNullException(nameof(Options));
        this.UnityBuildProfile = UnityBuildProfile;
    }

    public BuildProfileInputsKind InputKind { get; }

    public BuildTargetOutput Target { get; }

    public BuildScenesOutput Scenes { get; }

    public BuildOptionsOutput Options { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BuildUnityBuildProfileOutput? UnityBuildProfile { get; }
}
