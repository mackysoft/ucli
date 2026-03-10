#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one parsed argument payload for <c>ucli.prefab.create</c>. </summary>
    internal readonly struct PrefabCreateArguments
    {
        public PrefabCreateArguments (
            UnityObjectReference targetReference,
            string prefabPath)
        {
            TargetReference = targetReference;
            PrefabPath = prefabPath;
        }

        public UnityObjectReference TargetReference { get; }

        public string PrefabPath { get; }
    }
}