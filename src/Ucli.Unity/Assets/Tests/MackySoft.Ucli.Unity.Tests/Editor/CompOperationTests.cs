using System;
using System.Text.Json;
using System.Threading;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class CompOperationTests
    {
        [Test]
        [Category("Size.Small")]
        public void Ensure_Plan_WhenComponentMissing_ReturnsChangedTrueAndStoresTemporaryAlias ()
        {
            var operation = new CompEnsureOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _ = new GameObject("Root");
                EditorSceneManager.SaveScene(scene, scenePath);
                var requestOperation = CreateOperation(
                    opId: "op-ensure",
                    opName: "ucli.comp.ensure",
                    args: new
                    {
                        target = new
                        {
                            scene = scenePath,
                            hierarchyPath = "Root",
                        },
                        type = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                    },
                    alias: "ensured");
                var context = new OperationExecutionContext();

                var result = operation.Plan(requestOperation, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: false, changed: true, scenePath);
                Assert.That(context.TryGetTemporaryAlias("ensured", out var temporaryObject, out var temporaryScenePath), Is.True);
                Assert.That(temporaryScenePath, Is.EqualTo(scenePath));
                Assert.That(temporaryObject, Is.TypeOf<CompOperationTestComponent>());
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Ensure_Plan_WhenSameEnsureWasAlreadyPlanned_UsesPlannedEnsureState ()
        {
            var operation = new CompEnsureOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _ = new GameObject("Root");
                EditorSceneManager.SaveScene(scene, scenePath);
                var firstRequest = CreateOperation(
                    opId: "op-ensure-1",
                    opName: "ucli.comp.ensure",
                    args: new
                    {
                        target = new
                        {
                            scene = scenePath,
                            hierarchyPath = "Root",
                        },
                        type = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                    });
                var secondRequest = CreateOperation(
                    opId: "op-ensure-2",
                    opName: "ucli.comp.ensure",
                    args: new
                    {
                        target = new
                        {
                            scene = scenePath,
                            hierarchyPath = "Root",
                        },
                        type = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                    },
                    alias: "ensured");
                var context = new OperationExecutionContext();

                var firstResult = operation.Plan(firstRequest, context, CancellationToken.None).GetAwaiter().GetResult();
                var secondResult = operation.Plan(secondRequest, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(firstResult, applied: false, changed: true, scenePath);
                AssertSuccess(secondResult, applied: false, changed: false, scenePath);
                Assert.That(context.TryGetTemporaryAlias("ensured", out var temporaryObject, out var temporaryScenePath), Is.True);
                Assert.That(temporaryScenePath, Is.EqualTo(scenePath));
                Assert.That(temporaryObject, Is.TypeOf<CompOperationTestComponent>());
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Ensure_Call_WhenMultipleComponentsExist_ReusesFirstExistingComponent ()
        {
            var operation = new CompEnsureOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                var first = root.AddComponent<CompOperationTestComponent>();
                _ = root.AddComponent<CompOperationTestComponent>();
                EditorSceneManager.SaveScene(scene, scenePath);
                var requestOperation = CreateOperation(
                    opId: "op-ensure",
                    opName: "ucli.comp.ensure",
                    args: new
                    {
                        target = new
                        {
                            scene = scenePath,
                            hierarchyPath = "Root",
                        },
                        type = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                    },
                    alias: "ensured");
                var context = new OperationExecutionContext();

                var result = operation.Call(requestOperation, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: true, changed: false, scenePath);
                Assert.That(context.AliasStore.TryGet("ensured", out var resolvedReference), Is.True);
                Assert.That(resolvedReference!.GlobalObjectId, Is.EqualTo(UnityObjectReferenceResolver.CreateResolvedReference(first).GlobalObjectId));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Ensure_Validate_WhenTypeIsNotComponent_ReturnsInvalidArgument ()
        {
            var operation = new CompEnsureOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                EditorSceneManager.SaveScene(scene, scenePath);
                var requestOperation = CreateOperation(
                    opId: "op-ensure",
                    opName: "ucli.comp.ensure",
                    args: new
                    {
                        target = new
                        {
                            scene = scenePath,
                            hierarchyPath = "Root",
                        },
                        type = IndexTypeIdFormatter.Format(typeof(GameObject)),
                    });

                var result = operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

                AssertInvalidArgument(result, "op-ensure");
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Set_Call_WhenAssignmentsAreValid_AppliesRepresentativeValues ()
        {
            var operation = new CompSetOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                var target = root.AddComponent<CompOperationTestComponent>();
                var other = new GameObject("Other");
                EditorSceneManager.SaveScene(scene, scenePath);
                var context = new OperationExecutionContext();
                context.AliasStore.Set("target", UnityObjectReferenceResolver.CreateResolvedReference(target));
                context.AliasStore.Set("other", UnityObjectReferenceResolver.CreateResolvedReference(other));
                var managedTypeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent.ManagedValue));
                const string hashText = "0123456789abcdef0123456789abcdef";
                var requestOperation = CreateOperation(
                    opId: "op-set",
                    opName: "ucli.comp.set",
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
                                value = 42,
                            },
                            new
                            {
                                path = "floatValue",
                                value = 3.5,
                            },
                            new
                            {
                                path = "text",
                                value = "updated",
                            },
                            new
                            {
                                path = "enumValue",
                                value = "Second",
                            },
                            new
                            {
                                path = "objectReferenceValue",
                                value = new
                                {
                                    @var = "other",
                                },
                            },
                            new
                            {
                                path = "nestedValue",
                                value = new
                                {
                                    number = 9,
                                    label = "nested",
                                },
                            },
                            new
                            {
                                path = "nestedList",
                                value = new object[]
                                {
                                    new
                                    {
                                        number = 1,
                                        label = "a",
                                    },
                                    new
                                    {
                                        number = 2,
                                        label = "b",
                                    },
                                },
                            },
                            new
                            {
                                path = "nestedList.Array.data[0].number",
                                value = 10,
                            },
                            new
                            {
                                path = "managedReferenceValue",
                                value = new
                                {
                                    type = managedTypeId,
                                    value = new
                                    {
                                        amount = 7,
                                        note = "managed",
                                    },
                                },
                            },
                            new
                            {
                                path = "curveValue",
                                value = new
                                {
                                    keys = new object[]
                                    {
                                        new
                                        {
                                            time = 0f,
                                            value = 0f,
                                        },
                                        new
                                        {
                                            time = 1f,
                                            value = 2f,
                                        },
                                    },
                                    preWrapMode = "Loop",
                                    postWrapMode = "PingPong",
                                },
                            },
                            new
                            {
                                path = "gradientValue",
                                value = new
                                {
                                    colorKeys = new object[]
                                    {
                                        new
                                        {
                                            color = new
                                            {
                                                r = 1f,
                                                g = 0f,
                                                b = 0f,
                                                a = 1f,
                                            },
                                            time = 0f,
                                        },
                                        new
                                        {
                                            color = new
                                            {
                                                r = 0f,
                                                g = 0f,
                                                b = 1f,
                                                a = 1f,
                                            },
                                            time = 1f,
                                        },
                                    },
                                    alphaKeys = new object[]
                                    {
                                        new
                                        {
                                            alpha = 1f,
                                            time = 0f,
                                        },
                                        new
                                        {
                                            alpha = 0.5f,
                                            time = 1f,
                                        },
                                    },
                                    mode = "Blend",
                                },
                            },
                            new
                            {
                                path = "boundsValue",
                                value = new
                                {
                                    center = new
                                    {
                                        x = 1f,
                                        y = 2f,
                                        z = 3f,
                                    },
                                    size = new
                                    {
                                        x = 4f,
                                        y = 5f,
                                        z = 6f,
                                    },
                                },
                            },
                            new
                            {
                                path = "hashValue",
                                value = hashText,
                            },
                        },
                    });

                var result = operation.Call(requestOperation, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: true, changed: true, scenePath);
                Assert.That(target.IntegerValue, Is.EqualTo(42));
                Assert.That(target.FloatValue, Is.EqualTo(3.5f));
                Assert.That(target.Text, Is.EqualTo("updated"));
                Assert.That(target.EnumValue, Is.EqualTo(CompOperationTestComponent.SampleMode.Second));
                Assert.That(target.ObjectReferenceValue, Is.EqualTo(other));
                Assert.That(target.NestedValueValue.Number, Is.EqualTo(9));
                Assert.That(target.NestedValueValue.Label, Is.EqualTo("nested"));
                Assert.That(target.NestedList.Count, Is.EqualTo(2));
                Assert.That(target.NestedList[0].Number, Is.EqualTo(10));
                Assert.That(target.NestedList[0].Label, Is.EqualTo("a"));
                Assert.That(target.NestedList[1].Number, Is.EqualTo(2));
                Assert.That(target.NestedList[1].Label, Is.EqualTo("b"));
                Assert.That(target.ManagedReferenceValue, Is.TypeOf<CompOperationTestComponent.ManagedValue>());
                var managedValue = (CompOperationTestComponent.ManagedValue)target.ManagedReferenceValue!;
                Assert.That(managedValue.Amount, Is.EqualTo(7));
                Assert.That(managedValue.Note, Is.EqualTo("managed"));
                Assert.That(target.CurveValue.preWrapMode, Is.EqualTo(WrapMode.Loop));
                Assert.That(target.CurveValue.postWrapMode, Is.EqualTo(WrapMode.PingPong));
                Assert.That(target.CurveValue.keys.Length, Is.EqualTo(2));
                Assert.That(target.GradientValue.mode, Is.EqualTo(GradientMode.Blend));
                Assert.That(target.GradientValue.colorKeys.Length, Is.EqualTo(2));
                Assert.That(target.GradientValue.alphaKeys[1].alpha, Is.EqualTo(0.5f));
                Assert.That(target.BoundsValue.center, Is.EqualTo(new Vector3(1f, 2f, 3f)));
                Assert.That(target.BoundsValue.size, Is.EqualTo(new Vector3(4f, 5f, 6f)));
                var expectedHash = Hash128.Parse(hashText);
                Assert.That(target.HashValue, Is.EqualTo(expectedHash));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Set_ValueApplier_WhenApplyingToSceneComponent_UpdatesBackingField ()
        {
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                var target = root.AddComponent<CompOperationTestComponent>();
                EditorSceneManager.SaveScene(scene, scenePath);
                var assignmentsJson = JsonSerializer.SerializeToElement(new object[]
                {
                    new
                    {
                        path = "integerValue",
                        value = 42,
                    },
                });
                Assert.That(CompSetArgumentsCodec.TryParse(
                    JsonSerializer.SerializeToElement(new
                    {
                        target = new
                        {
                            scene = scenePath,
                            hierarchyPath = "Root",
                        },
                        sets = assignmentsJson,
                    }),
                    out var parsedArguments,
                    out var parseErrorMessage), Is.True, parseErrorMessage);

                var result = CompSetValueApplier.TryApply(
                    target,
                    parsedArguments.Sets,
                    new OperationExecutionContext(),
                    allowTemporaryState: false,
                    out var changed,
                    out var errorMessage);

                Assert.That(result, Is.True, errorMessage);
                Assert.That(changed, Is.True);
                Assert.That(target.IntegerValue, Is.EqualTo(42));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void ResolveComponent_WhenAliasTargetsSceneComponent_ReturnsLiveComponent ()
        {
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                var target = root.AddComponent<CompOperationTestComponent>();
                EditorSceneManager.SaveScene(scene, scenePath);
                var context = new OperationExecutionContext();
                context.AliasStore.Set("target", UnityObjectReferenceResolver.CreateResolvedReference(target));

                var result = ComponentOperationUtilities.TryResolveComponent(
                    UnityObjectReference.FromAlias("target"),
                    context,
                    allowTemporaryState: false,
                    out var resolvedComponent,
                    out var resolvedScenePath,
                    out var errorMessage);

                Assert.That(result, Is.True, errorMessage);
                Assert.That(resolvedScenePath, Is.EqualTo(scenePath));
                Assert.That(resolvedComponent, Is.EqualTo(target));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Set_Validate_WhenModeIsSpecified_ReturnsInvalidArgument ()
        {
            var operation = new CompSetOperation();
            var requestOperation = CreateOperation(
                opId: "op-set",
                opName: "ucli.comp.set",
                args: new
                {
                    mode = "atomic",
                    target = new
                    {
                        @var = "target",
                    },
                    sets = new object[]
                    {
                        new
                        {
                            path = "integerValue",
                            value = 1,
                        },
                    },
                });

            var result = operation.Validate(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

            AssertInvalidArgument(result, "op-set");
        }

        [Test]
        [Category("Size.Small")]
        public void Set_Call_WhenPathIsReadOnly_ReturnsInvalidArgumentAndPreservesValue ()
        {
            var operation = new CompSetOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                var target = root.AddComponent<CompOperationTestComponent>();
                EditorSceneManager.SaveScene(scene, scenePath);
                var context = new OperationExecutionContext();
                context.AliasStore.Set("target", UnityObjectReferenceResolver.CreateResolvedReference(target));
                var requestOperation = CreateOperation(
                    opId: "op-set",
                    opName: "ucli.comp.set",
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
                                path = "m_Script",
                                value = (string?)null,
                            },
                        },
                    });

                var result = operation.Call(requestOperation, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertInvalidArgument(result, "op-set");
                Assert.That(target.IntegerValue, Is.EqualTo(1));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Set_Plan_WhenValueIsUnchanged_ReturnsChangedFalse ()
        {
            var operation = new CompSetOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                var target = root.AddComponent<CompOperationTestComponent>();
                EditorSceneManager.SaveScene(scene, scenePath);
                var context = new OperationExecutionContext();
                context.AliasStore.Set("target", UnityObjectReferenceResolver.CreateResolvedReference(target));
                var requestOperation = CreateOperation(
                    opId: "op-set",
                    opName: "ucli.comp.set",
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
                                value = 1,
                            },
                        },
                    });

                var result = operation.Plan(requestOperation, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(result, applied: false, changed: false, scenePath);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Set_Plan_WhenEnsureCreatesAlias_MutatesTemporaryComponent ()
        {
            var ensureOperation = new CompEnsureOperation();
            var setOperation = new CompSetOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                _ = new GameObject("Root");
                EditorSceneManager.SaveScene(scene, scenePath);
                var context = new OperationExecutionContext();
                var ensureRequest = CreateOperation(
                    opId: "op-ensure",
                    opName: "ucli.comp.ensure",
                    args: new
                    {
                        target = new
                        {
                            scene = scenePath,
                            hierarchyPath = "Root",
                        },
                        type = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent)),
                    },
                    alias: "ensured");
                var setRequest = CreateOperation(
                    opId: "op-set",
                    opName: "ucli.comp.set",
                    args: new
                    {
                        target = new
                        {
                            @var = "ensured",
                        },
                        sets = new object[]
                        {
                            new
                            {
                                path = "integerValue",
                                value = 99,
                            },
                        },
                    });

                var ensureResult = ensureOperation.Plan(ensureRequest, context, CancellationToken.None).GetAwaiter().GetResult();
                var setResult = setOperation.Plan(setRequest, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(ensureResult, applied: false, changed: true, scenePath);
                AssertSuccess(setResult, applied: false, changed: true, scenePath);
                Assert.That(context.TryGetTemporaryAlias("ensured", out var temporaryObject, out _), Is.True);
                Assert.That(((CompOperationTestComponent)temporaryObject!).IntegerValue, Is.EqualTo(99));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Set_Plan_WhenAliasStateIsReusedBeforeGlobalObjectId_KeepsAliasStateSynchronized ()
        {
            var operation = new CompSetOperation();
            var scenePath = CreateTemporaryScenePath();
            try
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject("Root");
                var target = root.AddComponent<CompOperationTestComponent>();
                EditorSceneManager.SaveScene(scene, scenePath);
                var context = new OperationExecutionContext();
                context.AliasStore.Set("target", UnityObjectReferenceResolver.CreateResolvedReference(target));
                var globalObjectId = UnityObjectReferenceResolver.CreateResolvedReference(target).GlobalObjectId;
                var firstRequest = CreateOperation(
                    opId: "op-set-alias-1",
                    opName: "ucli.comp.set",
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
                                path = "nestedList",
                                value = new object[]
                                {
                                    new
                                    {
                                        number = 10,
                                        label = "first",
                                    },
                                },
                            },
                        },
                    });
                var secondRequest = CreateOperation(
                    opId: "op-set-alias-2",
                    opName: "ucli.comp.set",
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
                                value = 5,
                            },
                        },
                    });
                var thirdRequest = CreateOperation(
                    opId: "op-set-global",
                    opName: "ucli.comp.set",
                    args: new
                    {
                        target = new
                        {
                            globalObjectId,
                        },
                        sets = new object[]
                        {
                            new
                            {
                                path = "nestedList",
                                value = new object[]
                                {
                                    new
                                    {
                                        number = 10,
                                        label = "first",
                                    },
                                    new
                                    {
                                        number = 20,
                                        label = "second",
                                    },
                                },
                            },
                        },
                    });
                var fourthRequest = CreateOperation(
                    opId: "op-set-alias-3",
                    opName: "ucli.comp.set",
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
                                path = "nestedList.Array.data[1].number",
                                value = 30,
                            },
                        },
                    });

                var firstResult = operation.Plan(firstRequest, context, CancellationToken.None).GetAwaiter().GetResult();
                var secondResult = operation.Plan(secondRequest, context, CancellationToken.None).GetAwaiter().GetResult();
                var thirdResult = operation.Plan(thirdRequest, context, CancellationToken.None).GetAwaiter().GetResult();
                var fourthResult = operation.Plan(fourthRequest, context, CancellationToken.None).GetAwaiter().GetResult();

                AssertSuccess(firstResult, applied: false, changed: true, scenePath);
                AssertSuccess(secondResult, applied: false, changed: true, scenePath);
                AssertSuccess(thirdResult, applied: false, changed: true, scenePath);
                AssertSuccess(fourthResult, applied: false, changed: true, scenePath);
                Assert.That(context.TryGetTemporaryAlias("target", out var temporaryObject, out _), Is.True);
                var temporaryComponent = (CompOperationTestComponent)temporaryObject!;
                Assert.That(temporaryComponent.IntegerValue, Is.EqualTo(5));
                Assert.That(temporaryComponent.NestedList.Count, Is.EqualTo(2));
                Assert.That(temporaryComponent.NestedList[1].Number, Is.EqualTo(30));
                Assert.That(temporaryComponent.NestedList[1].Label, Is.EqualTo("second"));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(scenePath);
            }
        }

        [Test]
        [Category("Size.Small")]
        public void Schema_Plan_WhenTypeIsValid_ReturnsSchemaResult ()
        {
            var operation = new CompSchemaOperation();
            var typeId = IndexTypeIdFormatter.Format(typeof(CompOperationTestComponent));
            var requestOperation = CreateOperation(
                opId: "op-schema",
                opName: "ucli.comp.schema",
                args: new
                {
                    type = typeId,
                });

            var result = operation.Plan(requestOperation, new OperationExecutionContext(), CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Result.HasValue, Is.True);
            var schema = result.Result!.Value;
            Assert.That(schema.GetProperty("schemaKey").GetString(), Is.EqualTo($"comp:{typeId}"));
            Assert.That(schema.GetProperty("kind").GetString(), Is.EqualTo("comp"));
            Assert.That(schema.GetProperty("typeId").GetString(), Is.EqualTo(typeId));
            Assert.That(schema.GetProperty("displayName").GetString(), Is.EqualTo(nameof(CompOperationTestComponent)));
            Assert.That(schema.GetProperty("properties").ToString(), Does.Contain("\"path\":\"integerValue\""));
        }

        private static string CreateTemporaryScenePath ()
        {
            return $"Assets/CompOperationTests_{Guid.NewGuid():N}.unity";
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

        private static void AssertSuccess (
            OperationPhaseStepResult result,
            bool applied,
            bool changed,
            string scenePath)
        {
            Assert.That(result.IsSuccess, Is.True, result.Failure?.Message);
            Assert.That(result.Applied, Is.EqualTo(applied));
            Assert.That(result.Changed, Is.EqualTo(changed));
            Assert.That(result.Touched.Count, Is.EqualTo(1));
            Assert.That(result.Touched[0].Kind, Is.EqualTo(OperationTouchKind.Scene));
            Assert.That(result.Touched[0].Path, Is.EqualTo(scenePath));
            Assert.That(result.Failure, Is.Null);
        }
    }
}