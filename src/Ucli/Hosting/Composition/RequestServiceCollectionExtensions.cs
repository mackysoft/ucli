using MackySoft.Ucli.Features.Requests.Call.UseCases.Call;
using MackySoft.Ucli.Features.Requests.Call.UseCases.Call.Preflight;
using MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan;
using MackySoft.Ucli.Features.Requests.Plan.UseCases.Plan.Preflight;
using MackySoft.Ucli.Features.Requests.Refresh.UseCases.Refresh;
using MackySoft.Ucli.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Features.Requests.Shared.Execution.Phase;
using MackySoft.Ucli.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Features.Requests.Validate.UseCases.Validate;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition;

/// <summary> Registers request feature services for refresh, validate, plan, and call commands. </summary>
internal static class RequestServiceCollectionExtensions
{
    /// <summary> Registers request feature services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliRequestFeatureServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IRequestPreparationService, RequestPreparationService>();
        services.AddSingleton<IRequestStaticValidationPreflightService, RequestStaticValidationPreflightService>();
        services.AddSingleton<IValidateRequestJsonParser, ValidateRequestJsonParser>();

        services.AddSingleton<IPhaseExecutionPreflightService, PhaseExecutionPreflightService>();
        services.AddSingleton<IOperationExecuteService, OperationExecuteService>();

        services.AddSingleton<IOperationCatalogDiscoveryService, OperationCatalogDiscoveryService>();
        services.AddSingleton<IOperationCatalogProvider, OperationCatalogProvider>();
        services.AddSingleton<IOperationCatalog, OperationCatalog>();
        services.AddSingleton<IOperationAuthorizationService, OperationAuthorizationService>();
        services.AddSingleton<IReadIndexValidationCatalogResolver, ReadIndexValidationCatalogResolver>();
        services.AddSingleton<IRequestStaticValidator, RequestStaticValidator>();
        services.AddSingleton<IRequestStaticValidationService, RequestStaticValidationService>();

        services.AddSingleton<IPlanCommandPreflightService, PlanCommandPreflightService>();
        services.AddSingleton<IPlanService, PlanService>();

        services.AddSingleton<ICallDangerousOperationGuard, CallDangerousOperationGuard>();
        services.AddSingleton<ICallUnityExecutionService, CallUnityExecutionService>();
        services.AddSingleton<ICallCommandPreflightService, CallCommandPreflightService>();
        services.AddSingleton<ICallService, CallService>();

        services.AddSingleton<IRefreshService, RefreshService>();
        services.AddSingleton<IValidateService, ValidateService>();
        return services;
    }
}