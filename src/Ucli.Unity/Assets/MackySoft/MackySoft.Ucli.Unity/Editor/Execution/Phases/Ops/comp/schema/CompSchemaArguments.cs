#nullable enable

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents parsed arguments for <c>ucli.comp.schema</c>. </summary>
    internal readonly struct CompSchemaArguments
    {
        public CompSchemaArguments (string typeId)
        {
            TypeId = typeId;
        }

        public string TypeId { get; }
    }
}