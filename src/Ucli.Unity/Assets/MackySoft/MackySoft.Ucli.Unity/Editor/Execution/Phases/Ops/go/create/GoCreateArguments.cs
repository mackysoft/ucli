#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents parsed arguments for <c>ucli.go.create</c>. </summary>
    internal readonly struct GoCreateArguments
    {
        public GoCreateArguments (
            string name,
            string? scenePath,
            UnityObjectReference parentReference,
            bool hasParentReference)
        {
            Name = name;
            ScenePath = scenePath;
            ParentReference = parentReference;
            HasParentReference = hasParentReference;
        }

        public string Name { get; }

        public string? ScenePath { get; }

        public UnityObjectReference ParentReference { get; }

        public bool HasParentReference { get; }
    }
}