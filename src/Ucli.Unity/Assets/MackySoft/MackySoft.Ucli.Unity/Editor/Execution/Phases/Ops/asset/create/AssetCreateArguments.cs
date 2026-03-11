namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents parsed arguments for <c>ucli.asset.create</c>. </summary>
    internal readonly struct AssetCreateArguments
    {
        public AssetCreateArguments (
            string typeId,
            string assetPath)
        {
            TypeId = typeId;
            AssetPath = assetPath;
        }

        public string TypeId { get; }

        public string AssetPath { get; }
    }
}