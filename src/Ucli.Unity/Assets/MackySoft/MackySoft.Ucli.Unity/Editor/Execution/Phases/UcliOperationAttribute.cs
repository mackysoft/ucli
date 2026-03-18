using System;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Marks one operation type for bootstrap-time discovery and registration. </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class UcliOperationAttribute : Attribute
    {
    }
}