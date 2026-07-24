using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Payload;

/// <summary> Represents resolved build scene input. </summary>
internal sealed record BuildScenesOutput
{
    public BuildScenesOutput (
        BuildProfileSceneSource Source,
        IReadOnlyList<SceneAssetPath> Paths)
    {
        if (!TextVocabulary.IsDefined(Source))
        {
            throw new ArgumentOutOfRangeException(nameof(Source), Source, "Build scene source must be specified.");
        }

        this.Source = Source;
        this.Paths = Paths ?? throw new ArgumentNullException(nameof(Paths));
    }

    public BuildProfileSceneSource Source { get; }

    public IReadOnlyList<SceneAssetPath> Paths { get; }
}
