using System.Collections.Generic;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one discovered operation snapshot shared by execute and ops-read flows. </summary>
    /// <param name="Registrations"> The discovered operation registrations. </param>
    /// <param name="Catalog"> The serialized ops-catalog response payload. </param>
    internal sealed record UcliOperationCatalogSnapshot (
        IReadOnlyList<UcliOperationRegistration> Registrations,
        IpcOpsReadResponse Catalog);
}