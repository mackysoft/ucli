using System.Text.Json;
using MackySoft.Ucli.Application.Features.Assurance.Compile;
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
                  "kind": "ready.lifecycle",
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
                    "kind": "{{ReadyValidityKindValues.ProbeOnly}}",
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
