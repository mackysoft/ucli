using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Shared.Execution.ReadPostcondition;

/// <summary> Represents the persisted mutation read-postcondition store document. </summary>
/// <param name="SchemaVersion"> The store schema version. </param>
/// <param name="Requirements"> The merged read requirements. </param>
internal sealed record MutationReadPostconditionDocument (
    int SchemaVersion,
    IReadOnlyList<IpcExecuteReadPostconditionRequirement> Requirements);
