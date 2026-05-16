using System;
using MackySoft.Ucli.Contracts;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.Index;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class AssetsFindOperationTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenNoConditionIsSpecified_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-find");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenPathPrefixIsOutsideAssets_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    pathPrefix = "ProjectSettings",
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-find");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenTypeIdCannotResolve_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    type = "Missing.Type, Missing.Assembly",
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-find");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenTypeIdIsNotUnityObject_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(string)),
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-find");
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenCursorIsInvalid_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    nameContains = "asset",
                    cursor = "not-a-cursor",
                });

            using var executionContext = new OperationExecutionContext();
            var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

            AssertInvalidArgument(result, "op-find");
            Assert.That(result.Failure!.Message, Does.Contain("args.cursor"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenLimitIsOutOfRange_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            var invalidLimits = new[] { 0, BoundedWindowConstants.MaxLimit + 1 };

            for (var i = 0; i < invalidLimits.Length; i++)
            {
                var requestOperation = CreateOperation(
                    opId: "op-find",
                    args: new
                    {
                        nameContains = "asset",
                        limit = invalidLimits[i],
                    });

                using var executionContext = new OperationExecutionContext();
                var result = await operation.ValidateAsync(requestOperation, executionContext, CancellationToken.None);

                AssertInvalidArgument(result, "op-find");
                Assert.That(result.Failure!.Message, Does.Contain("args.limit"));
            }
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator PlanAndCall_WhenMatchesExist_ReturnSortedMatchesInAssetPathOrder () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var firstPath = $"Assets/zzz_assets_find_{token}.asset";
            var secondPath = $"Assets/aaa_assets_find_{token}.asset";
            _ = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, firstPath, $"Sort-{token}-B");
            _ = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, secondPath, $"Sort-{token}-A");
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    nameContains = token,
                });
            var context = scope.CreateExecutionContext();

            var planResult = await operation.PlanAsync(requestOperation, context, CancellationToken.None);
            var callResult = await operation.CallAsync(requestOperation, context, CancellationToken.None);

            AssertQuerySuccess(planResult, applied: false);
            AssertQuerySuccess(callResult, applied: true);
            var plannedMatches = GetMatches(planResult);
            var liveMatches = GetMatches(callResult);
            Assert.That(plannedMatches.Count, Is.EqualTo(2));
            Assert.That(plannedMatches[0].AssetPath, Is.EqualTo(secondPath));
            Assert.That(plannedMatches[1].AssetPath, Is.EqualTo(firstPath));
            Assert.That(liveMatches.Count, Is.EqualTo(2));
            Assert.That(liveMatches[0].AssetPath, Is.EqualTo(secondPath));
            Assert.That(liveMatches[1].AssetPath, Is.EqualTo(firstPath));
            var window = GetWindow(planResult);
            Assert.That(window.GetProperty("limit").GetInt32(), Is.EqualTo(BoundedWindowConstants.DefaultLimit));
            Assert.That(window.TryGetProperty("after", out _), Is.False);
            Assert.That(window.GetProperty("isComplete").GetBoolean(), Is.True);
            Assert.That(window.GetProperty("totalCount").GetInt32(), Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenLimitAndCursorAreSpecified_ReturnsRequestedWindow () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var firstPath = $"Assets/aaa_assets_find_window_{token}.asset";
            var secondPath = $"Assets/bbb_assets_find_window_{token}.asset";
            var thirdPath = $"Assets/ccc_assets_find_window_{token}.asset";
            _ = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, firstPath, $"Window-{token}-A");
            _ = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, secondPath, $"Window-{token}-B");
            _ = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, thirdPath, $"Window-{token}-C");
            var cursor = BoundedWindowCursorCodec.Encode(1);
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    nameContains = token,
                    limit = 1,
                    cursor,
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertQuerySuccess(result, applied: false);
            var matches = GetMatches(result);
            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].AssetPath, Is.EqualTo(secondPath));
            var window = GetWindow(result);
            Assert.That(window.GetProperty("cursor").GetString(), Is.EqualTo(cursor));
            Assert.That(window.GetProperty("nextCursor").GetString(), Is.EqualTo(BoundedWindowCursorCodec.Encode(2)));
            Assert.That(window.GetProperty("isComplete").GetBoolean(), Is.False);
            Assert.That(window.GetProperty("totalCount").GetInt32(), Is.EqualTo(3));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenTypeFilterUsesBaseType_MatchesDerivedMainAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var assetPath = $"Assets/assets-find-assignable-{token}.asset";
            _ = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, assetPath, $"Assignable-{token}");
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(ScriptableObject)),
                    nameContains = token,
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertQuerySuccess(result, applied: true);
            var matches = GetMatches(result);
            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].AssetPath, Is.EqualTo(assetPath));
            Assert.That(matches[0].TypeId, Is.EqualTo(IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset))));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenNameContainsDiffersInCase_MatchesIgnoringCase () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var assetPath = $"Assets/assets-find-case-{token}.asset";
            var assetName = $"NeedleCase-{token}";
            _ = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, assetPath, assetName);
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    nameContains = assetName.ToLowerInvariant(),
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertQuerySuccess(result, applied: true);
            var matches = GetMatches(result);
            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].AssetPath, Is.EqualTo(assetPath));
            Assert.That(matches[0].Name, Is.EqualTo(assetName));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenFiltersAreCombined_ReturnsIntersectionOnly () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var matchingPath = $"Assets/assets-find-filter-{token}-match.asset";
            var sameTypeDifferentNamePath = $"Assets/assets-find-filter-{token}-other.asset";
            var matchingAsset = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, matchingPath, $"Needle-{token}");
            _ = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, sameTypeDifferentNamePath, $"Other-{token}");
            var textAssetPath = $"Assets/assets-find-filter-{token}-text.asset";
            _ = CreateTrackedTextAsset(scope, textAssetPath, $"Needle-{token}");
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    pathPrefix = $"Assets/assets-find-filter-{token}",
                    nameContains = "Needle",
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertQuerySuccess(result, applied: false);
            var matches = GetMatches(result);
            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].AssetPath, Is.EqualTo(matchingPath));
            Assert.That(matches[0].Name, Is.EqualTo(matchingAsset.name));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenCreateRanEarlier_DoesNotIncludePlannedAsset () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new AssetCreateOperation();
            var findOperation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var assetPath = $"Assets/assets-find-planned-call-{token}.asset";
            var context = scope.CreateExecutionContext();
            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                });
            var findRequest = CreateOperation(
                opId: "op-find",
                args: new
                {
                    pathPrefix = $"Assets/assets-find-planned-call-{token}",
                });

            var createResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var callResult = await findOperation.CallAsync(findRequest, context, CancellationToken.None);

            Assert.That(createResult.IsSuccess, Is.True, createResult.Failure?.Message);
            AssertQuerySuccess(callResult, applied: true);
            Assert.That(GetMatches(callResult), Is.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenCreateRanEarlier_IncludesPlannedAsset () => UniTask.ToCoroutine(async () =>
        {
            var createOperation = new AssetCreateOperation();
            var findOperation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var assetPath = $"Assets/assets-find-planned-{token}.asset";
            var context = scope.CreateExecutionContext();
            var createRequest = CreateOperation(
                opId: "op-create",
                opName: UcliPrimitiveOperationNames.AssetCreate,
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    path = assetPath,
                });
            var findRequest = CreateOperation(
                opId: "op-find",
                args: new
                {
                    pathPrefix = $"Assets/assets-find-planned-{token}",
                });

            var createResult = await createOperation.PlanAsync(createRequest, context, CancellationToken.None);
            var findResult = await findOperation.PlanAsync(findRequest, context, CancellationToken.None);

            Assert.That(createResult.IsSuccess, Is.True, createResult.Failure?.Message);
            AssertQuerySuccess(findResult, applied: false);
            var matches = GetMatches(findResult);
            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].AssetPath, Is.EqualTo(assetPath));
            Assert.That(matches[0].TypeId, Is.EqualTo(IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset))));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenSetRenamedAssetEarlier_DoesNotUseShadowStateForNameFilter () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new AssetSetOperation();
            var findOperation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var renamedName = $"Renamed-{token}";
            var assetPath = $"Assets/assets-find-shadow-call-{token}.asset";
            var asset = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, assetPath, $"Before-{token}");
            var context = scope.CreateExecutionContext();
            var setRequest = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
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
                            path = "m_Name",
                            value = renamedName,
                        },
                    },
                });
            var findRequest = CreateOperation(
                opId: "op-find",
                args: new
                {
                    nameContains = renamedName,
                });

            var setResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var callResult = await findOperation.CallAsync(findRequest, context, CancellationToken.None);

            Assert.That(setResult.IsSuccess, Is.True, setResult.Failure?.Message);
            AssertQuerySuccess(callResult, applied: true);
            Assert.That(asset.name, Is.EqualTo($"Before-{token}"));
            Assert.That(GetMatches(callResult), Is.Empty);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSetRenamedAssetEarlier_UsesShadowStateForNameFilter () => UniTask.ToCoroutine(async () =>
        {
            var setOperation = new AssetSetOperation();
            var findOperation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var renamedName = $"Renamed-{token}";
            var assetPath = $"Assets/assets-find-shadow-{token}.asset";
            var asset = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, assetPath, $"Before-{token}");
            var context = scope.CreateExecutionContext();
            var setRequest = CreateOperation(
                opId: "op-set",
                opName: UcliPrimitiveOperationNames.AssetSet,
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
                            path = "m_Name",
                            value = renamedName,
                        },
                    },
                });
            var findRequest = CreateOperation(
                opId: "op-find",
                args: new
                {
                    nameContains = renamedName,
                });

            var setResult = await setOperation.PlanAsync(setRequest, context, CancellationToken.None);
            var findResult = await findOperation.PlanAsync(findRequest, context, CancellationToken.None);

            Assert.That(setResult.IsSuccess, Is.True, setResult.Failure?.Message);
            AssertQuerySuccess(findResult, applied: false);
            Assert.That(asset.name, Is.EqualTo($"Before-{token}"));
            var matches = GetMatches(findResult);
            Assert.That(matches.Count, Is.EqualTo(1));
            Assert.That(matches[0].AssetPath, Is.EqualTo(assetPath));
            Assert.That(matches[0].Name, Is.EqualTo(renamedName));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Plan_WhenSubAssetMatchesName_ReturnsNoResultForSubAsset () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var assetPath = $"Assets/assets-find-subasset-{token}.asset";
            var mainAsset = CreateTrackedScriptableAsset<AssetOperationTestAsset>(scope, assetPath, $"Main-{token}");
            var subAsset = scope.TrackUnityObject(ScriptableObject.CreateInstance<AssetOperationTestAsset>());
            subAsset.name = $"Sub-{token}";
            AssetDatabase.AddObjectToAsset(subAsset, assetPath);
            AssetDatabase.SaveAssets();
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    type = IndexTypeIdFormatter.Format(typeof(AssetOperationTestAsset)),
                    nameContains = $"Sub-{token}",
                });

            var result = await operation.PlanAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertQuerySuccess(result, applied: false);
            var matches = GetMatches(result);
            Assert.That(matches, Is.Empty);
            Assert.That(mainAsset.name, Is.EqualTo($"Main-{token}"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenPrefixMatchesPrefabAndScene_IncludesBothMainAssets () => UniTask.ToCoroutine(async () =>
        {
            var operation = new AssetsFindOperation();
            using var scope = new EditorTestScope();
            var token = Guid.NewGuid().ToString("N");
            var pathPrefix = $"Assets/assets-find-main-assets-{token}";
            var prefabPath = $"{pathPrefix}-prefab.prefab";
            var scenePath = $"{pathPrefix}-scene.unity";
            CreateTrackedPrefabAsset(scope, prefabPath, $"Prefab-{token}");
            CreateTrackedSceneAsset(scope, scenePath);
            var requestOperation = CreateOperation(
                opId: "op-find",
                args: new
                {
                    pathPrefix,
                });

            var result = await operation.CallAsync(requestOperation, scope.CreateExecutionContext(), CancellationToken.None);

            AssertQuerySuccess(result, applied: true);
            var matches = GetMatches(result);
            Assert.That(matches.Count, Is.EqualTo(2));
            Assert.That(matches[0].AssetPath, Is.EqualTo(prefabPath));
            Assert.That(matches[1].AssetPath, Is.EqualTo(scenePath));
        });

        private static TAsset CreateTrackedScriptableAsset<TAsset> (
            EditorTestScope scope,
            string assetPath,
            string assetName)
            where TAsset : ScriptableObject
        {
            scope.TrackAsset(assetPath);
            var asset = scope.TrackUnityObject(ScriptableObject.CreateInstance<TAsset>());
            AssetDatabase.CreateAsset(asset, assetPath);
            return PersistMainAssetName<TAsset>(assetPath, assetName);
        }

        private static TextAsset CreateTrackedTextAsset (
            EditorTestScope scope,
            string assetPath,
            string assetName)
        {
            scope.TrackAsset(assetPath);
            var asset = scope.TrackUnityObject(new TextAsset("assets-find-test"));
            AssetDatabase.CreateAsset(asset, assetPath);
            return PersistMainAssetName<TextAsset>(assetPath, assetName);
        }

        private static TAsset PersistMainAssetName<TAsset> (
            string assetPath,
            string assetName)
            where TAsset : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
            Assert.That(asset, Is.Not.Null);

            var serializedObject = new SerializedObject(asset);
            var nameProperty = serializedObject.FindProperty("m_Name");
            Assert.That(nameProperty, Is.Not.Null);
            nameProperty.stringValue = assetName;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            var reloadedAsset = AssetDatabase.LoadAssetAtPath<TAsset>(assetPath);
            Assert.That(reloadedAsset, Is.Not.Null);
            Assert.That(reloadedAsset.name, Is.EqualTo(assetName));
            return reloadedAsset;
        }

        private static void CreateTrackedPrefabAsset (
            EditorTestScope scope,
            string prefabPath,
            string rootName)
        {
            scope.TrackAsset(prefabPath);
            var sourceRoot = new GameObject(rootName);
            try
            {
                var prefabAsset = PrefabUtility.SaveAsPrefabAsset(sourceRoot, prefabPath);
                Assert.That(prefabAsset, Is.Not.Null);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sourceRoot);
            }
        }

        private static void CreateTrackedSceneAsset (
            EditorTestScope scope,
            string scenePath)
        {
            scope.TrackAsset(scenePath);
            scope.EnableEditorSceneReset();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
        }

        private static NormalizedOperation CreateOperation (
            string opId,
            object args,
            string opName = UcliPrimitiveOperationNames.AssetsFind)
        {
            return new NormalizedOperation(
                Id: opId,
                Op: opName,
                Args: JsonSerializer.SerializeToElement(args),
                As: null,
                Expect: null,
                InternalExecutionKey: null);
        }

        private static IReadOnlyList<AssetMatchSnapshot> GetMatches (OperationPhaseStepResult result)
        {
            var matchesElement = result.Result!.Value.GetProperty("matches");
            var matches = new AssetMatchSnapshot[matchesElement.GetArrayLength()];
            var index = 0;
            foreach (var matchElement in matchesElement.EnumerateArray())
            {
                matches[index] = new AssetMatchSnapshot(
                    matchElement.GetProperty("assetPath").GetString()!,
                    matchElement.GetProperty("assetGuid").GetString()!,
                    matchElement.GetProperty("name").GetString()!,
                    matchElement.GetProperty("typeId").GetString()!);
                index++;
            }

            return matches;
        }

        private static JsonElement GetWindow (OperationPhaseStepResult result)
        {
            return result.Result!.Value.GetProperty("window");
        }

        private static void AssertInvalidArgument (
            OperationPhaseStepResult result,
            string expectedOperationId)
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(result.Failure.OpId, Is.EqualTo(expectedOperationId));
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

        private readonly struct AssetMatchSnapshot
        {
            public AssetMatchSnapshot (
                string assetPath,
                string assetGuid,
                string name,
                string typeId)
            {
                AssetPath = assetPath;
                AssetGuid = assetGuid;
                Name = name;
                TypeId = typeId;
            }

            public string AssetPath { get; }

            public string AssetGuid { get; }

            public string Name { get; }

            public string TypeId { get; }
        }
    }
}
