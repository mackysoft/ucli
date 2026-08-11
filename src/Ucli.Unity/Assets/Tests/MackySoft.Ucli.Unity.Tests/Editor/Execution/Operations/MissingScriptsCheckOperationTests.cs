using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.ContractReading;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Unity.Execution;
using MackySoft.Ucli.Unity.Execution.Dispatch;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;
using MackySoft.Ucli.Unity.SceneInspection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

#nullable enable

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class MissingScriptsCheckOperationTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenRequestedSavedAssetsHaveNoMissingScripts_ReturnsPassAndScannedAssets () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateProductionOperation();
            using var scope = new EditorTestScope();
            var directoryPath = CreateDirectory(scope);
            var scenePath = CreateScene(scope, directoryPath);
            var prefabPath = CreatePrefab(scope, directoryPath);
            var activeScenePath = SceneManager.GetActiveScene().path;
            var activeSceneDirty = SceneManager.GetActiveScene().isDirty;
            var sceneCount = SceneManager.sceneCount;

            var result = await operation.CallAsync(
                CreateOperation(new
                {
                    roots = new[] { directoryPath },
                    assetKinds = new[] { "scene", "prefab" },
                }),
                scope.CreateExecutionContext(),
                CancellationToken.None);

            AssertQuerySuccess(result);
            Assert.That(result.Verdict, Is.EqualTo(Verdict.Pass));
            Assert.That(GetPaths(result, "scannedAssets"), Is.EqualTo(new[] { prefabPath, scenePath }));
            Assert.That(GetArrayLength(result, "unscannedScopes"), Is.Zero);
            Assert.That(GetArrayLength(result, "unscannedAssets"), Is.Zero);
            Assert.That(GetArrayLength(result, "missingScriptSlots"), Is.Zero);
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(activeScenePath));
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.EqualTo(activeSceneDirty));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Validate_WhenRootIsNotAnExistingAssetsDirectory_ReturnsInvalidArgument () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateProductionOperation();
            using var scope = new EditorTestScope();
            var missingDirectory = $"Assets/missing-script-check-{Guid.NewGuid():N}";

            var result = await operation.ValidateAsync(
                CreateOperation(new
                {
                    roots = new[] { missingDirectory },
                    assetKinds = new[] { "scene" },
                }),
                scope.CreateExecutionContext(),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(result.Failure.Message, Does.Contain("args.roots[0]"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenJsonAssetKindsAreDuplicated_ReturnsInvalidArgumentWithoutResultOrVerdict () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateProductionOperation();
            using var scope = new EditorTestScope();
            var directoryPath = CreateDirectory(scope);

            var result = await operation.CallAsync(
                CreateOperation(new
                {
                    roots = new[] { directoryPath },
                    assetKinds = new[] { "scene", "scene" },
                }),
                scope.CreateExecutionContext(),
                CancellationToken.None);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Failure, Is.Not.Null);
            Assert.That(result.Failure!.Code, Is.EqualTo(UcliCoreErrorCodes.InvalidArgument));
            Assert.That(result.Result, Is.Null);
            Assert.That(result.Verdict, Is.Null);
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenMultipleMissingScriptSlotsAreConfirmed_ReturnsFailAndEachSlot () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateProductionOperation();
            using var scope = new EditorTestScope();
            var directoryPath = CreateDirectory(scope);
            var prefabPath = CreatePrefab(scope, directoryPath, missingScriptCount: 2);

            var result = await operation.CallAsync(
                CreateOperation(new
                {
                    roots = new[] { directoryPath },
                    assetKinds = new[] { "prefab" },
                }),
                scope.CreateExecutionContext(),
                CancellationToken.None);

            AssertQuerySuccess(result);
            Assert.That(result.Verdict, Is.EqualTo(Verdict.Fail));
            Assert.That(GetPaths(result, "scannedAssets"), Is.EqualTo(new[] { prefabPath }));
            var slots = result.Result!.Value.GetProperty("missingScriptSlots");
            Assert.That(slots.GetArrayLength(), Is.EqualTo(2));
            Assert.That(slots[0].GetProperty("assetPath").GetString(), Is.EqualTo(prefabPath));
            Assert.That(slots[0].GetProperty("hierarchyPath").GetString(), Is.EqualTo("checked"));
            Assert.That(slots[0].GetProperty("componentIndex").GetInt32(), Is.EqualTo(1));
            Assert.That(slots[1].GetProperty("componentIndex").GetInt32(), Is.EqualTo(2));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenSavedSceneHasMissingScript_ReturnsFailWithoutChangingActiveSceneState () => UniTask.ToCoroutine(async () =>
        {
            var operation = CreateProductionOperation();
            using var scope = new EditorTestScope();
            var directoryPath = CreateDirectory(scope);
            var scenePath = CreateScene(scope, directoryPath, missingScriptCount: 1);
            var activeScenePath = SceneManager.GetActiveScene().path;
            var activeSceneDirty = SceneManager.GetActiveScene().isDirty;
            var sceneCount = SceneManager.sceneCount;

            var result = await operation.CallAsync(
                CreateOperation(new
                {
                    roots = new[] { directoryPath },
                    assetKinds = new[] { "scene" },
                }),
                scope.CreateExecutionContext(),
                CancellationToken.None);

            AssertQuerySuccess(result);
            Assert.That(result.Verdict, Is.EqualTo(Verdict.Fail));
            var slot = result.Result!.Value.GetProperty("missingScriptSlots")[0];
            Assert.That(slot.GetProperty("assetPath").GetString(), Is.EqualTo(scenePath));
            Assert.That(slot.GetProperty("hierarchyPath").GetString(), Is.EqualTo("SceneRoot"));
            Assert.That(slot.GetProperty("componentIndex").GetInt32(), Is.EqualTo(1));
            Assert.That(SceneManager.GetActiveScene().path, Is.EqualTo(activeScenePath));
            Assert.That(SceneManager.GetActiveScene().isDirty, Is.EqualTo(activeSceneDirty));
            Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenSavedPrefabDisappearsOrCannotBeRead_ReturnsIncompleteWithActualEngineReasons () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var directoryPath = CreateDirectory(scope);
            var changedPath = CreatePrefab(scope, directoryPath, "changed");
            var unreadablePath = CreatePrefab(scope, directoryPath, "unreadable");
            var assetAccess = new DelegatingMissingScriptsAssetAccess
            {
                IsPrefabAssetOverride = assetPath => assetPath != changedPath,
                LoadPrefabContentsOverride = assetPath => assetPath == unreadablePath
                    ? throw new InvalidOperationException("Simulated unreadable prefab.")
                    : PrefabUtility.LoadPrefabContents(assetPath),
            };
            var operation = new MissingScriptsCheckOperation(new MissingScriptsScanEngine(assetAccess));

            var result = await operation.CallAsync(
                CreateOperation(new
                {
                    roots = new[] { directoryPath },
                    assetKinds = new[] { "prefab" },
                }),
                scope.CreateExecutionContext(),
                CancellationToken.None);

            AssertQuerySuccess(result);
            Assert.That(result.Verdict, Is.EqualTo(Verdict.Incomplete));
            var unscannedAssets = result.Result!.Value.GetProperty("unscannedAssets");
            Assert.That(unscannedAssets[0].GetProperty("reason").GetString(), Is.EqualTo("assetChanged"));
            Assert.That(unscannedAssets[1].GetProperty("reason").GetString(), Is.EqualTo("assetReadFailed"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenAssetDiscoveryFails_ReturnsIncompleteWithScopeReadFailed () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var directoryPath = CreateDirectory(scope);
            var assetAccess = new DelegatingMissingScriptsAssetAccess
            {
                FindAssetsOverride = (_, _) => throw new InvalidOperationException("Simulated discovery failure."),
            };
            var operation = new MissingScriptsCheckOperation(new MissingScriptsScanEngine(assetAccess));

            var result = await operation.CallAsync(
                CreateOperation(new
                {
                    roots = new[] { directoryPath },
                    assetKinds = new[] { "prefab" },
                }),
                scope.CreateExecutionContext(),
                CancellationToken.None);

            AssertQuerySuccess(result);
            Assert.That(result.Verdict, Is.EqualTo(Verdict.Incomplete));
            var unscannedScope = result.Result!.Value.GetProperty("unscannedScopes")[0];
            Assert.That(unscannedScope.GetProperty("root").GetString(), Is.EqualTo(directoryPath));
            Assert.That(unscannedScope.GetProperty("assetKind").GetString(), Is.EqualTo("prefab"));
            Assert.That(unscannedScope.GetProperty("reason").GetString(), Is.EqualTo("scopeReadFailed"));
        });

        [Test]
        [Category("Size.Small")]
        [TestCase(DiscoveryResolutionFailure.EmptyPath)]
        [TestCase(DiscoveryResolutionFailure.Throws)]
        [TestCase(DiscoveryResolutionFailure.InvalidPath)]
        [TestCase(DiscoveryResolutionFailure.KindMismatch)]
        public void Scan_WhenDiscoveredGuidCannotResolveToTheRequestedAssetKind_MarksScopeUnscanned (
            DiscoveryResolutionFailure failure)
        {
            var assetAccess = new DelegatingMissingScriptsAssetAccess
            {
                FindAssetsOverride = (_, _) => new[] { "guid" },
                GuidToAssetPathOverride = _ => failure switch
                {
                    DiscoveryResolutionFailure.EmptyPath => string.Empty,
                    DiscoveryResolutionFailure.Throws => throw new InvalidOperationException("Simulated GUID resolution failure."),
                    DiscoveryResolutionFailure.InvalidPath => "invalid path",
                    DiscoveryResolutionFailure.KindMismatch => "Assets/checked.unity",
                    _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, null),
                },
            };

            var result = new MissingScriptsScanEngine(assetAccess).Scan(new MissingScriptsCheckArgs(
                new[] { new UnityAssetPathPrefix("Assets") },
                new[] { MissingScriptsAssetKind.Prefab }));

            Assert.That(result.UnscannedScopes, Has.Count.EqualTo(1));
            Assert.That(result.UnscannedScopes[0].Root.Value, Is.EqualTo("Assets"));
            Assert.That(result.UnscannedScopes[0].AssetKind, Is.EqualTo(MissingScriptsAssetKind.Prefab));
            Assert.That(result.UnscannedScopes[0].Reason, Is.EqualTo(MissingScriptsUnscannedReason.ScopeReadFailed));
            Assert.That(result.ScannedAssets, Is.Empty);
            Assert.That(result.UnscannedAssets, Is.Empty);
        }

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenLoadedSavedPrefabHasUnrepresentableHierarchyPath_ReturnsIncomplete () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var directoryPath = CreateDirectory(scope);
            var prefabPath = CreatePrefab(scope, directoryPath);
            var assetAccess = new DelegatingMissingScriptsAssetAccess
            {
                LoadPrefabContentsOverride = assetPath =>
                {
                    var prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                    prefabRoot.name = "Invalid//Child";
                    return prefabRoot;
                },
            };
            var operation = new MissingScriptsCheckOperation(new MissingScriptsScanEngine(assetAccess));

            var result = await operation.CallAsync(
                CreateOperation(new
                {
                    roots = new[] { directoryPath },
                    assetKinds = new[] { "prefab" },
                }),
                scope.CreateExecutionContext(),
                CancellationToken.None);

            AssertQuerySuccess(result);
            Assert.That(result.Verdict, Is.EqualTo(Verdict.Incomplete));
            var unscannedAsset = result.Result!.Value.GetProperty("unscannedAssets")[0];
            Assert.That(unscannedAsset.GetProperty("assetPath").GetString(), Is.EqualTo(prefabPath));
            Assert.That(unscannedAsset.GetProperty("reason").GetString(), Is.EqualTo("hierarchyPathUnrepresentable"));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenMissingScriptAndUnscannedAssetAreBothObserved_PrefersFail () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var directoryPath = CreateDirectory(scope);
            var operation = new MissingScriptsCheckOperation(new StubMissingScriptsScanEngine(args =>
                CreateResult(
                    args,
                    unscannedAssets: new[]
                    {
                        new MissingScriptsUnscannedAsset(
                            new UnityAssetPath($"{directoryPath}/unreadable.prefab"),
                            MissingScriptsAssetKind.Prefab,
                            MissingScriptsUnscannedReason.AssetReadFailed),
                    },
                    missingScriptSlots: new[]
                    {
                        new MissingScriptSlot(
                            new UnityAssetPath($"{directoryPath}/missing.prefab"),
                            new UnityHierarchyPath("Root"),
                            componentIndex: 1),
                    })));

            var result = await operation.CallAsync(
                CreateOperation(new
                {
                    roots = new[] { directoryPath },
                    assetKinds = new[] { "prefab" },
                }),
                scope.CreateExecutionContext(),
                CancellationToken.None);

            AssertQuerySuccess(result);
            Assert.That(result.Verdict, Is.EqualTo(Verdict.Fail));
        });

        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator Call_WhenScannerCleanupFails_LeavesResultAndVerdictOutOfFailureTrace () => UniTask.ToCoroutine(async () =>
        {
            using var scope = new EditorTestScope();
            var directoryPath = CreateDirectory(scope);
            _ = CreatePrefab(scope, directoryPath);
            var assetAccess = new DelegatingMissingScriptsAssetAccess
            {
                UnloadPrefabContentsOverride = prefabRoot =>
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    throw new InvalidOperationException("Prefab cleanup failed.");
                },
            };
            var operation = new MissingScriptsCheckOperation(new MissingScriptsScanEngine(assetAccess));
            var normalizedOperation = CreateOperation(new
            {
                roots = new[] { directoryPath },
                assetKinds = new[] { "prefab" },
            });

            using var executionContext = scope.CreateExecutionContext();
            var callPassResult = await new OperationCallPassExecutor().ExecuteAsync(
                new[]
                {
                    new PreparedOperation(
                        normalizedOperation,
                        operation,
                        Array.Empty<OperationTouch>(),
                        PlanPersisted: false,
                        RequiresPreCallPlanReplay: false),
                },
                executionContext,
                CancellationToken.None);

            Assert.That(callPassResult.IsSuccess, Is.False);
            Assert.That(callPassResult.Errors, Has.Count.EqualTo(1));
            Assert.That(callPassResult.OperationTraces[0].Failure, Is.Not.Null);
            Assert.That(callPassResult.OperationTraces[0].Result, Is.Null);
            Assert.That(callPassResult.OperationTraces[0].Verdict, Is.Null);

            var response = ExecuteResponseBuilder.CreateExecutionResponse(
                new ExecuteDispatchContext(
                    Guid.NewGuid(),
                    new UnityProjectIdentity(
                        ProjectPathTestValues.RepositoryUnityProject,
                        ProjectFingerprintTestFactory.Create("missing-scripts-cleanup"),
                        "6000.1.4f1")),
                OperationPhase.Call,
                PhaseExecutionTrace.Failed(
                    new[]
                    {
                        new NormalizedRequestStep(
                            normalizedOperation.Id,
                            IpcExecuteStepKind.Op,
                            normalizedOperation.Op,
                            1,
                            operation.Metadata.DescriptorDigest),
                    },
                    callPassResult.OperationTraces,
                    callPassResult.Errors));

            Assert.That(response.Status, Is.EqualTo(IpcResponseStatus.Error));
            Assert.That(response.Errors, Has.Count.EqualTo(1));
            Assert.That(response.Payload.TryGetProperty("opResults", out var operationResults), Is.True, response.Payload.GetRawText());
            var operationResult = operationResults[0];
            Assert.That(operationResult.TryGetProperty("result", out _), Is.False);
            Assert.That(operationResult.GetProperty("verdict").ValueKind, Is.EqualTo(JsonValueKind.Null));
        });

        private static string CreateDirectory (EditorTestScope scope)
        {
            var directoryName = $"missing-script-check-{Guid.NewGuid():N}";
            var guid = AssetDatabase.CreateFolder("Assets", directoryName);
            Assert.That(guid, Is.Not.Empty);
            var directoryPath = $"Assets/{directoryName}";
            scope.TrackAsset(directoryPath);
            return directoryPath;
        }

        private static string CreateScene (
            EditorTestScope scope,
            string directoryPath,
            int missingScriptCount = 0)
        {
            var scenePath = $"{directoryPath}/checked.unity";
            scope.TrackAsset(scenePath);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("SceneRoot");
            for (var i = 0; i < missingScriptCount; i++)
            {
                _ = root.AddComponent<CompOperationTestComponent>();
            }

            Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
            if (missingScriptCount > 0)
            {
                ReplaceComponentScriptGuidWithMissing(scenePath);
            }

            _ = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            return scenePath;
        }

        private static string CreatePrefab (
            EditorTestScope scope,
            string directoryPath,
            string assetName = "checked",
            int missingScriptCount = 0)
        {
            var prefabPath = $"{directoryPath}/{assetName}.prefab";
            scope.TrackAsset(prefabPath);
            var source = new GameObject("PrefabRoot");
            try
            {
                for (var i = 0; i < missingScriptCount; i++)
                {
                    _ = source.AddComponent<CompOperationTestComponent>();
                }

                Assert.That(PrefabUtility.SaveAsPrefabAsset(source, prefabPath), Is.Not.Null);
                AssetDatabase.SaveAssets();
                if (missingScriptCount > 0)
                {
                    ReplaceComponentScriptGuidWithMissing(prefabPath);
                }

                return prefabPath;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static void ReplaceComponentScriptGuidWithMissing (string assetPath)
        {
            const string componentScriptPath = "Assets/Tests/MackySoft.Ucli.Unity.Tests/Runtime/CompOperationTestComponent.cs";
            var componentScriptGuid = AssetDatabase.AssetPathToGUID(componentScriptPath);
            Assert.That(componentScriptGuid, Is.Not.Empty);
            var absolutePath = Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length));
            var contents = File.ReadAllText(absolutePath);
            var original = $"guid: {componentScriptGuid}";
            Assert.That(contents, Does.Contain(original));
            File.WriteAllText(contents: contents.Replace(original, "guid: d41d8cd98f00b204e9800998ecf8427e"), path: absolutePath);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static NormalizedOperation CreateOperation (object args)
        {
            return new NormalizedOperation(
                OperationExecutionKey.ForRawStep(new IpcExecuteStepId("missing-scripts")),
                UcliPrimitiveOperationNames.ProjectMissingScriptsCheck,
                JsonSerializer.SerializeToElement(args),
                As: null,
                Expect: null,
                AliasReferences: OperationAliasReferenceMap.Empty,
                PersistenceReportingPolicy: OperationPersistenceReportingPolicy.ReportAll,
                AllowExplicitPrefabAssetMutation: false);
        }

        private static MissingScriptsCheckOperation CreateProductionOperation ()
        {
            return new MissingScriptsCheckOperation(new MissingScriptsScanEngine(new UnityMissingScriptsAssetAccess()));
        }

        private static MissingScriptsCheckResult CreateResult (
            MissingScriptsCheckArgs args,
            IReadOnlyList<MissingScriptsUnscannedScope>? unscannedScopes = null,
            IReadOnlyList<UnityAssetPath>? scannedAssets = null,
            IReadOnlyList<MissingScriptsUnscannedAsset>? unscannedAssets = null,
            IReadOnlyList<MissingScriptSlot>? missingScriptSlots = null)
        {
            return new MissingScriptsCheckResult(
                new MissingScriptsRequestedScope(args.Roots, args.AssetKinds),
                unscannedScopes ?? Array.Empty<MissingScriptsUnscannedScope>(),
                scannedAssets ?? Array.Empty<UnityAssetPath>(),
                unscannedAssets ?? Array.Empty<MissingScriptsUnscannedAsset>(),
                missingScriptSlots ?? Array.Empty<MissingScriptSlot>());
        }

        private sealed class StubMissingScriptsScanEngine : IMissingScriptsScanEngine
        {
            private readonly Func<MissingScriptsCheckArgs, MissingScriptsCheckResult> scan;

            public StubMissingScriptsScanEngine (Func<MissingScriptsCheckArgs, MissingScriptsCheckResult> scan)
            {
                this.scan = scan ?? throw new ArgumentNullException(nameof(scan));
            }

            public MissingScriptsCheckResult Scan (MissingScriptsCheckArgs args)
            {
                return scan(args);
            }
        }

        private sealed class DelegatingMissingScriptsAssetAccess : IMissingScriptsAssetAccess
        {
            public Func<string, string[], string[]>? FindAssetsOverride { get; set; }

            public Func<string, string>? GuidToAssetPathOverride { get; set; }

            public Func<string, bool>? IsPrefabAssetOverride { get; set; }

            public Func<string, GameObject>? LoadPrefabContentsOverride { get; set; }

            public Action<GameObject>? UnloadPrefabContentsOverride { get; set; }

            public string[] FindAssets (string filter, string[] searchInFolders)
            {
                return FindAssetsOverride?.Invoke(filter, searchInFolders)
                    ?? AssetDatabase.FindAssets(filter, searchInFolders);
            }

            public string GuidToAssetPath (string assetGuid)
            {
                return GuidToAssetPathOverride?.Invoke(assetGuid)
                    ?? AssetDatabase.GUIDToAssetPath(assetGuid);
            }

            public bool IsSceneAsset (string assetPath)
            {
                return AssetDatabase.LoadAssetAtPath<SceneAsset>(assetPath) != null;
            }

            public bool IsPrefabAsset (string assetPath)
            {
                return IsPrefabAssetOverride?.Invoke(assetPath)
                    ?? AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null;
            }

            public bool TryAcquirePersistedPreview (string assetPath, out SceneSourceLease lease)
            {
                return SceneReadSourceResolver.TryAcquirePersistedPreview(assetPath, out lease, out _);
            }

            public GameObject LoadPrefabContents (string assetPath)
            {
                return LoadPrefabContentsOverride?.Invoke(assetPath)
                    ?? PrefabUtility.LoadPrefabContents(assetPath);
            }

            public void UnloadPrefabContents (GameObject prefabRoot)
            {
                if (UnloadPrefabContentsOverride != null)
                {
                    UnloadPrefabContentsOverride(prefabRoot);
                    return;
                }

                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        public enum DiscoveryResolutionFailure
        {
            EmptyPath,
            Throws,
            InvalidPath,
            KindMismatch,
        }

        private static int GetArrayLength (
            OperationPhaseStepResult result,
            string propertyName)
        {
            return result.Result!.Value.GetProperty(propertyName).GetArrayLength();
        }

        private static string[] GetPaths (
            OperationPhaseStepResult result,
            string propertyName)
        {
            var paths = new string[result.Result!.Value.GetProperty(propertyName).GetArrayLength()];
            var index = 0;
            foreach (var path in result.Result!.Value.GetProperty(propertyName).EnumerateArray())
            {
                paths[index] = path.GetString()!;
                index++;
            }

            return paths;
        }

        private static void AssertQuerySuccess (OperationPhaseStepResult result)
        {
            Assert.That(result.IsSuccess, Is.True, result.Failure?.Message);
            Assert.That(result.Applied, Is.False);
            Assert.That(result.Changed, Is.False);
            Assert.That(result.Touched, Is.Empty);
            Assert.That(result.Result.HasValue, Is.True);
            Assert.That(result.Failure, Is.Null);
        }
    }
}
