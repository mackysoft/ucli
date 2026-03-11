using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class AssetOperationTests
    {
        [Test]
        [Category("Size.Small")]
        public async Task Create_Plan_WhenAliasIsSpecified_StoresTemporaryAssetAlias ()
        {
            var createOperation = new AssetCreateOperation();
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: "ucli.asset.create",
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = CreateTemporaryAssetPath(),
                },
                alias: "created");
            var context = new OperationExecutionContext();

            var result = await createOperation.Plan(requestOperation, context, CancellationToken.None);

            AssertAssetSuccess(result, applied: false, changed: true, requestOperation.Args.GetProperty("path").GetString()!);
            Assert.That(context.TryGetTemporaryAliasState("created", out var aliasState), Is.True);
            Assert.That(aliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Asset));
            Assert.That(aliasState.Resource.Path, Is.EqualTo(requestOperation.Args.GetProperty("path").GetString()));
            Assert.That(aliasState.UnityObject, Is.TypeOf<AssetOperationTestAsset>());
        }

        [Test]
        [Category("Size.Small")]
        public async Task Create_Call_WhenArgumentsAreValid_CreatesAssetAndStoresAlias ()
        {
            var operation = new AssetCreateOperation();
            var assetPath = CreateTemporaryAssetPath();
            try
            {
                var requestOperation = CreateOperation(
                    opId: "op-create",
                    opName: "ucli.asset.create",
                    args: new
                    {
                        type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                        path = assetPath,
                    },
                    alias: "created");
                var context = new OperationExecutionContext();

                var result = await operation.Call(requestOperation, context, CancellationToken.None);

                AssertAssetSuccess(result, applied: true, changed: true, assetPath);
                Assert.That(AssetDatabase.LoadAssetAtPath<AssetOperationTestAsset>(assetPath), Is.Not.Null);
                Assert.That(context.AliasStore.TryGet("created", out var resolvedReference), Is.True);
                Assert.That(resolvedReference, Is.Not.Null);
            }
            finally
            {
                DeleteAsset(assetPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Create_Validate_WhenTypeIsNotScriptableObject_ReturnsInvalidArgument ()
        {
            var operation = new AssetCreateOperation();
            var requestOperation = CreateOperation(
                opId: "op-create",
                opName: "ucli.asset.create",
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(Material)),
                    path = CreateTemporaryAssetPath(),
                });

            var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-create");
        }

        [Test]
        [Category("Size.Small")]
        public async Task Set_Plan_WhenTargetUsesCreatedAlias_UpdatesTemporaryAssetState ()
        {
            var createOperation = new AssetCreateOperation();
            var setOperation = new AssetSetOperation();
            var schemaOperation = new AssetSchemaOperation();
            var assetPath = CreateTemporaryAssetPath();
            var context = new OperationExecutionContext();
            var createRequest = CreateOperation(
                opId: "op-create",
                opName: "ucli.asset.create",
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                },
                alias: "created");
            var setRequest = CreateOperation(
                opId: "op-set",
                opName: "ucli.asset.set",
                args: new
                {
                    target = new
                    {
                        @var = "created",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 42,
                        },
                        new
                        {
                            path = "text",
                            value = "after",
                        },
                    },
                });
            var schemaRequest = CreateOperation(
                opId: "op-schema",
                opName: "ucli.asset.schema",
                args: new
                {
                    target = new
                    {
                        @var = "created",
                    },
                });

            var createResult = await createOperation.Plan(createRequest, context, CancellationToken.None);
            var setResult = await setOperation.Plan(setRequest, context, CancellationToken.None);
            var schemaResult = await schemaOperation.Plan(schemaRequest, context, CancellationToken.None);

            AssertAssetSuccess(createResult, applied: false, changed: true, assetPath);
            AssertAssetSuccess(setResult, applied: false, changed: true, assetPath);
            AssertQuerySuccess(schemaResult, applied: false);
            Assert.That(context.TryGetTemporaryAliasState("created", out var aliasState), Is.True);
            Assert.That(aliasState.Resource.Kind, Is.EqualTo(OperationTouchKind.Asset));
            Assert.That(aliasState.Resource.Path, Is.EqualTo(assetPath));
            Assert.That(aliasState.UnityObject, Is.TypeOf<AssetOperationTestAsset>());
            var temporaryAsset = (AssetOperationTestAsset)aliasState.UnityObject!;
            Assert.That(temporaryAsset.IntegerValue, Is.EqualTo(42));
            Assert.That(temporaryAsset.Text, Is.EqualTo("after"));

            var schema = schemaResult.Result!.Value;
            Assert.That(schema.GetProperty("schemaKey").GetString(), Does.StartWith("asset:"));
            Assert.That(schema.GetProperty("typeId").GetString(), Is.EqualTo(IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset))));
            Assert.That(schema.GetProperty("properties").ToString(), Does.Contain("\"path\":\"integerValue\""));
            Assert.That(schema.GetProperty("properties").ToString(), Does.Contain("\"path\":\"text\""));
        }

        [Test]
        [Category("Size.Small")]
        public async Task Set_Call_WhenTargetIsScriptableObjectAsset_UpdatesValueAndLeavesAssetDirty ()
        {
            var operation = new AssetSetOperation();
            var assetPath = CreateTemporaryAssetPath();
            var asset = ScriptableObject.CreateInstance<AssetOperationTestAsset>();
            try
            {
                AssetDatabase.CreateAsset(asset, assetPath);
                var requestOperation = CreateOperation(
                    opId: "op-set",
                    opName: "ucli.asset.set",
                    args: new
                    {
                        target = new
                        {
                            assetPath,
                        },
                        sets = new object[]
                        {
                            new
                            {
                                path = "integerValue",
                                value = 64,
                            },
                            new
                            {
                                path = "text",
                                value = "updated",
                            },
                        },
                    });

                var result = await operation.Call(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertAssetSuccess(result, applied: true, changed: true, assetPath);
                Assert.That(asset.IntegerValue, Is.EqualTo(64));
                Assert.That(asset.Text, Is.EqualTo("updated"));
                Assert.That(EditorUtility.IsDirty(asset), Is.True);
            }
            finally
            {
                if (asset != null)
                {
                    ScriptableObject.DestroyImmediate(asset, allowDestroyingAssets: true);
                }

                DeleteAsset(assetPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Set_Call_WhenTargetIsMaterialAsset_UpdatesMaterialSerializedValue ()
        {
            var operation = new AssetSetOperation();
            var assetPath = CreateTemporaryMaterialPath();
            var material = new Material(ResolveMaterialShader());
            try
            {
                AssetDatabase.CreateAsset(material, assetPath);
                var requestOperation = CreateOperation(
                    opId: "op-set",
                    opName: "ucli.asset.set",
                    args: new
                    {
                        target = new
                        {
                            assetPath,
                        },
                        sets = new object[]
                        {
                            new
                            {
                                path = "m_CustomRenderQueue",
                                value = 2450,
                            },
                        },
                    });

                var result = await operation.Call(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertAssetSuccess(result, applied: true, changed: true, assetPath);
                Assert.That(material.renderQueue, Is.EqualTo(2450));
                Assert.That(EditorUtility.IsDirty(material), Is.True);
            }
            finally
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material, allowDestroyingAssets: true);
                }

                DeleteAsset(assetPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Set_Validate_WhenTargetIsSubAsset_ReturnsInvalidArgument ()
        {
            var operation = new AssetSetOperation();
            var assetPath = CreateTemporaryAssetPath();
            var mainAsset = ScriptableObject.CreateInstance<AssetOperationTestAsset>();
            var subAsset = ScriptableObject.CreateInstance<AssetOperationTestAsset>();
            try
            {
                AssetDatabase.CreateAsset(mainAsset, assetPath);
                AssetDatabase.AddObjectToAsset(subAsset, assetPath);
                var context = new OperationExecutionContext();
                context.AliasStore.Set("target", UnityObjectReferenceResolver.CreateResolvedReference(subAsset));
                var requestOperation = CreateOperation(
                    opId: "op-set",
                    opName: "ucli.asset.set",
                    args: new
                    {
                        target = new
                        {
                            @var = "target",
                        },
                        sets = new object[]
                        {
                            new
                            {
                                path = "integerValue",
                                value = 10,
                            },
                        },
                    });

                var result = await operation.Validate(requestOperation, context, CancellationToken.None);

                AssertInvalidArgument(result, "op-set");
            }
            finally
            {
                if (subAsset != null)
                {
                    ScriptableObject.DestroyImmediate(subAsset, allowDestroyingAssets: true);
                }

                if (mainAsset != null)
                {
                    ScriptableObject.DestroyImmediate(mainAsset, allowDestroyingAssets: true);
                }

                DeleteAsset(assetPath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public async Task Schema_Plan_WhenTypeUsesMaterial_ReturnsInvalidArgument ()
        {
            var operation = new AssetSchemaOperation();
            var requestOperation = CreateOperation(
                opId: "op-schema",
                opName: "ucli.asset.schema",
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(Material)),
                });

            var result = await operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None);

            AssertInvalidArgument(result, "op-schema");
        }

        [Test]
        [Category("Size.Small")]
        public async Task Schema_Plan_WhenTargetIsMaterial_ReturnsSchemaMetadata ()
        {
            var operation = new AssetSchemaOperation();
            var assetPath = CreateTemporaryMaterialPath();
            var material = new Material(ResolveMaterialShader());
            try
            {
                AssetDatabase.CreateAsset(material, assetPath);
                var requestOperation = CreateOperation(
                    opId: "op-schema",
                    opName: "ucli.asset.schema",
                    args: new
                    {
                        target = new
                        {
                            assetPath,
                        },
                    });

                var result = await operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None);

                AssertQuerySuccess(result, applied: false);
                var schema = result.Result!.Value;
                Assert.That(schema.GetProperty("kind").GetString(), Is.EqualTo("asset"));
                Assert.That(schema.GetProperty("typeId").GetString(), Is.EqualTo(IndexTypeIdFormatter.Format(typeof(Material))));
            }
            finally
            {
                if (material != null)
                {
                    UnityEngine.Object.DestroyImmediate(material, allowDestroyingAssets: true);
                }

                DeleteAsset(assetPath);
            }
        }

        private static Shader ResolveMaterialShader ()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            if (shader == null)
            {
                throw new InvalidOperationException("Material test shader could not be resolved.");
            }

            return shader;
        }

        private static string CreateTemporaryAssetPath ()
        {
            return $"Assets/AssetOperationTests_{Guid.NewGuid():N}.asset";
        }

        private static string CreateTemporaryMaterialPath ()
        {
            return $"Assets/AssetOperationTests_{Guid.NewGuid():N}.mat";
        }

        private static NormalizedOperation CreateOperation (
            string opId,
            string opName,
            object args,
            string? alias = null)
        {
            return new NormalizedOperation(
                Id: opId,
                Op: opName,
                Args: JsonSerializer.SerializeToElement(args),
                As: alias,
                Expect: null);
        }

        private static void AssertInvalidArgument (
            OperationPhaseStepResult result,
            string expectedOperationId)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure!.Code, Is.EqualTo(IpcErrorCodes.InvalidArgument));
            Assert.That(result.Failure.OpId, Is.EqualTo(expectedOperationId));
        }

        private static void AssertAssetSuccess (
            OperationPhaseStepResult result,
            bool applied,
            bool changed,
            string assetPath)
        {
            Assert.That(result.IsSuccess, Is.True, result.Failure?.Message);
            Assert.That(result.Applied, Is.EqualTo(applied));
            Assert.That(result.Changed, Is.EqualTo(changed));
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            Assert.That(result.Touched[0].Kind, Is.EqualTo(OperationTouchKind.Asset));
            Assert.That(result.Touched[0].Path, Is.EqualTo(assetPath));
            Assert.That(result.Failure, Is.Null);
        }

        private static void AssertQuerySuccess (
            OperationPhaseStepResult result,
            bool applied)
        {
            Assert.That(result.IsSuccess, Is.True, result.Failure?.Message);
            Assert.That(result.Applied, Is.EqualTo(applied));
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Touched, Is.Empty);
            Assert.That(result.Result.HasValue, Is.True);
            Assert.That(result.Failure, Is.Null);
        }

        private static void DeleteAsset (string assetPath)
        {
            var previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                {
                    _ = AssetDatabase.DeleteAsset(assetPath);
                }

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = previousIgnoreFailingMessages;
            }
        }
    }
}
