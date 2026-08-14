using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;

namespace MackySoft.Ucli.Contracts.Ipc;

public sealed record CsEvalTouchedResources
{
    [JsonConstructor]
    public CsEvalTouchedResources (
        bool noChanges,
        IReadOnlyList<string> scenes,
        IReadOnlyList<string> prefabs,
        IReadOnlyList<string> assets,
        IReadOnlyList<string> projectSettings)
    {
        NoChanges = noChanges;
        Scenes = Normalize(scenes, nameof(scenes));
        Prefabs = Normalize(prefabs, nameof(prefabs));
        Assets = Normalize(assets, nameof(assets));
        ProjectSettings = Normalize(projectSettings, nameof(projectSettings));
        var hasTouchedResource = Scenes.Count != 0 || Prefabs.Count != 0 || Assets.Count != 0 || ProjectSettings.Count != 0;
        if (noChanges && hasTouchedResource)
        {
            throw new ArgumentException("No-change declarations cannot include touched resources.", nameof(noChanges));
        }

        if (!noChanges && !hasTouchedResource)
        {
            throw new ArgumentException("Touched resources require a no-change declaration or at least one path.", nameof(noChanges));
        }
    }

    [JsonInclude]
    [JsonRequired]
    [Description("Whether evaluated code explicitly declared that it made no changes.")]
    public bool NoChanges { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<string> Scenes { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<string> Prefabs { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<string> Assets { get; private init; }

    [JsonInclude]
    [JsonRequired]
    public IReadOnlyList<string> ProjectSettings { get; private init; }

    private static IReadOnlyList<string> Normalize (IReadOnlyList<string> values, string parameterName)
    {
        var snapshot = ContractArgumentGuard.RequireItems(values, parameterName);
        return snapshot
            .Select(value => ContractArgumentGuard.RequireValue(value, parameterName))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();
    }
}
