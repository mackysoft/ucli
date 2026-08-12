using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.CsEval;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Runtime;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using MackySoft.Ucli.Contracts.Operations;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UcliOperationDiscovererTests
    {
        private const string AssemblyCSharpEditorFixtureOperationName = "ucli.tests.assembly-csharp-editor.discover";
        private const string AssemblyCSharpEditorAssemblyName = "Assembly-CSharp-Editor";
        private const string RemovedProjectRefreshOperationName = "ucli.project.refresh";

        private readonly ServiceProvider operationServiceProvider = CreateOperationServiceProvider();

        internal static ServiceProvider CreateOperationServiceProvider ()
        {
            return new ServiceCollection()
                .AddSingleton<IUnityMutationLaneControl>(new UnexpectedMutationLaneControl())
                .AddSingleton<IUnityEditorReadinessGate>(new StubUnityEditorReadinessGate())
                .AddUnityOperationServices()
                .BuildServiceProvider(new ServiceProviderOptions
                {
                    ValidateOnBuild = true,
                    ValidateScopes = true,
                });
        }

        [OneTimeTearDown]
        public void DisposeOperationServiceProvider ()
        {
            operationServiceProvider.Dispose();
        }

        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenTypeIsValidOperation_ReturnsOperationInstance ()
        {
            var operations = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
            {
                typeof(DiscoverableOperation),
            }, operationServiceProvider);

            Assert.That(operations.Count, Is.EqualTo(1));
            Assert.That(operations[0].Operation, Is.TypeOf<DiscoverableOperation>());
            Assert.That(operations[0].Metadata.OperationName, Is.EqualTo("ucli.tests.discover"));
        }

        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenConstructorDependencyIsRegistered_UsesServiceProvider ()
        {
            var dependency = new RegisteredOperationDependency();
            var serviceProvider = new SingleServiceProvider(dependency);

            var operations = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
            {
                typeof(RegisteredDependencyOperation),
            }, serviceProvider);

            Assert.That(operations.Count, Is.EqualTo(1));
            var operation = (RegisteredDependencyOperation)operations[0].Operation;
            Assert.That(operation.Dependency, Is.SameAs(dependency));
        }

        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenConcreteDependencyIsUnregistered_ThrowsInvalidOperationException ()
        {
            var serviceProvider = new SingleServiceProvider(null);

            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
                {
                    typeof(UnregisteredConcreteDependencyOperation),
                }, serviceProvider);
            });
        }

        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenConstructorIsNonPublic_ThrowsInvalidOperationException ()
        {
            var serviceProvider = new SingleServiceProvider(null);

            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
                {
                    typeof(PrivateConstructorOperation),
                }, serviceProvider);
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenPublicOperationMayCreatePreviewState_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = UcliOperationMetadata.CreateWithoutVerdict<UcliEmptyArgs, UcliNoResult>(
                    operationName: "ucli.tests.public-preview-state",
                    kind: UcliOperationKind.Command,
                    description: "Public preview-state operation.",
                    assurance: CreatePreviewStateAssurance(),
                    requiresPreCallPlanReplay: false,
                    exposure: UcliOperationExposure.Public,
                    playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                    codeContract: null);
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenEditLoweringOnlyOperationMayCreatePreviewState_ReturnsMetadata ()
        {
            var editLoweringOnlyMetadata = UcliOperationMetadata.CreateWithoutVerdict<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.edit-preview-state",
                kind: UcliOperationKind.Command,
                description: "Edit-only preview-state operation.",
                assurance: CreatePreviewStateAssurance(),
                requiresPreCallPlanReplay: false,
                exposure: UcliOperationExposure.EditLoweringOnly,
                playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                codeContract: null);

            Assert.That(editLoweringOnlyMetadata.Exposure, Is.EqualTo(UcliOperationExposure.EditLoweringOnly));
        }

        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenAttributedTypeDoesNotImplementOperation_ThrowsInvalidOperationException ()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
                {
                    typeof(InvalidAttributedType),
                }, operationServiceProvider);
            });
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenCurrentDomainContainsInvalidAttributedTestType_IgnoresTestAssembly ()
        {
            var operations = UcliOperationDiscoverer.Discover(operationServiceProvider);

            Assert.That(operations.Count, Is.GreaterThan(0));

            var containsResolveOperation = false;
            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i].Metadata.OperationName == UcliPrimitiveOperationNames.Resolve)
                {
                    containsResolveOperation = true;
                    break;
                }
            }

            Assert.That(containsResolveOperation, Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenAssemblyCSharpEditorOperationExists_ReturnsUserDefinedOperation ()
        {
            var operations = UcliOperationDiscoverer.Discover(operationServiceProvider);

            var registration = FindRegistration(operations, AssemblyCSharpEditorFixtureOperationName);

            Assert.That(registration.Operation.GetType().Assembly.GetName().Name, Is.EqualTo(AssemblyCSharpEditorAssemblyName));
            Assert.That(registration.Metadata.Kind, Is.EqualTo(UcliOperationKind.Query));
        }

        [Test]
        [Category("Size.Small")]
        public void BuildCatalog_ExcludesCSharpEvaluationFromOperationCatalog ()
        {
            var operations = UcliOperationDiscoverer.Discover(operationServiceProvider);

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(operations);

            Assert.That(
                snapshot.Registrations.Any(static registration => registration.Metadata.OperationName == "ucli.cs.eval"),
                Is.False);
            Assert.That(
                snapshot.Catalog.Operations!.Any(static operation => operation.Name == "ucli.cs.eval"),
                Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void BuildCatalog_WhenOperationExposureIsEditLoweringOnly_ExcludesFromPublicCatalogAndKeepsRegistration ()
        {
            var operation = new DiscoverableOperation();
            var registrations = new[]
            {
                CreateRegistration("ucli.tests.public", UcliOperationExposure.Public, operation),
                CreateRegistration("ucli.tests.edit-lowering-only", UcliOperationExposure.EditLoweringOnly, operation),
            };

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(registrations);

            Assert.That(snapshot.Registrations.Count, Is.EqualTo(2));
            Assert.That(snapshot.Catalog.Operations!.Select(static entry => entry.Name), Is.EquivalentTo(new[] { "ucli.tests.public" }));
            Assert.That(
                snapshot.RequestValidationCatalog.Operations!.Select(static entry => entry.Name),
                Is.EquivalentTo(new[] { "ucli.tests.public", "ucli.tests.edit-lowering-only" }));
            var editOnlyEntry = snapshot.RequestValidationCatalog.Operations!.Single(static entry => entry.Name == "ucli.tests.edit-lowering-only");
            Assert.That(editOnlyEntry.Exposure, Is.EqualTo(UcliOperationExposure.EditLoweringOnly));
        }

        [Test]
        [Category("Size.Small")]
        public void BuildCatalog_WhenBuiltInOperationsAreExported_PublicSchemasDoNotDeclareRequestLocalVar ()
        {
            var operations = UcliOperationDiscoverer.Discover(operationServiceProvider);

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(operations);

            Assert.That(snapshot.Catalog.Operations, Is.Not.Null.And.Not.Empty);
            for (var i = 0; i < snapshot.Catalog.Operations!.Count; i++)
            {
                var argsContract = snapshot.Catalog.Operations[i].ArgsContract;
                Assert.That(argsContract, Is.Not.Null);
                Assert.That(
                    SchemaDeclaresProperty(
                        argsContract!.Value.Schema.ToJsonElement(),
                        UcliOperationContractPropertyNames.Alias),
                    Is.False,
                    $"Public operation schema declared request-local property '{UcliOperationContractPropertyNames.Alias}': {snapshot.Catalog.Operations[i].Name}");
            }
        }

        [Test]
        [Category("Size.Small")]
        public void BuildCatalog_WhenLegacyProjectRefreshOperationWasRemoved_OmitsDiscoveryAndDescribeContracts ()
        {
            var operations = UcliOperationDiscoverer.Discover(operationServiceProvider);

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(operations);

            Assert.That(
                ContainsOperation(operations, RemovedProjectRefreshOperationName),
                Is.False);
            Assert.That(
                snapshot.Catalog.Operations!.Select(static entry => entry.Name),
                Does.Not.Contain(RemovedProjectRefreshOperationName));
            Assert.That(
                snapshot.RequestValidationCatalog.Operations!.Select(static entry => entry.Name),
                Does.Not.Contain(RemovedProjectRefreshOperationName));
        }

        [Test]
        [Category("Size.Small")]
        public void BuildCatalog_WhenMissingScriptsCheckIsExported_UsesTypedArgsResultAndVerdictContracts ()
        {
            var operations = UcliOperationDiscoverer.Discover(operationServiceProvider);
            var metadata = FindMetadata(operations, UcliPrimitiveOperationNames.ProjectMissingScriptsCheck);

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(operations);

            Assert.That(metadata.Kind, Is.EqualTo(UcliOperationKind.Query));
            Assert.That(metadata.Policy, Is.EqualTo(OperationPolicy.Safe));
            Assert.That(metadata.DescribeContract.VerdictContract, Is.Not.Null);
            var catalogEntry = FindCatalogEntry(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.ProjectMissingScriptsCheck);
            Assert.That(catalogEntry.ArgsContract, Is.Not.Null);
            Assert.That(catalogEntry.ResultContract, Is.Not.Null);
            Assert.That(catalogEntry.VerdictContract, Is.Not.Null);
            var argsSchema = GetReferencedRootSchema(catalogEntry.ArgsContract!.Value.Schema.ToJsonElement());
            var argsProperties = argsSchema.GetProperty("properties");
            Assert.That(argsProperties.TryGetProperty("roots", out var rootsSchema), Is.True);
            Assert.That(rootsSchema.GetProperty("minItems").GetInt32(), Is.EqualTo(1));
            Assert.That(argsProperties.TryGetProperty("assetKinds", out var assetKindsSchema), Is.True);
            Assert.That(assetKindsSchema.GetProperty("minItems").GetInt32(), Is.EqualTo(1));
            Assert.That(
                argsSchema.GetProperty("required").EnumerateArray().Select(static item => item.GetString()),
                Is.EquivalentTo(new[] { "roots", "assetKinds" }));
            Assert.That(
                assetKindsSchema.GetProperty("items").GetProperty("enum").EnumerateArray().Select(static item => item.GetString()),
                Is.EquivalentTo(new[] { "scene", "prefab" }));
            var resultProperties = GetReferencedRootSchema(catalogEntry.ResultContract!.Value.Schema.ToJsonElement())
                .GetProperty("properties");
            Assert.That(resultProperties.TryGetProperty("requestedScope", out _), Is.True);
            Assert.That(resultProperties.TryGetProperty("unscannedScopes", out _), Is.True);
            Assert.That(resultProperties.TryGetProperty("scannedAssets", out _), Is.True);
            Assert.That(resultProperties.TryGetProperty("unscannedAssets", out _), Is.True);
            Assert.That(resultProperties.TryGetProperty("missingScriptSlots", out _), Is.True);
            Assert.That(resultProperties.TryGetProperty("errors", out _), Is.False);
            Assert.That(resultProperties.TryGetProperty("diagnostics", out _), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void AddUnityOperationServices_WhenMissingScriptsCheckIsDiscovered_ResolvesItsExplicitAssetAccessDependencies ()
        {
            using var serviceProvider = CreateOperationServiceProvider();

            Assert.That(serviceProvider.GetRequiredService<IMissingScriptsAssetAccess>(), Is.TypeOf<UnityMissingScriptsAssetAccess>());
            Assert.That(serviceProvider.GetRequiredService<IMissingScriptsScanEngine>(), Is.TypeOf<MissingScriptsScanEngine>());
            Assert.That(
                UcliOperationDiscoverer.Discover(serviceProvider).Any(
                    static registration => registration.Metadata.OperationName == UcliPrimitiveOperationNames.ProjectMissingScriptsCheck),
                Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void BuildCatalog_WhenPrefabRevertOverridesIsExported_DescribesSceneTouchAndReadInvalidation ()
        {
            var operations = UcliOperationDiscoverer.Discover(operationServiceProvider);

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(operations);

            Assert.That(
                snapshot.Catalog.Operations!.Any(static entry => entry.Name == UcliPrimitiveOperationNames.PrefabRevertOverrides),
                Is.False);
            var entry = FindCatalogEntry(snapshot.RequestValidationCatalog.Operations!, UcliPrimitiveOperationNames.PrefabRevertOverrides);
            Assert.That(entry.Exposure, Is.EqualTo(UcliOperationExposure.EditLoweringOnly));
            Assert.That(entry.Assurance, Is.Not.Null);
            Assert.That(entry.Assurance!.TouchedKinds, Does.Contain(UcliTouchedResourceKind.Scene));
            Assert.That(entry.Assurance.TouchedContract, Does.Contain("scene resource"));
            Assert.That(entry.Assurance.ReadPostconditionContract, Does.Contain("Scene tree"));
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenUcliDefinedAssembliesAreExcluded_ReturnsNoBuiltInOperations ()
        {
            var operations = UcliOperationDiscoverer.Discover(
                includeUcliDefinedAssemblies: false,
                includeUserDefinedAssemblies: true,
                serviceProvider: operationServiceProvider);

            Assert.That(ContainsOperation(operations, UcliPrimitiveOperationNames.Resolve), Is.False);
            Assert.That(ContainsOperation(operations, AssemblyCSharpEditorFixtureOperationName), Is.True);
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenUserDefinedAssembliesAreExcluded_ReturnsNoUserDefinedOperations ()
        {
            var operations = UcliOperationDiscoverer.Discover(
                includeUcliDefinedAssemblies: true,
                includeUserDefinedAssemblies: false,
                serviceProvider: operationServiceProvider);

            Assert.That(ContainsOperation(operations, UcliPrimitiveOperationNames.Resolve), Is.True);
            Assert.That(ContainsOperation(operations, AssemblyCSharpEditorFixtureOperationName), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenOnlyTestAssemblyIsProvided_ReturnsNoOperations ()
        {
            var operations = UcliOperationDiscoverer.Discover(
                new Assembly[]
                {
                    typeof(UcliOperationDiscovererTests).Assembly,
                },
                includeUcliDefinedAssemblies: true,
                includeUserDefinedAssemblies: true,
                serviceProvider: operationServiceProvider);

            Assert.That(operations, Is.Empty);
        }

        [UcliOperation]
        private sealed class DiscoverableOperation : IUcliOperation
        {
            public UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.CreateWithoutVerdict<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.discover",
                kind: UcliOperationKind.Query,
                description: "ucli.tests.discover test operation.",
                assurance: CreateValidationOnlyAssurance(),
                requiresPreCallPlanReplay: false,
                exposure: UcliOperationExposure.Public,
                playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                codeContract: null);

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }
        }

        [UcliOperation]
        private sealed class RegisteredDependencyOperation : IUcliOperation
        {
            public RegisteredDependencyOperation (RegisteredOperationDependency dependency)
            {
                Dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
            }

            public RegisteredOperationDependency Dependency { get; }

            public UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.CreateWithoutVerdict<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.registered-dependency",
                kind: UcliOperationKind.Query,
                description: "ucli.tests.registered-dependency test operation.",
                assurance: CreateValidationOnlyAssurance(),
                requiresPreCallPlanReplay: false,
                exposure: UcliOperationExposure.Public,
                playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                codeContract: null);

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }
        }

        [UcliOperation]
        private sealed class UnregisteredConcreteDependencyOperation : IUcliOperation
        {
            public UnregisteredConcreteDependencyOperation (UnregisteredOperationDependency dependency)
            {
                _ = dependency ?? throw new ArgumentNullException(nameof(dependency));
            }

            public UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.CreateWithoutVerdict<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.unregistered-concrete-dependency",
                kind: UcliOperationKind.Query,
                description: "ucli.tests.unregistered-concrete-dependency test operation.",
                assurance: CreateValidationOnlyAssurance(),
                requiresPreCallPlanReplay: false,
                exposure: UcliOperationExposure.Public,
                playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                codeContract: null);

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }
        }

        [UcliOperation]
        private sealed class PrivateConstructorOperation : IUcliOperation
        {
            private PrivateConstructorOperation ()
            {
            }

            public UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.CreateWithoutVerdict<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.private-constructor",
                kind: UcliOperationKind.Query,
                description: "ucli.tests.private-constructor test operation.",
                assurance: CreateValidationOnlyAssurance(),
                requiresPreCallPlanReplay: false,
                exposure: UcliOperationExposure.Public,
                playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                codeContract: null);

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success(applied: false, changed: false,touched:Array.Empty<OperationTouch>()));
            }
        }

        private sealed class RegisteredOperationDependency
        {
        }

        private sealed class UnregisteredOperationDependency
        {
        }

        private sealed class UnexpectedMutationLaneControl : IUnityMutationLaneControl
        {
            public bool IsBusy => throw new InvalidOperationException("Operation discovery must not inspect mutation-lane state.");

            public bool HasUnfinishedWork => throw new InvalidOperationException("Operation discovery must not inspect mutation-lane state.");

            public bool IsQuarantined => throw new InvalidOperationException("Operation discovery must not inspect mutation-lane state.");

            public IUnityMutationActivity BeginMutation ()
            {
                throw new InvalidOperationException("Operation discovery must not mutate mutation-lane state.");
            }

            public void Quarantine (string reason, Task mutationCompletion)
            {
                throw new InvalidOperationException("Operation discovery must not mutate mutation-lane state.");
            }

            public bool TrySealAdmissionForRetirement (out IDisposable admissionSeal)
            {
                throw new InvalidOperationException("Operation discovery must not mutate mutation-lane state.");
            }

            public Task WaitForRetirementAsync ()
            {
                throw new InvalidOperationException("Operation discovery must not inspect mutation-lane state.");
            }
        }

        private sealed class SingleServiceProvider : IServiceProvider
        {
            private readonly object? service;

            public SingleServiceProvider (object? service)
            {
                this.service = service;
            }

            public object? GetService (Type serviceType)
            {
                if (service != null && service.GetType() == serviceType)
                {
                    return service;
                }

                return null;
            }
        }

        [UcliOperation]
        private sealed class InvalidAttributedType
        {
        }

        private static UcliOperationMetadata FindMetadata (
            IReadOnlyList<UcliOperationRegistration> operations,
            string operationName)
        {
            return FindRegistration(operations, operationName).Metadata;
        }

        private static UcliOperationRegistration FindRegistration (
            IReadOnlyList<UcliOperationRegistration> operations,
            string operationName)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i].Metadata.OperationName == operationName)
                {
                    return operations[i];
                }
            }

            Assert.Fail($"Operation metadata was not discovered: {operationName}");
            return default;
        }

        private static bool ContainsOperation (
            IReadOnlyList<UcliOperationRegistration> operations,
            string operationName)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i].Metadata.OperationName == operationName)
                {
                    return true;
                }
            }

            return false;
        }

        private static UcliOperationRegistration CreateRegistration (
            string operationName,
            UcliOperationExposure exposure,
            IUcliOperation operation)
        {
            return new UcliOperationRegistration(
                UcliOperationMetadata.CreateWithoutVerdict<UcliEmptyArgs, UcliNoResult>(
                    operationName: operationName,
                    kind: UcliOperationKind.Query,
                    description: $"{operationName} test operation.",
                    assurance: CreateValidationOnlyAssurance(),
                    requiresPreCallPlanReplay: false,
                    exposure: exposure,
                    playModeSupport: UcliOperationPlayModeSupport.Disallowed,
                    codeContract: null),
                operation);
        }

        private static IndexOpEntryJsonContract FindCatalogEntry (
            IReadOnlyList<IndexOpEntryJsonContract> operations,
            string operationName)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i].Name == operationName)
                {
                    return operations[i];
                }
            }

            Assert.Fail($"Catalog operation was not discovered: {operationName}");
            return null!;
        }

        private static JsonElement GetReferencedRootSchema (JsonElement schema)
        {
            var reference = schema.GetProperty("$ref").GetString();
            Assert.That(reference, Does.StartWith("#/$defs/"));
            return schema.GetProperty("$defs").GetProperty(reference!.Substring("#/$defs/".Length));
        }

        private static bool SchemaDeclaresProperty (
            JsonElement schemaNode,
            string propertyName)
        {
            if (schemaNode.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in schemaNode.EnumerateArray())
                {
                    if (SchemaDeclaresProperty(item, propertyName))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (schemaNode.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            foreach (var property in schemaNode.EnumerateObject())
            {
                if (property.NameEquals("properties")
                    && property.Value.ValueKind == JsonValueKind.Object
                    && property.Value.TryGetProperty(propertyName, out _))
                {
                    return true;
                }

                if (SchemaDeclaresProperty(property.Value, propertyName))
                {
                    return true;
                }
            }

            return false;
        }

        private static UcliOperationAssuranceContract CreateValidationOnlyAssurance ()
        {
            return new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ValidationOnly,
                planSemantics: "Validate arguments without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>());
        }

        private static UcliOperationAssuranceContract CreatePreviewStateAssurance ()
        {
            return new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.MayCreatePreviewState,
                planSemantics: "Create request-local preview state before approval.",
                callSemantics: "Apply the requested operation.",
                touchedContract: "Reports no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the operation did not complete.",
                dangerousNotes: new[] { "Preview-state planning is not public raw safe." });
        }

        private static UcliOperationAssuranceContract CreateRuntimeStateMutationAssurance ()
        {
            return new UcliOperationAssuranceContract(
                sideEffects: new[] { UcliOperationSideEffect.RuntimeStateMutation },
                touchedKinds: Array.Empty<UcliTouchedResourceKind>(),
                planMode: UcliOperationPlanMode.ObservesLiveUnity,
                planSemantics: "Validate Play Mode runtime state before mutation.",
                callSemantics: "Apply a Play Mode runtime-state mutation.",
                touchedContract: "Does not report persistent Unity resources.",
                readPostconditionContract: "Persistent read surfaces are unchanged; runtime state may differ.",
                failureSemantics: "Failure before invocation leaves runtime state unchanged.",
                dangerousNotes: new[] { "Changes Play Mode runtime state and is not persisted." });
        }

    }
}
