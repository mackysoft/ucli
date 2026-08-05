using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Contracts.Recording;

namespace MackySoft.Ucli.Hosting.Cli.Recording;

/// <summary>Represents the closed successful payload of <c>recording status</c>.</summary>
internal abstract record GameViewRecordingStatusCommandPayload
{
    private protected GameViewRecordingStatusCommandPayload (
        UnityProjectIdentity project,
        GameViewRecordingCapability capability,
        GameViewRecordingSelection recordingSelection)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        Capability = capability ?? throw new ArgumentNullException(nameof(capability));
        RecordingSelection = recordingSelection ?? throw new ArgumentNullException(nameof(recordingSelection));
    }

    public UnityProjectIdentity Project { get; }

    public GameViewRecordingCapability Capability { get; }

    public GameViewRecordingSelection RecordingSelection { get; }

    public static GameViewRecordingStatusCommandPayload Create (
        GameViewRecordingStatusPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return new StatusPayload(
            payload.Project,
            payload.Capability,
            payload.RecordingSelection);
    }

    public static bool TryConfigure (JsonTypeInfo typeInfo)
    {
        ArgumentNullException.ThrowIfNull(typeInfo);
        if (typeInfo.Type != typeof(GameViewRecordingStatusCommandPayload))
        {
            return false;
        }

        typeInfo.PolymorphismOptions = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = "payloadKind",
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
            DerivedTypes =
            {
                new JsonDerivedType(
                    typeof(StatusPayload),
                    TextVocabulary.GetText(GameViewRecordingPayloadKind.Status)),
            },
        };
        return true;
    }

    internal sealed record StatusPayload : GameViewRecordingStatusCommandPayload
    {
        public StatusPayload (
            UnityProjectIdentity project,
            GameViewRecordingCapability capability,
            GameViewRecordingSelection recordingSelection)
            : base(project, capability, recordingSelection)
        {
        }
    }
}
