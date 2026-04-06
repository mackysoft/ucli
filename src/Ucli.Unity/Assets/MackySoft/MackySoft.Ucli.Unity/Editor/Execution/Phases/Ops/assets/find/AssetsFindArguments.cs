#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents parsed arguments for <c>ucli.assets.find</c>. </summary>
    internal readonly struct AssetsFindArguments
    {
        public AssetsFindArguments (
            string? typeId,
            string? pathPrefix,
            string? nameContains)
        {
            TypeId = typeId;
            PathPrefix = pathPrefix;
            NameContains = nameContains;
        }

        public string? TypeId { get; }

        public string? PathPrefix { get; }

        public string? NameContains { get; }
    }
}
