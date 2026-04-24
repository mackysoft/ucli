using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Execution.ReadPostcondition;

namespace MackySoft.Ucli.Tests;

internal sealed class TestMutationReadPostconditionStore : IMutationReadPostconditionStore
{
    public int ReadCallCount { get; private set; }

    public int WriteCallCount { get; private set; }

    public string? LastStorageRoot { get; private set; }

    public string? LastProjectFingerprint { get; private set; }

    public IpcExecuteReadPostcondition? LastWrittenReadPostcondition { get; private set; }

    public MutationReadPostconditionReadResult ReadResult { get; set; }
        = MutationReadPostconditionReadResult.Success(null);

    public MutationReadPostconditionStoreOperationResult WriteResult { get; set; }
        = MutationReadPostconditionStoreOperationResult.Success();

    public ValueTask<MutationReadPostconditionReadResult> ReadOrNull (
        string storageRoot,
        string projectFingerprint,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ReadCallCount++;
        LastStorageRoot = storageRoot;
        LastProjectFingerprint = projectFingerprint;
        return ValueTask.FromResult(ReadResult);
    }

    public ValueTask<MutationReadPostconditionStoreOperationResult> WriteMerged (
        string storageRoot,
        string projectFingerprint,
        IpcExecuteReadPostcondition readPostcondition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(readPostcondition);
        WriteCallCount++;
        LastStorageRoot = storageRoot;
        LastProjectFingerprint = projectFingerprint;
        LastWrittenReadPostcondition = readPostcondition;
        return ValueTask.FromResult(WriteResult);
    }
}