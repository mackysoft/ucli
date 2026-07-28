using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
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

        private readonly ServiceProvider operationServiceProvider = CreateOperationServiceProvider();

        internal static ServiceProvider CreateOperationServiceProvider ()
        {
            return new ServiceCollection()
                .AddSingleton<IUnityMutationLaneControl>(new UnexpectedMutationLaneControl())
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
        public void DiscoverFromTypes_WhenTypeIsGenericOperation_ReturnsTypedDescribeContract ()
        {
            var operations = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
            {
                typeof(GenericDiscoverableOperation),
            }, operationServiceProvider);

            Assert.That(operations.Count, Is.EqualTo(1));
            Assert.That(operations[0].Operation, Is.TypeOf<GenericDiscoverableOperation>());
            Assert.That(operations[0].Metadata.ArgsType, Is.EqualTo(typeof(GenericDiscoverableArgs)));
            Assert.That(operations[0].Metadata.ResultType, Is.EqualTo(typeof(UcliNoResult)));
            Assert.That(operations[0].Metadata.DescribeContract.Description, Is.EqualTo("Generic operation used to verify custom operation authoring."));
            Assert.That(operations[0].Metadata.DescribeContract.ArgsContract, Is.Not.Null);
            Assert.That(operations[0].Metadata.DescribeContract.ResultContract, Is.Null);
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
        public void DiscoverFromTypes_WhenTypedOperationMetadataArgsTypeDoesNotMatch_ThrowsInvalidOperationException ()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
                {
                    typeof(MetadataArgsMismatchOperation),
                }, operationServiceProvider);
            });
        }

        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenTypedOperationMetadataResultTypeDoesNotMatch_ThrowsInvalidOperationException ()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                _ = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
                {
                    typeof(MetadataResultMismatchOperation),
                }, operationServiceProvider);
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenArgsUseReservedRawOpPropertyName_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = UcliOperationMetadata.Create<ReservedVarArgs, UcliNoResult>(
                    operationName: "ucli.tests.reserved-var",
                    kind: UcliOperationKind.Query,
                    description: "Reserved var property operation.",
                    assurance: CreateValidationOnlyAssurance());
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenArgsRootIsNotObject_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = UcliOperationMetadata.Create<string, UcliNoResult>(
                    operationName: "ucli.tests.scalar-args",
                    kind: UcliOperationKind.Query,
                    description: "Invalid scalar args operation.",
                    assurance: CreateValidationOnlyAssurance());
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenResultRootIsNotObject_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = UcliOperationMetadata.Create<UcliEmptyArgs, string>(
                    operationName: "ucli.tests.scalar-result",
                    kind: UcliOperationKind.Query,
                    description: "Invalid scalar result operation.",
                    assurance: CreateValidationOnlyAssurance());
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenPublicOperationMayCreatePreviewState_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                    operationName: "ucli.tests.public-preview-state",
                    kind: UcliOperationKind.Command,
                    description: "Public preview-state operation.",
                    assurance: CreatePreviewStateAssurance());
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenEditLoweringOnlyOperationMayCreatePreviewState_ReturnsMetadata ()
        {
            var editLoweringOnlyMetadata = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.edit-preview-state",
                kind: UcliOperationKind.Command,
                description: "Edit-only preview-state operation.",
                assurance: CreatePreviewStateAssurance(),
                exposure: UcliOperationExposure.EditLoweringOnly);

            Assert.That(editLoweringOnlyMetadata.Exposure, Is.EqualTo(UcliOperationExposure.EditLoweringOnly));
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenPlayModeSupportIsOmitted_DefaultsToDisallowed ()
        {
            var metadata = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.playmode-default",
                kind: UcliOperationKind.Command,
                description: "Default Play Mode support operation.",
                assurance: CreateValidationOnlyAssurance());

            Assert.That(metadata.PlayModeSupport, Is.EqualTo(UcliOperationPlayModeSupport.Disallowed));
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenPlayModeSupportIsSpecified_StoresValue ()
        {
            var metadata = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.playmode-required",
                kind: UcliOperationKind.Mutation,
                description: "Play Mode required operation.",
                assurance: CreateRuntimeStateMutationAssurance(),
                playModeSupport: UcliOperationPlayModeSupport.Required);

            Assert.That(metadata.PlayModeSupport, Is.EqualTo(UcliOperationPlayModeSupport.Required));
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenDescribeContractIsMutatedAfterCreation_DoesNotExposeMutation ()
        {
            var metadata = UcliOperationMetadata.Create<GenericDiscoverableArgs, UcliNoResult>(
                operationName: "ucli.tests.describe-defensive-copy",
                kind: UcliOperationKind.Query,
                description: "Defensive copy operation.",
                assurance: CreateValidationOnlyAssurance());
            var firstRead = metadata.DescribeContract;
            firstRead.Description = "Mutated description.";

            var secondRead = metadata.DescribeContract;

            Assert.That(secondRead.Description, Is.EqualTo("Defensive copy operation."));
            Assert.That(secondRead.ArgsContract, Is.Not.Null);
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
        public void BuildCatalog_WhenCsEvalOperationIsDiscovered_IncludesPublicDangerousOperation ()
        {
            var operations = UcliOperationDiscoverer.Discover(operationServiceProvider);
            var metadata = FindMetadata(operations, UcliPrimitiveOperationNames.CsEval);

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(operations);

            Assert.That(snapshot.Registrations, Has.Some.Matches<UcliOperationRegistration>(
                registration => registration.Metadata.OperationName == UcliPrimitiveOperationNames.CsEval));
            Assert.That(
                snapshot.Catalog.Operations!.Any(operation => operation.Name == UcliPrimitiveOperationNames.CsEval),
                Is.True);
            Assert.That(metadata.Exposure, Is.EqualTo(UcliOperationExposure.Public));
            Assert.That(metadata.PlayModeSupport, Is.EqualTo(UcliOperationPlayModeSupport.Allowed));
            Assert.That(metadata.Kind, Is.EqualTo(UcliOperationKind.Mutation));
            Assert.That(metadata.Policy, Is.EqualTo(OperationPolicy.Dangerous));
            var describeContract = metadata.DescribeContract;
            Assert.That(describeContract.ArgsContract, Is.Not.Null);
            Assert.That(describeContract.ResultContract, Is.Not.Null);
            var catalogEntry = FindCatalogEntry(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.CsEval);
            Assert.That(catalogEntry.PlayModeSupport, Is.EqualTo(UcliOperationPlayModeSupport.Allowed));
            Assert.That(describeContract.CodeContract, Is.Not.Null);
            Assert.That(describeContract.CodeContract!.Language, Is.EqualTo(UcliCodeLanguage.CSharp));
            Assert.That(describeContract.CodeContract.EntryPoint!.MatchRule, Does.Contain("exactly one"));
            Assert.That(describeContract.CodeContract.SourceForms!.Count, Is.EqualTo(2));
            Assert.That(describeContract.CodeContract.SourceForms![0].Kind, Is.EqualTo(UcliCodeSourceFormKind.CompilationUnit));
            Assert.That(describeContract.CodeContract.SourceForms[1].Kind, Is.EqualTo(UcliCodeSourceFormKind.Snippet));
            Assert.That(describeContract.CodeContract.ApiTypes!.Count, Is.EqualTo(1));
            Assert.That(describeContract.Assurance, Is.Not.Null);
            Assert.That(describeContract.Assurance!.PlanSemantics, Does.Contain("without invoking user code"));
            Assert.That(describeContract.Assurance.CallSemantics, Does.Contain("execute, and await the user C# entry point"));
            Assert.That(describeContract.Assurance.TouchedContract, Does.Contain("caller-controlled"));
            Assert.That(describeContract.Assurance.FailureSemantics, Does.Contain("cannot be forcibly stopped"));
            Assert.That(describeContract.Assurance.DangerousNotes!.Count, Is.EqualTo(2));
            var apiType = describeContract.CodeContract.ApiTypes[0];
            Assert.That(apiType.Members!.Count, Is.EqualTo(8));
            Assert.That(apiType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == "DeclareNoTouchedResources"));
            Assert.That(apiType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == "DeclareTouchedAsset"));
            Assert.That(apiType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == "DeclareTouchedPrefab"));
            Assert.That(apiType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == "DeclareTouchedProjectSettings"));
            Assert.That(apiType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == "DeclareTouchedScene"));
            Assert.That(apiType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == "Log"));
            Assert.That(apiType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == "LogError"));
            Assert.That(apiType.Members, Has.Some.Matches<UcliCodeApiMemberContract>(member => member.Name == "LogWarning"));
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
            public UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.discover",
                kind: UcliOperationKind.Query,
                description: "ucli.tests.discover test operation.",
                assurance: CreateValidationOnlyAssurance());

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        [UcliOperation]
        private sealed class GenericDiscoverableOperation : UcliOperation<GenericDiscoverableArgs, UcliNoResult>
        {
            public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<GenericDiscoverableArgs, UcliNoResult>(
                operationName: "ucli.tests.generic-discover",
                kind: UcliOperationKind.Query,
                description: "Generic operation used to verify custom operation authoring.",
                assurance: CreateValidationOnlyAssurance());

            protected override Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
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

            public UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.registered-dependency",
                kind: UcliOperationKind.Query,
                description: "ucli.tests.registered-dependency test operation.",
                assurance: CreateValidationOnlyAssurance());

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        [UcliOperation]
        private sealed class UnregisteredConcreteDependencyOperation : IUcliOperation
        {
            public UnregisteredConcreteDependencyOperation (UnregisteredOperationDependency dependency)
            {
                _ = dependency ?? throw new ArgumentNullException(nameof(dependency));
            }

            public UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.unregistered-concrete-dependency",
                kind: UcliOperationKind.Query,
                description: "ucli.tests.unregistered-concrete-dependency test operation.",
                assurance: CreateValidationOnlyAssurance());

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        [UcliOperation]
        private sealed class PrivateConstructorOperation : IUcliOperation
        {
            private PrivateConstructorOperation ()
            {
            }

            public UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.private-constructor",
                kind: UcliOperationKind.Query,
                description: "ucli.tests.private-constructor test operation.",
                assurance: CreateValidationOnlyAssurance());

            public Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            public Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken = default)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
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
        private sealed class MetadataArgsMismatchOperation : UcliOperation<GenericDiscoverableArgs, UcliNoResult>
        {
            public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                operationName: "ucli.tests.args-mismatch",
                kind: UcliOperationKind.Query,
                description: "Metadata args mismatch operation.",
                assurance: CreateValidationOnlyAssurance());

            protected override Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        [UcliOperation]
        private sealed class MetadataResultMismatchOperation : UcliOperation<GenericDiscoverableArgs, GenericDiscoverableResult>
        {
            public override UcliOperationMetadata Metadata { get; } = UcliOperationMetadata.Create<GenericDiscoverableArgs, UcliNoResult>(
                operationName: "ucli.tests.result-mismatch",
                kind: UcliOperationKind.Query,
                description: "Metadata result mismatch operation.",
                assurance: CreateValidationOnlyAssurance());

            protected override Task<OperationPhaseStepResult> ValidateAsync (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> PlanAsync (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }

            protected override Task<OperationPhaseStepResult> CallAsync (
                NormalizedOperation operation,
                GenericDiscoverableArgs args,
                OperationExecutionContext executionContext,
                CancellationToken cancellationToken)
            {
                return Task.FromResult(OperationPhaseStepResult.Success());
            }
        }

        private sealed class GenericDiscoverableArgs
        {
            [JsonRequired]
            public SceneAssetPath? Path { get; set; }
        }

        private sealed class GenericDiscoverableResult
        {
        }

        private sealed class ReservedVarArgs
        {
            [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
            public string? Alias { get; set; }
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
                UcliOperationMetadata.Create<UcliEmptyArgs, UcliNoResult>(
                    operationName: operationName,
                    kind: UcliOperationKind.Query,
                    description: $"{operationName} test operation.",
                    assurance: CreateValidationOnlyAssurance(),
                    exposure: exposure),
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
