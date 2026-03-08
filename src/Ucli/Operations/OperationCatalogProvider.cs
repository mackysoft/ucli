using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Ops;

namespace MackySoft.Ucli.Operations;

/// <summary> Builds the operation catalog from discovered operation metadata. </summary>
internal sealed class OperationCatalogProvider : IOperationCatalogProvider
{
    private readonly IInitStatusContextResolver initStatusContextResolver;

    private readonly IOpsCatalogReader opsCatalogReader;

    /// <summary> Initializes a new instance of the <see cref="OperationCatalogProvider" /> class. </summary>
    /// <param name="initStatusContextResolver"> The shared context resolver dependency. </param>
    /// <param name="opsCatalogReader"> The ops catalog reader dependency. </param>
    public OperationCatalogProvider (
        IInitStatusContextResolver initStatusContextResolver,
        IOpsCatalogReader opsCatalogReader)
    {
        this.initStatusContextResolver = initStatusContextResolver ?? throw new ArgumentNullException(nameof(initStatusContextResolver));
        this.opsCatalogReader = opsCatalogReader ?? throw new ArgumentNullException(nameof(opsCatalogReader));
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResult = await initStatusContextResolver.Resolve(
                projectPath: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!contextResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Operation catalog context could not be resolved. {contextResult.Error!.Message}");
        }

        var catalogResult = await opsCatalogReader.Read(
                contextResult.Context!.UnityProject,
                contextResult.Context.Config,
                mode: null,
                timeout: null,
                cancellationToken)
            .ConfigureAwait(false);
        if (!catalogResult.IsSuccess)
        {
            throw new InvalidOperationException(
                $"Operation catalog discovery failed. {catalogResult.Message}");
        }

        var operations = catalogResult.Response!.Operations!;
        var descriptors = new UcliOperationDescriptor[operations.Count];
        for (var i = 0; i < operations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var operation = operations[i];
            if (!UcliOperationKindCodec.TryParse(operation.Kind, out var kind))
            {
                throw new InvalidOperationException(
                    $"Operation kind is invalid for '{operation.Name}'.");
            }

            if (!OperationPolicyCodec.TryParse(operation.Policy, out var policy))
            {
                throw new InvalidOperationException(
                    $"Operation policy is invalid for '{operation.Name}'.");
            }

            descriptors[i] = new UcliOperationDescriptor(
                Name: operation.Name!,
                Kind: kind,
                Policy: policy,
                ArgsSchemaJson: operation.ArgsSchemaJson!);
        }

        return descriptors;
    }
}
