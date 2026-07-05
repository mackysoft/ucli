namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;

internal static class RequestStaticValidatorTestExtensions
{
    public static ValueTask<ValidationResult> ValidateAsync (
        this IRequestStaticValidator validator,
        ValidateRequest request,
        IReadOnlyList<UcliOperationDescriptor>? operations,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(config);
        cancellationToken.ThrowIfCancellationRequested();

        return validator.ValidateAsync(
            request,
            operations is null
                ? RequestStaticValidationCatalog.Unavailable
                : RequestStaticValidationCatalog.Available(operations),
            config,
            cancellationToken);
    }

    public static async ValueTask<ValidationResult> ValidateAsync (
        this IRequestStaticValidator validator,
        ValidateRequest request,
        ResolvedUnityProjectContext unityProject,
        UcliConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(unityProject);
        ArgumentNullException.ThrowIfNull(config);

        var operations = await new InMemoryOperationCatalogProvider()
            .GetOperationsAsync(cancellationToken)
            .ConfigureAwait(false);
        return await validator.ValidateAsync(
                request,
                RequestStaticValidationCatalog.Available(operations),
                config,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
