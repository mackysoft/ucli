namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one touched persistence-unit kind. </summary>
    public enum OperationTouchKind
    {
        /// <summary> The touched unit kind is unknown. </summary>
        Unknown = 0,

        /// <summary> The touched unit is a scene asset. </summary>
        Scene = 1,

        /// <summary> The touched unit is a prefab asset. </summary>
        Prefab = 2,

        /// <summary> The touched unit is a generic asset. </summary>
        Asset = 3,

        /// <summary> The touched unit is a project settings asset. </summary>
        ProjectSettings = 4,
    }
}