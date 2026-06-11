using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Cryptography;
using MackySoft.Ucli.Application.Shared.EnvironmentVariables;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Git;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Shared.Configuration;
using MackySoft.Ucli.Shared.Cryptography;
using MackySoft.Ucli.Shared.EnvironmentVariables;
using MackySoft.Ucli.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Shared.Git;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Hosting.Composition.Common;

/// <summary> Registers shared services reused across feature boundaries. </summary>
internal static class SharedServiceCollectionExtensions
{
    /// <summary> Registers shared services. </summary>
    /// <param name="services"> The target service collection. </param>
    /// <returns> The updated service collection. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="services" /> is <see langword="null" />. </exception>
    public static IServiceCollection AddUcliSharedServices (this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IEnvironmentVariableReader, ProcessEnvironmentVariableReader>();
        services.AddSingleton<UcliConfigSchemaValidator>();
        services.AddSingleton<UcliEffectiveConfigBuilder>();
        services.AddSingleton<UcliConfigCompiler>();
        services.AddSingleton<IUcliConfigStore, UcliConfigStore>();
        services.AddSingleton<IProjectLifecycleLockProvider, FileSystemProjectLifecycleLockProvider>();
        services.AddSingleton<ISha256DigestCalculator, InfrastructureSha256DigestCalculator>();
        services.AddSingleton<IUnityProjectLockFileProbe, UnityProjectLockFileProbe>();
        services.AddSingleton<IUnityEditorInstanceProbe, UnityEditorInstanceProbe>();
        services.AddSingleton<IUnityProjectProcessScanner, UnityProjectProcessScanner>();
        services.AddSingleton<IUnityProjectLockOwnerProbe, UnityProjectLockOwnerProbe>();
        services.AddSingleton<IUnityProjectLockFileCleaner, UnityProjectLockFileCleaner>();
        services.AddSingleton<IUnityProjectLockPreflightService, UnityProjectLockPreflightService>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IGitCommandClient, GitCommandClient>();
        services.AddSingleton<IGitWorktreeListPorcelainParser, GitWorktreeListPorcelainParser>();
        services.AddSingleton<IGitWorktreeQueryService, GitWorktreeQueryService>();
        services.AddSingleton<IJsonContractWriter<IndexOpsCatalogJsonContract>, IndexOpsCatalogJsonContractWriter>();
        services.AddSingleton<IJsonContractWriter<IndexOpsDescribeJsonContract>, IndexOpsDescribeJsonContractWriter>();
        services.AddSingleton<IJsonContractWriter<IndexTypesCatalogJsonContract>, IndexTypesCatalogJsonContractWriter>();
        services.AddSingleton<IJsonContractWriter<IndexSchemasCatalogJsonContract>, IndexSchemasCatalogJsonContractWriter>();
        services.AddSingleton<IJsonContractWriter<IndexInputsManifestJsonContract>, IndexInputsManifestJsonContractWriter>();
        services.AddSingleton<IJsonContractWriter<IndexAssetSearchLookupJsonContract>, IndexAssetSearchLookupJsonContractWriter>();
        services.AddSingleton<IJsonContractWriter<IndexGuidPathLookupJsonContract>, IndexGuidPathLookupJsonContractWriter>();
        services.AddSingleton<IJsonContractWriter<IndexSceneTreeLiteLookupJsonContract>, IndexSceneTreeLiteLookupJsonContractWriter>();
        return services;
    }
}
