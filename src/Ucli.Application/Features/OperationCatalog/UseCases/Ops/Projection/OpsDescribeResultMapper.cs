using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;

/// <summary> Implements mapping from catalog snapshots to command-facing <c>ops describe</c> results. </summary>
internal sealed class OpsDescribeResultMapper : IOpsDescribeResultMapper
{
    private readonly OpsReadIndexInfoMapper readIndexInfoMapper;

    /// <summary> Initializes a new instance of the <see cref="OpsDescribeResultMapper" /> class. </summary>
    /// <param name="readIndexInfoMapper"> The read-index info mapper dependency. </param>
    public OpsDescribeResultMapper (OpsReadIndexInfoMapper readIndexInfoMapper)
    {
        this.readIndexInfoMapper = readIndexInfoMapper ?? throw new ArgumentNullException(nameof(readIndexInfoMapper));
    }

    /// <inheritdoc />
    public OpsDescribeServiceResult Map (OpsDescribeReadOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var operation = output.Operation;
        var operationName = operation.Name;

        return OpsDescribeServiceResult.Success(
            new OpsDescribeExecutionOutput(
                Operation: new OpsOperationDetail(
                    name: operation.Name,
                    kind: operation.Kind,
                    policy: operation.Policy,
                    playModeSupport: operation.PlayModeSupport,
                    descriptorDigest: operation.DescriptorDigest,
                    description: operation.Description,
                    argsContract: operation.ArgsContract,
                    resultContract: operation.ResultContract,
                    verdictContract: operation.VerdictContract,
                    assurance: operation.Assurance,
                    codeContract: operation.CodeContract),
                ReadIndex: readIndexInfoMapper.Map(output.AccessInfo)),
            $"uCLI ops describe completed for '{operationName}'.");
    }
}
