using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Ready;
using MackySoft.Ucli.Application.Features.Assurance.Semantics;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Assets;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;
using MackySoft.Ucli.Hosting.Composition.Common;
using MackySoft.Ucli.UnityIntegration.Indexing.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MackySoft.Ucli.Tests.Hosting.Composition;

public sealed class UcliServiceCollectionExtensionsTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void AddUcliServices_RegistersSystemTimeProviderAsSingleton ()
    {
        var services = new ServiceCollection();
        services.AddUcliServices();
        var descriptor = Assert.Single(
            services,
            candidate => candidate.ServiceType == typeof(TimeProvider));

        using var serviceProvider = services.BuildServiceProvider();

        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Same(TimeProvider.System, descriptor.ImplementationInstance);
        Assert.Same(TimeProvider.System, serviceProvider.GetRequiredService<TimeProvider>());
        Assert.Same(
            serviceProvider.GetRequiredService<TimeProvider>(),
            serviceProvider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddUcliServices_PreservesRegisteredTimeProvider ()
    {
        var timeProvider = new ManualTimeProvider();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(timeProvider);
        services.AddUcliServices();

        using var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        Assert.Same(timeProvider, serviceProvider.GetRequiredService<TimeProvider>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddUcliServices_ResolvesGuidGeneratorWithNonEmptyGuid ()
    {
        var services = new ServiceCollection();
        services.AddUcliServices();

        using var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        Assert.NotEqual(
            Guid.Empty,
            serviceProvider.GetRequiredService<IGuidGenerator>().Generate());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddUcliServices_ResolvesReadIndexPolicyAndAdapterGraph ()
    {
        var services = new ServiceCollection();
        services.AddUcliServices();

        using var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });

        Assert.NotNull(serviceProvider.GetRequiredService<IReadIndexArtifactReader>());
        Assert.NotNull(serviceProvider.GetRequiredService<ICompileService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ICompileRunArtifactReader>());
        Assert.NotNull(serviceProvider.GetRequiredService<ICompileRunArtifactStore>());
        Assert.NotNull(serviceProvider.GetRequiredService<IReadIndexArtifactWriter>());
        Assert.NotNull(serviceProvider.GetRequiredService<IReadIndexFreshnessEvaluator>());
        Assert.NotNull(serviceProvider.GetRequiredService<IOpsCatalogSourceRefreshService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IAssetLookupSourceRefreshService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISceneTreeLiteSourceRefreshService>());
        Assert.NotNull(serviceProvider.GetRequiredService<ISceneTreeLiteDirtySourceProbeService>());
        Assert.NotNull(serviceProvider.GetRequiredService<IPersistedOpsCatalogPersistenceArtifactsReader>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AddUcliServices_ResolvesReadyAssuranceSemanticInvariantRule ()
    {
        var services = new ServiceCollection();
        services.AddUcliServices();

        using var serviceProvider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        var validator = serviceProvider.GetRequiredService<AssuranceSemanticInvariantValidator>();

        using var document = JsonDocument.Parse(
            $$"""
            {
              "verdict": "pass",
              "target": "execution",
              "requestedMode": "auto",
              "resolvedMode": "oneshot",
              "sessionKind": "transientProbe",
              "verifiers": [
                {
                  "id": "ready.lifecycle",
                  "kind": "ready",
                  "deterministic": false,
                  "required": true,
                  "primaryClaims": [
                    "{{ReadyClaimCodes.UnityReadyExecution}}"
                  ],
                  "effects": []
                }
              ],
              "claims": [
                {
                  "id": "{{ReadyClaimCodes.UnityReadyExecution}}",
                  "status": "passed",
                  "coverage": "full",
                  "required": true,
                  "verifierRef": "ready.lifecycle",
                  "validity": {
                    "kind": "{{ContractLiteralCodec.ToValue(ReadyValidityKind.ProbeOnly)}}",
                    "guaranteesReusableSession": true
                  },
                  "evidence": [],
                  "residualRisks": []
                }
              ],
              "reports": {},
              "residualRisks": []
            }
            """);

        var result = validator.Validate(document.RootElement);

        Assert.Contains(
            result.Violations,
            violation => string.Equals(
                violation.Path,
                "$.claims[0].validity.guaranteesReusableSession",
                StringComparison.Ordinal));
    }
}
