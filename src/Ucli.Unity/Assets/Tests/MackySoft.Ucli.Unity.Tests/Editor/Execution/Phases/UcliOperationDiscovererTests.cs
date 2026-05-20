using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using NUnit.Framework;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class UcliOperationDiscovererTests
    {
        [Test]
        [Category("Size.Small")]
        public void DiscoverFromTypes_WhenTypeIsValidOperation_ReturnsOperationInstance ()
        {
            var operations = UcliOperationDiscoverer.DiscoverFromTypes(new Type[]
            {
                typeof(DiscoverableOperation),
            });

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
            });

            Assert.That(operations.Count, Is.EqualTo(1));
            Assert.That(operations[0].Operation, Is.TypeOf<GenericDiscoverableOperation>());
            Assert.That(operations[0].Metadata.ArgsType, Is.EqualTo(typeof(GenericDiscoverableArgs)));
            Assert.That(operations[0].Metadata.ResultType, Is.EqualTo(typeof(UcliNoResult)));
            Assert.That(operations[0].Metadata.DescribeContract.Description, Is.EqualTo("Generic operation used to verify custom operation authoring."));
            Assert.That(operations[0].Metadata.DescribeContract.Inputs!.Count, Is.EqualTo(1));
            Assert.That(operations[0].Metadata.DescribeContract.Inputs[0].Name, Is.EqualTo("path"));
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
                });
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
                });
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenEmittedResultTypeNameDoesNotMatch_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = UcliOperationMetadata.Create<GenericDiscoverableArgs, GenericDiscoverableResult>(
                    operationName: "ucli.tests.result-contract-mismatch",
                    kind: UcliOperationKind.Query,
                    describeContract: new UcliOperationDescribeContract(
                        "Result contract mismatch operation.",
                        Array.Empty<UcliOperationInputContract>(),
                        new UcliOperationResultContract(
                            emitted: true,
                            resultType: "DifferentResult",
                            description: "Wrong result contract."),
                    CreateValidationOnlyAssurance()));
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
        public void UcliOperationMetadata_WhenArgsUseRequestLocalAliasType_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = UcliOperationMetadata.Create<ReservedAliasTypeArgs, UcliNoResult>(
                    operationName: "ucli.tests.reserved-alias-type",
                    kind: UcliOperationKind.Query,
                    description: "Reserved alias type operation.",
                    assurance: CreateValidationOnlyAssurance());
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenDescribeVariantFieldUsesRequestLocalAliasArgsPath_ThrowsArgumentException ()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = UcliOperationMetadata.Create<GenericDiscoverableArgs, UcliNoResult>(
                    operationName: "ucli.tests.describe-alias-path",
                    kind: UcliOperationKind.Query,
                    describeContract: new UcliOperationDescribeContract(
                        "Describe alias path operation.",
                        new[]
                        {
                            new UcliOperationInputContract(
                                "target",
                                "object",
                                "Target reference.",
                                Array.Empty<UcliOperationInputConstraintContract>(),
                                variants: new[]
                                {
                                    new UcliOperationInputVariantContract(
                                        "byAlias",
                                        "Use request-local alias.",
                                        new[]
                                        {
                                            new UcliOperationInputVariantFieldContract(
                                                "var",
                                                "$.target.var",
                                                "Request-local alias.",
                                                Array.Empty<UcliOperationInputConstraintContract>()),
                                        }),
                                }),
                        },
                        UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
                        CreateValidationOnlyAssurance()));
            });
        }

        [Test]
        [Category("Size.Small")]
        public void UcliOperationMetadata_WhenDescribeContractIsMutatedAfterCreation_DoesNotExposeMutation ()
        {
            var fieldConstraint = new UcliOperationInputConstraintContract(UcliOperationInputConstraintKindValues.GlobalObjectId);
            var variantField = new UcliOperationInputVariantFieldContract(
                "globalObjectId",
                "$.target.globalObjectId",
                "Resolved Unity GlobalObjectId.",
                new[] { fieldConstraint });
            var input = new UcliOperationInputContract(
                "target",
                "object",
                "Target reference.",
                Array.Empty<UcliOperationInputConstraintContract>(),
                variants: new[]
                {
                    new UcliOperationInputVariantContract(
                        "byGlobalObjectId",
                        "Use an exact Unity GlobalObjectId.",
                        new[] { variantField }),
                });
            var describeContract = new UcliOperationDescribeContract(
                "Defensive copy operation.",
                new[] { input },
                UcliOperationResultContract.NoResult("This operation does not emit operation-specific result data."),
                CreateValidationOnlyAssurance());

            var metadata = UcliOperationMetadata.Create<GenericDiscoverableArgs, UcliNoResult>(
                operationName: "ucli.tests.describe-defensive-copy",
                kind: UcliOperationKind.Query,
                describeContract: describeContract);

            input.ArgsPath = "$.target.var";
            variantField.ArgsPath = "$.target.var";
            fieldConstraint.Kind = UcliOperationInputConstraintKindValues.HierarchyPath;
            metadata.DescribeContract.Inputs![0].ArgsPath = "$.target.var";
            metadata.DescribeContract.Inputs![0].Variants![0].Fields![0].ArgsPath = "$.target.var";
            metadata.DescribeContract.Inputs![0].Variants![0].Fields![0].Constraints![0].Kind = UcliOperationInputConstraintKindValues.HierarchyPath;

            Assert.That(metadata.DescribeContract.Inputs![0].ArgsPath, Is.Null);
            Assert.That(metadata.DescribeContract.Inputs![0].Variants![0].Fields![0].ArgsPath, Is.EqualTo("$.target.globalObjectId"));
            Assert.That(metadata.DescribeContract.Inputs![0].Variants![0].Fields![0].Constraints![0].Kind, Is.EqualTo(UcliOperationInputConstraintKindValues.GlobalObjectId));
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
                });
            });
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenCurrentDomainContainsInvalidAttributedTestType_IgnoresTestAssembly ()
        {
            var operations = UcliOperationDiscoverer.Discover();

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
        public void Discover_WhenBuiltInOperationsAreRead_ReturnsConcreteArgsSchemas ()
        {
            var operations = UcliOperationDiscoverer.Discover();

            var resolveMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.Resolve);
            using var resolveSchemaDocument = JsonDocument.Parse(resolveMetadata.ArgsSchemaJson);
            var resolveProperties = resolveSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(
                resolveProperties.TryGetProperty("globalObjectId", out _),
                Is.True);
            Assert.That(resolveProperties.TryGetProperty("projectAssetPath", out _), Is.True);
            Assert.That(resolveProperties.TryGetProperty("prefab", out _), Is.True);
            Assert.That(resolveProperties.TryGetProperty("componentType", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(resolveSchemaDocument.RootElement);

            var compEnsureMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.CompEnsure);
            using var compEnsureSchemaDocument = JsonDocument.Parse(compEnsureMetadata.ArgsSchemaJson);
            var compEnsureTargetProperties = compEnsureSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(compEnsureTargetProperties.TryGetProperty("prefab", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(compEnsureSchemaDocument.RootElement);

            var compSetMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.CompSet);
            using var compSetSchemaDocument = JsonDocument.Parse(compSetMetadata.ArgsSchemaJson);
            var compSetProperties = compSetSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(compSetProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(compSetProperties.GetProperty("sets").GetProperty("type").GetString(), Is.EqualTo("array"));
            var compSetTargetProperties = compSetProperties.GetProperty("target").GetProperty("properties");
            Assert.That(compSetTargetProperties.TryGetProperty("scene", out _), Is.True);
            Assert.That(compSetTargetProperties.TryGetProperty("prefab", out _), Is.True);
            Assert.That(compSetTargetProperties.TryGetProperty("componentType", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(compSetSchemaDocument.RootElement);

            var prefabCreateMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.PrefabCreate);
            using var prefabCreateSchemaDocument = JsonDocument.Parse(prefabCreateMetadata.ArgsSchemaJson);
            var prefabCreateProperties = prefabCreateSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(prefabCreateProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(prefabCreateProperties.TryGetProperty("path", out _), Is.True);
            Assert.That(prefabCreateSchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(2));

            var prefabOpenMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.PrefabOpen);
            Assert.That(prefabOpenMetadata.Kind, Is.EqualTo(UcliOperationKind.Command));
            using var prefabOpenSchemaDocument = JsonDocument.Parse(prefabOpenMetadata.ArgsSchemaJson);
            var prefabOpenProperties = prefabOpenSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(prefabOpenProperties.TryGetProperty("path", out _), Is.True);
            Assert.That(prefabOpenSchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(1));

            var goCreateMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.GoCreate);
            using var goCreateSchemaDocument = JsonDocument.Parse(goCreateMetadata.ArgsSchemaJson);
            var goCreateParentProperties = goCreateSchemaDocument.RootElement.GetProperty("properties").GetProperty("parent").GetProperty("properties");
            Assert.That(goCreateParentProperties.TryGetProperty("prefab", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(goCreateSchemaDocument.RootElement);

            var goDeleteMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.GoDelete);
            using var goDeleteSchemaDocument = JsonDocument.Parse(goDeleteMetadata.ArgsSchemaJson);
            var goDeleteTargetProperties = goDeleteSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(goDeleteTargetProperties.TryGetProperty("componentType", out _), Is.False);

            var goReparentMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.GoReparent);
            using var goReparentSchemaDocument = JsonDocument.Parse(goReparentMetadata.ArgsSchemaJson);
            var goReparentProperties = goReparentSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(goReparentProperties.GetProperty("target").GetProperty("properties").TryGetProperty("componentType", out _), Is.False);
            Assert.That(goReparentProperties.GetProperty("parent").GetProperty("properties").TryGetProperty("componentType", out _), Is.False);

            var sceneQueryMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.SceneQuery);
            using var sceneQuerySchemaDocument = JsonDocument.Parse(sceneQueryMetadata.ArgsSchemaJson);
            Assert.That(sceneQuerySchemaDocument.RootElement.GetProperty("additionalProperties").GetBoolean(), Is.False);
            var sceneQueryProperties = sceneQuerySchemaDocument.RootElement.GetProperty("properties");
            Assert.That(sceneQueryProperties.TryGetProperty("scene", out _), Is.True);
            Assert.That(sceneQueryProperties.TryGetProperty("pathPrefix", out _), Is.True);
            Assert.That(sceneQueryProperties.TryGetProperty("componentType", out _), Is.True);
            Assert.That(sceneQueryProperties.EnumerateObject().Count(), Is.EqualTo(3));
            Assert.That(sceneQuerySchemaDocument.RootElement.GetProperty("required").GetArrayLength(), Is.EqualTo(1));
            Assert.That(sceneQuerySchemaDocument.RootElement.GetProperty("required")[0].GetString(), Is.EqualTo("scene"));

            var assetCreateMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.AssetCreate);
            using var assetCreateSchemaDocument = JsonDocument.Parse(assetCreateMetadata.ArgsSchemaJson);
            var assetCreateProperties = assetCreateSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(assetCreateProperties.TryGetProperty("type", out _), Is.True);
            Assert.That(assetCreateProperties.TryGetProperty("path", out _), Is.True);

            var assetSetMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.AssetSet);
            using var assetSetSchemaDocument = JsonDocument.Parse(assetSetMetadata.ArgsSchemaJson);
            var assetSetProperties = assetSetSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(assetSetProperties.TryGetProperty("target", out _), Is.True);
            Assert.That(assetSetProperties.GetProperty("sets").GetProperty("type").GetString(), Is.EqualTo("array"));
            var assetSetTargetProperties = assetSetProperties.GetProperty("target").GetProperty("properties");
            Assert.That(assetSetTargetProperties.TryGetProperty("projectAssetPath", out _), Is.True);

            var assetSchemaMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.AssetSchema);
            using var assetSchemaDocument = JsonDocument.Parse(assetSchemaMetadata.ArgsSchemaJson);
            var assetSchemaProperties = assetSchemaDocument.RootElement.GetProperty("properties");
            Assert.That(assetSchemaProperties.TryGetProperty("type", out _), Is.True);
            Assert.That(assetSchemaProperties.TryGetProperty("target", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(assetSchemaDocument.RootElement);

            var goDescribeMetadata = FindMetadata(operations, UcliPrimitiveOperationNames.GoDescribe);
            using var goDescribeSchemaDocument = JsonDocument.Parse(goDescribeMetadata.ArgsSchemaJson);
            var goDescribeTargetProperties = goDescribeSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(goDescribeTargetProperties.TryGetProperty("prefab", out _), Is.True);
            AssertContainsNoUnsupportedSchemaKeyword(goDescribeSchemaDocument.RootElement);
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenAssetsFindOperationIsRead_ReturnsConcreteArgsSchema ()
        {
            var operations = UcliOperationDiscoverer.Discover();

            var metadata = FindMetadata(operations, UcliPrimitiveOperationNames.AssetsFind);
            using var schemaDocument = JsonDocument.Parse(metadata.ArgsSchemaJson);
            var root = schemaDocument.RootElement;
            Assert.That(root.GetProperty("additionalProperties").GetBoolean(), Is.False);
            AssertContainsNoUnsupportedSchemaKeyword(root);
            var properties = root.GetProperty("properties");
            Assert.That(properties.TryGetProperty("type", out var typeProperty), Is.True);
            Assert.That(typeProperty.GetProperty("type").GetString(), Is.EqualTo("string"));
            Assert.That(properties.TryGetProperty("pathPrefix", out var pathPrefixProperty), Is.True);
            Assert.That(pathPrefixProperty.GetProperty("type").GetString(), Is.EqualTo("string"));
            Assert.That(properties.TryGetProperty("nameContains", out var nameContainsProperty), Is.True);
            Assert.That(nameContainsProperty.GetProperty("type").GetString(), Is.EqualTo("string"));
            Assert.That(properties.TryGetProperty("limit", out var limitProperty), Is.True);
            Assert.That(limitProperty.GetProperty("type")[0].GetString(), Is.EqualTo("integer"));
            Assert.That(properties.TryGetProperty("cursor", out var cursorProperty), Is.True);
            Assert.That(cursorProperty.GetProperty("type").GetString(), Is.EqualTo("string"));
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenAssetsFindOperationIsRead_ReturnsWindowResultSchema ()
        {
            var operations = UcliOperationDiscoverer.Discover();

            var metadata = FindMetadata(operations, UcliPrimitiveOperationNames.AssetsFind);
            using var schemaDocument = JsonDocument.Parse(metadata.ResultSchemaJson!);
            var windowProperties = schemaDocument.RootElement
                .GetProperty("properties")
                .GetProperty("window")
                .GetProperty("properties");

            Assert.That(windowProperties.TryGetProperty("limit", out _), Is.True);
            Assert.That(windowProperties.GetProperty("cursor").GetProperty("type")[0].GetString(), Is.EqualTo("string"));
            Assert.That(windowProperties.GetProperty("cursor").GetProperty("type")[1].GetString(), Is.EqualTo("null"));
            Assert.That(windowProperties.GetProperty("nextCursor").GetProperty("type")[0].GetString(), Is.EqualTo("string"));
            Assert.That(windowProperties.GetProperty("nextCursor").GetProperty("type")[1].GetString(), Is.EqualTo("null"));
            Assert.That(windowProperties.TryGetProperty("isComplete", out _), Is.True);
            Assert.That(windowProperties.TryGetProperty("totalCount", out _), Is.True);
            Assert.That(windowProperties.TryGetProperty("after", out _), Is.False);
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenSceneTreeOperationIsRead_ReturnsWindowArgsSchema ()
        {
            var operations = UcliOperationDiscoverer.Discover();

            var metadata = FindMetadata(operations, UcliPrimitiveOperationNames.SceneTree);
            using var schemaDocument = JsonDocument.Parse(metadata.ArgsSchemaJson);
            var root = schemaDocument.RootElement;
            Assert.That(root.GetProperty("additionalProperties").GetBoolean(), Is.False);
            AssertContainsNoUnsupportedSchemaKeyword(root);
            var properties = root.GetProperty("properties");
            Assert.That(properties.TryGetProperty("path", out var pathProperty), Is.True);
            Assert.That(pathProperty.GetProperty("type").GetString(), Is.EqualTo("string"));
            Assert.That(properties.TryGetProperty("depth", out var depthProperty), Is.True);
            Assert.That(depthProperty.GetProperty("type")[0].GetString(), Is.EqualTo("integer"));
            Assert.That(properties.TryGetProperty("limit", out var limitProperty), Is.True);
            Assert.That(limitProperty.GetProperty("type")[0].GetString(), Is.EqualTo("integer"));
            Assert.That(properties.TryGetProperty("cursor", out var cursorProperty), Is.True);
            Assert.That(cursorProperty.GetProperty("type").GetString(), Is.EqualTo("string"));
        }

        [Test]
        [Category("Size.Small")]
        public void BuildCatalog_WhenCsEvalOperationIsDiscovered_ExcludesFromPublicCatalogAndKeepsRegistration ()
        {
            var operations = UcliOperationDiscoverer.Discover();
            var metadata = FindMetadata(operations, UcliPrimitiveOperationNames.CsEval);

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(operations);

            Assert.That(snapshot.Registrations, Has.Some.Matches<UcliOperationRegistration>(
                registration => registration.Metadata.OperationName == UcliPrimitiveOperationNames.CsEval));
            Assert.That(
                snapshot.Catalog.Operations!.Any(operation => operation.Name == UcliPrimitiveOperationNames.CsEval),
                Is.False);
            Assert.That(metadata.Exposure, Is.EqualTo(UcliOperationExposure.Internal));
            Assert.That(metadata.Kind, Is.EqualTo(UcliOperationKind.Mutation));
            Assert.That(metadata.Policy, Is.EqualTo(OperationPolicy.Dangerous));
            Assert.That(metadata.ArgsSchemaJson, Does.Contain("\"source\""));
            Assert.That(metadata.ResultSchemaJson, Does.Contain("\"sourceKind\""));
            var describeContract = metadata.DescribeContract;
            Assert.That(describeContract.CodeContract, Is.Not.Null);
            Assert.That(describeContract.CodeContract!.Language, Is.EqualTo("csharp"));
            Assert.That(describeContract.CodeContract.EntryPoint!.MatchRule, Does.Contain("exactly one"));
            Assert.That(describeContract.CodeContract.SourceForms!.Count, Is.EqualTo(2));
            Assert.That(describeContract.CodeContract.SourceForms![0].Kind, Is.EqualTo(CsEvalSourceKindValues.CompilationUnit));
            Assert.That(describeContract.CodeContract.SourceForms[1].Kind, Is.EqualTo(CsEvalSourceKindValues.Snippet));
            Assert.That(describeContract.CodeContract.ApiTypes!.Count, Is.EqualTo(1));
            Assert.That(describeContract.Assurance, Is.Not.Null);
            Assert.That(describeContract.Assurance!.PlanSemantics, Does.Contain("without invoking user code"));
            Assert.That(describeContract.Assurance.CallSemantics, Does.Contain("execute the user C# entry point"));
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
        public void BuildCatalog_WhenOperationExposureIsNotPublic_ExcludesFromPublicCatalogAndKeepsRegistration ()
        {
            var operation = new DiscoverableOperation();
            var registrations = new[]
            {
                CreateRegistration("ucli.tests.public", UcliOperationExposure.Public, operation),
                CreateRegistration("ucli.tests.edit-lowering-only", UcliOperationExposure.EditLoweringOnly, operation),
                CreateRegistration("ucli.tests.internal", UcliOperationExposure.Internal, operation),
            };

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(registrations);

            Assert.That(snapshot.Registrations.Count, Is.EqualTo(3));
            Assert.That(snapshot.Catalog.Operations!.Select(static entry => entry.Name), Is.EquivalentTo(new[] { "ucli.tests.public" }));
            Assert.That(
                snapshot.RequestValidationCatalog.Operations!.Select(static entry => entry.Name),
                Is.EquivalentTo(new[] { "ucli.tests.public", "ucli.tests.edit-lowering-only" }));
            var editOnlyEntry = snapshot.RequestValidationCatalog.Operations!.Single(static entry => entry.Name == "ucli.tests.edit-lowering-only");
            Assert.That(editOnlyEntry.Exposure, Is.EqualTo(UcliOperationExposureValues.EditLoweringOnly));
        }

        [Test]
        [Category("Size.Small")]
        public void BuildCatalog_WhenBuiltInOperationsAreExported_RemovesVarSelectorsFromPublicSchemas ()
        {
            var operations = UcliOperationDiscoverer.Discover();

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(operations);

            var sceneOpenEntry = FindCatalogEntry(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.SceneOpen);
            Assert.That(sceneOpenEntry.Kind, Is.EqualTo(UcliOperationKindValues.Command));
            Assert.That(sceneOpenEntry.Policy, Is.EqualTo(OperationPolicyValues.Advanced));
            Assert.That(sceneOpenEntry.Description, Is.Not.Null.And.Not.Empty);
            Assert.That(sceneOpenEntry.ResultContract, Is.Not.Null);
            Assert.That(sceneOpenEntry.ResultContract!.Emitted, Is.False);
            Assert.That(sceneOpenEntry.Assurance, Is.Not.Null);
            Assert.That(sceneOpenEntry.Assurance!.SideEffects, Does.Contain(UcliOperationSideEffectValues.EditorStateChange));
            Assert.That(sceneOpenEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.OpensSceneInEditor));
            Assert.That(sceneOpenEntry.Assurance!.PlanMode, Is.EqualTo(UcliOperationPlanModeValues.MayCreatePreviewState));
            Assert.That(sceneOpenEntry.Assurance.PlanSemantics, Does.Contain("scene path"));
            Assert.That(sceneOpenEntry.Assurance.CallSemantics, Does.Contain("Open the requested scene"));
            Assert.That(sceneOpenEntry.Assurance.TouchedContract, Does.Contain("observed editor context"));
            Assert.That(sceneOpenEntry.Assurance.DangerousNotes, Is.Not.Empty);

            var projectRefreshEntry = FindCatalogEntry(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.ProjectRefresh);
            Assert.That(projectRefreshEntry.Kind, Is.EqualTo(UcliOperationKindValues.Command));
            Assert.That(projectRefreshEntry.Assurance, Is.Not.Null);
            Assert.That(projectRefreshEntry.Assurance!.SideEffects, Does.Contain(UcliOperationSideEffectValues.AssetDatabaseRefresh));
            Assert.That(projectRefreshEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.AssetImport));
            Assert.That(projectRefreshEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.ScriptCompilation));
            Assert.That(projectRefreshEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.DomainReload));
            Assert.That(projectRefreshEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.AssetContentMutation));
            Assert.That(projectRefreshEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.AssetSave));
            Assert.That(projectRefreshEntry.Assurance.MayDirty, Is.True);
            Assert.That(projectRefreshEntry.Assurance.MayPersist, Is.True);
            Assert.That(projectRefreshEntry.Assurance.ReadPostconditionContract, Does.Contain("readIndex"));
            Assert.That(projectRefreshEntry.Assurance.DangerousNotes, Is.Not.Empty);

            var prefabCreateEntry = FindCatalogEntry(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.PrefabCreate);
            Assert.That(prefabCreateEntry.Assurance, Is.Not.Null);
            Assert.That(prefabCreateEntry.Assurance!.SideEffects, Does.Contain(UcliOperationSideEffectValues.PrefabContentMutation));
            Assert.That(prefabCreateEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.SceneContentMutation));
            Assert.That(prefabCreateEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.PrefabSave));
            Assert.That(prefabCreateEntry.Assurance.MayDirty, Is.True);
            Assert.That(prefabCreateEntry.Assurance.MayPersist, Is.True);
            Assert.That(prefabCreateEntry.Assurance.TouchedKinds, Does.Contain(IpcExecuteTouchedResourceKindNames.Scene));
            Assert.That(prefabCreateEntry.Assurance.TouchedKinds, Does.Contain(IpcExecuteTouchedResourceKindNames.Prefab));

            var assetSetEntry = FindCatalogEntry(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.AssetSet);
            Assert.That(assetSetEntry.Assurance, Is.Not.Null);
            Assert.That(assetSetEntry.Assurance!.SideEffects, Does.Contain(UcliOperationSideEffectValues.AssetContentMutation));
            Assert.That(assetSetEntry.Assurance.SideEffects, Does.Contain(UcliOperationSideEffectValues.ProjectSettingsMutation));
            Assert.That(assetSetEntry.Assurance.PlanSemantics, Does.Contain("preview"));
            Assert.That(assetSetEntry.Assurance.CallSemantics, Does.Contain("live asset"));
            Assert.That(assetSetEntry.Assurance.DangerousNotes, Is.Not.Empty);

            var goCreateSchemaJson = FindCatalogSchema(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.GoCreate);
            using var goCreateSchemaDocument = JsonDocument.Parse(goCreateSchemaJson);
            var goCreateParentProperties = goCreateSchemaDocument.RootElement.GetProperty("properties").GetProperty("parent").GetProperty("properties");
            Assert.That(goCreateParentProperties.TryGetProperty("var", out _), Is.False);
            AssertContainsNoUnsupportedSchemaKeyword(goCreateSchemaDocument.RootElement);

            var goDescribeSchemaJson = FindCatalogSchema(snapshot.Catalog.Operations!, UcliPrimitiveOperationNames.GoDescribe);
            using var goDescribeSchemaDocument = JsonDocument.Parse(goDescribeSchemaJson);
            var goDescribeTargetProperties = goDescribeSchemaDocument.RootElement.GetProperty("properties").GetProperty("target").GetProperty("properties");
            Assert.That(goDescribeTargetProperties.TryGetProperty("var", out _), Is.False);
            AssertContainsNoUnsupportedSchemaKeyword(goDescribeSchemaDocument.RootElement);

            for (var i = 0; i < snapshot.Catalog.Operations!.Count; i++)
            {
                using var schemaDocument = JsonDocument.Parse(snapshot.Catalog.Operations[i].ArgsSchemaJson!);
                AssertContainsNoVarBranch(schemaDocument.RootElement);
                AssertContainsNoVarVariantField(snapshot.Catalog.Operations[i].Inputs);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void BuildCatalog_WhenPrefabRevertOverridesIsExported_DescribesSceneTouchAndReadInvalidation ()
        {
            var operations = UcliOperationDiscoverer.Discover();

            var snapshot = UcliOperationCatalogSnapshotBuilder.Build(operations);

            Assert.That(
                snapshot.Catalog.Operations!.Any(static entry => entry.Name == UcliPrimitiveOperationNames.PrefabRevertOverrides),
                Is.False);
            var entry = FindCatalogEntry(snapshot.RequestValidationCatalog.Operations!, UcliPrimitiveOperationNames.PrefabRevertOverrides);
            Assert.That(entry.Exposure, Is.EqualTo(UcliOperationExposureValues.EditLoweringOnly));
            Assert.That(entry.Assurance, Is.Not.Null);
            Assert.That(entry.Assurance!.TouchedKinds, Does.Contain(IpcExecuteTouchedResourceKindNames.Scene));
            Assert.That(entry.Assurance.TouchedContract, Does.Contain("scene resource"));
            Assert.That(entry.Assurance.ReadPostconditionContract, Does.Contain("Scene tree"));
        }

        [Test]
        [Category("Size.Small")]
        public void Discover_WhenUcliDefinedAssembliesAreExcluded_ReturnsNoBuiltInOperations ()
        {
            var operations = UcliOperationDiscoverer.Discover(
                new Assembly[]
                {
                    typeof(ResolveOperation).Assembly,
                },
                includeUcliDefinedAssemblies: false,
                includeUserDefinedAssemblies: true);

            Assert.That(operations, Is.Empty);
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
                includeUserDefinedAssemblies: true);

            Assert.That(operations, Is.Empty);
        }

        [UcliOperation]
        private sealed class DiscoverableOperation : IUcliOperation
        {
            public UcliOperationMetadata Metadata { get; } = new UcliOperationMetadata(
                operationName: "ucli.tests.discover",
                kind: UcliOperationKind.Query,
                describeContract: CreateDescribeContract("ucli.tests.discover"));

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

        [UcliDescription("Generic discoverable operation args.")]
        private sealed class GenericDiscoverableArgs
        {
            [UcliRequired]
            [UcliDescription("Scene asset path to inspect.")]
            public SceneAssetPath? Path { get; set; }
        }

        [UcliDescription("Generic discoverable operation result.")]
        private sealed class GenericDiscoverableResult
        {
        }

        [UcliDescription("Reserved var args.")]
        private sealed class ReservedVarArgs
        {
            [UcliDescription("Reserved property name.")]
            [JsonPropertyName(UcliOperationContractPropertyNames.Alias)]
            public string? Alias { get; set; }
        }

        [UcliDescription("Reserved alias type args.")]
        private sealed class ReservedAliasTypeArgs
        {
            [UcliDescription("Reserved alias type.")]
            public UcliPlanAlias? Alias { get; set; }
        }

        [UcliOperation]
        private sealed class InvalidAttributedType
        {
        }

        private static UcliOperationMetadata FindMetadata (
            IReadOnlyList<UcliOperationRegistration> operations,
            string operationName)
        {
            for (var i = 0; i < operations.Count; i++)
            {
                if (operations[i].Metadata.OperationName == operationName)
                {
                    return operations[i].Metadata;
                }
            }

            Assert.Fail($"Operation metadata was not discovered: {operationName}");
            return null!;
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
                    describeContract: CreateDescribeContract(operationName),
                    exposure: exposure),
                operation);
        }

        private static string FindCatalogSchema (
            IReadOnlyList<IndexOpEntryJsonContract> operations,
            string operationName)
        {
            return FindCatalogEntry(operations, operationName).ArgsSchemaJson!;
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

        private static void AssertContainsNoVarBranch (JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        Assert.That(property.Name, Is.Not.EqualTo("var"));
                        AssertContainsNoVarBranch(property.Value);
                    }

                    return;

                case JsonValueKind.Array:
                    foreach (var item in element.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                        {
                            Assert.That(item.GetString(), Is.Not.EqualTo("var"));
                        }
                        else
                        {
                            AssertContainsNoVarBranch(item);
                        }
                    }

                    return;

                default:
                    return;
            }
        }

        private static void AssertContainsNoVarVariantField (IReadOnlyList<UcliOperationInputContract>? inputs)
        {
            if (inputs == null)
            {
                return;
            }

            for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
            {
                var variants = inputs[inputIndex].Variants;
                if (variants == null)
                {
                    continue;
                }

                for (var variantIndex = 0; variantIndex < variants.Count; variantIndex++)
                {
                    var fields = variants[variantIndex].Fields;
                    if (fields == null)
                    {
                        continue;
                    }

                    for (var fieldIndex = 0; fieldIndex < fields.Count; fieldIndex++)
                    {
                        Assert.That(fields[fieldIndex].Name, Is.Not.EqualTo("var"));
                        Assert.That(fields[fieldIndex].ArgsPath, Does.Not.EndWith(".var"));
                    }
                }
            }
        }

        private static void AssertContainsNoUnsupportedSchemaKeyword (JsonElement element)
        {
            AssertContainsOnlySupportedSchemaKeywords(element);
        }

        private static void AssertContainsOnlySupportedSchemaKeywords (JsonElement schemaNode)
        {
            Assert.That(schemaNode.ValueKind, Is.EqualTo(JsonValueKind.Object));
            foreach (var property in schemaNode.EnumerateObject())
            {
                Assert.That(IsSupportedSchemaKeyword(property.Name), Is.True, $"Unsupported schema keyword: {property.Name}");
                switch (property.Name)
                {
                    case "properties":
                    case "$defs":
                        AssertSchemaMapUsesSupportedKeywords(property.Value);
                        break;

                    case "items":
                        AssertContainsOnlySupportedSchemaKeywords(property.Value);
                        break;
                }
            }
        }

        private static void AssertSchemaMapUsesSupportedKeywords (JsonElement mapElement)
        {
            Assert.That(mapElement.ValueKind, Is.EqualTo(JsonValueKind.Object));
            foreach (var entry in mapElement.EnumerateObject())
            {
                AssertContainsOnlySupportedSchemaKeywords(entry.Value);
            }
        }

        private static bool IsSupportedSchemaKeyword (string keyword)
        {
            switch (keyword)
            {
                case "type":
                case "properties":
                case "required":
                case "additionalProperties":
                case "items":
                case "$ref":
                case "$defs":
                    return true;

                default:
                    return false;
            }
        }

        private static UcliOperationAssuranceContract CreateValidationOnlyAssurance ()
        {
            return new UcliOperationAssuranceContract(
                sideEffects: Array.Empty<UcliOperationSideEffect>(),
                touchedKinds: Array.Empty<string>(),
                planMode: UcliOperationPlanMode.ValidationOnly,
                planSemantics: "Validate arguments without applying mutation.",
                callSemantics: "Read Unity state without applying mutation.",
                touchedContract: "Returns no touched resources.",
                readPostconditionContract: "Does not stale read surfaces by itself.",
                failureSemantics: "Failure means the observation was not fully produced.",
                dangerousNotes: Array.Empty<string>());
        }

        private static UcliOperationDescribeContract CreateDescribeContract (string operationName)
        {
            return new UcliOperationDescribeContract(
                $"{operationName} test operation.",
                Array.Empty<UcliOperationInputContract>(),
                UcliOperationResultContract.NoResult("This test operation does not emit operation-specific result data."),
                CreateValidationOnlyAssurance());
        }
    }
}
