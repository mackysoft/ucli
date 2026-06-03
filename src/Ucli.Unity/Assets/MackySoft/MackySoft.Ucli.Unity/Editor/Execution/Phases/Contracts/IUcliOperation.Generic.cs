namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Marks one operation whose implementation body is authored against typed args and result contracts. </summary>
    /// <typeparam name="TArgs"> The operation args contract type. </typeparam>
    /// <typeparam name="TResult"> The operation result contract type. </typeparam>
    public interface IUcliOperation<TArgs, TResult> : IUcliOperation
    {
    }
}
