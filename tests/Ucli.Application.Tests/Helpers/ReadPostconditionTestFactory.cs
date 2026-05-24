using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal static class ReadPostconditionTestFactory
{
    public static OperationExecutionReadPostcondition Create (
        IReadOnlyList<IpcExecuteReadPostconditionRequirement> requirements)
    {
        var mappedRequirements = new OperationExecutionReadPostconditionRequirement[requirements.Count];
        for (var i = 0; i < requirements.Count; i++)
        {
            var requirement = requirements[i];
            mappedRequirements[i] = new OperationExecutionReadPostconditionRequirement(
                requirement.Surface,
                requirement.MinSafeGeneratedAtUtc)
            {
                ScenePath = requirement.ScenePath,
            };
        }

        return new OperationExecutionReadPostcondition(mappedRequirements);
    }
}
