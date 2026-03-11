using System.Text.Json;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution.OperationExecute;
using MackySoft.Ucli.Operations;

namespace MackySoft.Ucli.Refresh;

/// <summary> Executes the <c>refresh</c> workflow by dispatching the fixed <c>ucli.project.refresh</c> operation definition. </summary>
internal sealed class RefreshService : IRefreshService
{
    private static readonly OperationExecuteDefinition RefreshOperation = new(
        Command: UcliCommandIds.Refresh,
        OperationId: "refresh",
        Descriptor: new UcliOperationDescriptor(
            Name: "ucli.project.refresh",
            Kind: UcliOperationKind.Mutation,
            Policy: OperationPolicy.Advanced,
            ArgsSchemaJson: """{"type":"object","additionalProperties":false}"""),
        Args: JsonSerializer.SerializeToElement(new { }));

    private readonly IOperationExecuteService operationExecuteService;

    /// <summary> Initializes a new instance of the <see cref="RefreshService" /> class. </summary>
    /// <param name="operationExecuteService"> The shared fixed-operation execution service dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="operationExecuteService" /> is <see langword="null" />. </exception>
    public RefreshService (IOperationExecuteService operationExecuteService)
    {
        this.operationExecuteService = operationExecuteService ?? throw new ArgumentNullException(nameof(operationExecuteService));
    }

    /// <inheritdoc />
    public ValueTask<OperationExecuteResult> Execute (
        string? projectPath,
        string? mode,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        return operationExecuteService.Execute(RefreshOperation, projectPath, mode, timeout, cancellationToken);
    }
}