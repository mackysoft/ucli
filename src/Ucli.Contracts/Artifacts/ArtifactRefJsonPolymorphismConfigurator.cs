using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace MackySoft.Ucli.Contracts;

/// <summary> Configures the effective artifact-locator tagged union shared by CLI and IPC serialization. </summary>
internal static class ArtifactRefJsonPolymorphismConfigurator
{
    public static bool TryConfigure (JsonTypeInfo typeInfo)
    {
        if (typeInfo.Type != typeof(ArtifactRef))
        {
            return false;
        }

        var locationKindPropertyName = typeInfo.Options.PropertyNamingPolicy?.ConvertName(
                nameof(ArtifactRef.LocationKind))
            ?? nameof(ArtifactRef.LocationKind);
        var options = new JsonPolymorphismOptions
        {
            TypeDiscriminatorPropertyName = locationKindPropertyName,
            IgnoreUnrecognizedTypeDiscriminators = false,
            UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization,
        };
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(PathArtifactRef),
            TextVocabulary.GetText(ArtifactLocationKind.Path)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(UriArtifactRef),
            TextVocabulary.GetText(ArtifactLocationKind.Uri)));
        options.DerivedTypes.Add(new JsonDerivedType(
            typeof(PathAndUriArtifactRef),
            TextVocabulary.GetText(ArtifactLocationKind.PathAndUri)));
        typeInfo.PolymorphismOptions = options;
        return true;
    }
}
