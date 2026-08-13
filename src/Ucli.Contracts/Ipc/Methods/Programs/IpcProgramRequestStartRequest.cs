using System.Text.Json.Serialization;
using System.Text;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Starts one Program-owned logical Request execution exactly once. </summary>
public sealed record IpcProgramRequestStartRequest
{
    [JsonConstructor]
    public IpcProgramRequestStartRequest (Guid executionId, IpcProgramRequestExecutionBinding binding, IpcExecuteRequest request)
    {
        if (executionId == Guid.Empty) throw new ArgumentException("Execution id must not be empty.", nameof(executionId));
        ExecutionId = executionId;
        Binding = binding ?? throw new ArgumentNullException(nameof(binding));
        Request = request ?? throw new ArgumentNullException(nameof(request));
        if (!string.Equals(Request.Command, "call", StringComparison.Ordinal))
        {
            throw new ArgumentException("Program request start can execute only a call request.", nameof(request));
        }
        var planToken = StringValueNormalizer.TrimToNull(Request.PlanToken);
        var planTokenDigest = planToken is null ? null : Sha256Digest.Compute(Encoding.UTF8.GetBytes(planToken));
        if (Binding.PlanTokenDigest != planTokenDigest)
        {
            throw new ArgumentException("Program request plan token does not match its fixed execution binding.", nameof(request));
        }
    }

    [JsonInclude, JsonRequired] public Guid ExecutionId { get; private init; }
    [JsonInclude, JsonRequired] public IpcProgramRequestExecutionBinding Binding { get; private init; }
    [JsonInclude, JsonRequired] public IpcExecuteRequest Request { get; private init; }
}
