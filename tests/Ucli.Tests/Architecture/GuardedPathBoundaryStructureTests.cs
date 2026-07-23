using System.Text.RegularExpressions;

namespace MackySoft.Ucli.Tests.Architecture;

public sealed class GuardedPathBoundaryStructureTests
{
    private static readonly string[] ProductionSourceRoots =
    [
        "src/Ucli",
        "src/Ucli.Application",
        "src/Ucli.Contracts",
        "src/Ucli.Infrastructure",
        "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity",
    ];

    private const string UnityAssetPathUtilityPath =
        "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/Project/UnityAssetPathUtility.cs";

    private static readonly IReadOnlyDictionary<string, int> ExpectedProductLowLevelPathApiMatches =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [CreateMatchKey(UnityAssetPathUtilityPath, "Path.GetDirectoryName(")] = 1,
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedGuardedPathAdapterMatches =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [CreateMatchKey(
            "src/Ucli.Application/Features/Assurance/Build/Artifacts/BuildRunnerOutputPathAdapter.cs",
            "RootRelativePath.Parse(path.Value)")] = 1,
            [CreateMatchKey(
            "src/Ucli.Infrastructure/Paths/BuildRunnerOutputPathAdapter.cs",
            "RootRelativePath.Parse(path.Value)")] = 1,
            [CreateMatchKey(
            "src/Ucli.Infrastructure/Paths/ProjectMutationAuditPathAdapter.cs",
            "RootRelativePath.Parse(path.Value)")] = 1,
            [CreateMatchKey(
            "src/Ucli.Application/Shared/Execution/ReadIndex/Scenes/SceneTreeLiteSourcePaths.cs",
            "RootRelativePath.Parse(sceneAssetPath.Value)")] = 1,
            [CreateMatchKey(
            "src/Ucli.Application/Shared/Execution/ReadIndex/Scenes/SceneTreeLiteSourcePaths.cs",
            "RootRelativePath.Parse(sceneRelativePath.Value+UnityAssetPathContract.MetaFileExtension)")] = 1,
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedPathValidationCatchMatches =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [CreateMatchKey(
            "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/Ipc/Methods/BuildRun/BuildRunUnityIpcMethodHandler.cs",
            "catch (PathValidationException exception)")] = 1,
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedDeterministicPathTextMatches =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [CreateMatchKey(
            "src/Ucli.Infrastructure/Project/UnityProjectFingerprintCalculator.cs",
            "DeterministicPathText.ForIdentity(")] = 3,
            [CreateMatchKey(
            "src/Ucli.Infrastructure/Index/Inputs/IndexInputFileHasher.cs",
            "DeterministicPathText.ForIdentity(")] = 1,
            [CreateMatchKey(
            "src/Ucli/Shared/Execution/Lifecycle/FileSystemProjectLifecycleLockProvider.cs",
            "DeterministicPathText.ForIdentity(")] = 1,
            [CreateMatchKey(
            "src/Ucli/Features/Daemon/Supervisor/SupervisorWorktreeIdentity.cs",
            "DeterministicPathText.ForIdentity(")] = 1,
        };

    private static readonly IReadOnlyDictionary<string, int> ExpectedPortablePathAdapterMatches =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            [CreateMatchKey(
            "src/Ucli.Infrastructure/Paths/ProjectMutationAuditPathAdapter.cs",
            "UcliPortablePathAdapter.TryFormat(")] = 1,
            [CreateMatchKey(
            "src/Ucli/Features/Assurance/Build/FileBuildRunArtifactStore.cs",
            "UcliPortablePathAdapter.TryFormat(")] = 2,
            [CreateMatchKey(
            "src/Ucli/Features/Assurance/Verify/FileVerifyProfileFileReader.cs",
            "UcliPortablePathAdapter.TryFormat(")] = 1,
            [CreateMatchKey(
            "src/Ucli/Features/Screenshot/Artifacts/FileScreenshotArtifactStore.cs",
            "UcliPortablePathAdapter.TryFormat(")] = 1,
            [CreateMatchKey(
            "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/Execution/Phases/Ops/project/ProjectOperationUtilities.cs",
            "UcliPortablePathAdapter.TryFormat(")] = 1,
        };

    private static readonly Regex LegacyHelperPattern = new(
        @"\b(?:FullPathNormalizationResult|IpcBuildOutputLayoutResolver|PathFormatExceptionClassifier|PathIdentity|PathNormalizationFailureKind|PathNormalizer|PathStringNormalizer|RepositoryPathNormalizationResult|RepositoryPathNormalizer)\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex PathValidationCatchPattern = new(
        @"\bcatch\s*\(\s*(?:(?:global\s*::\s*)?MackySoft\s*\.\s*FileSystem\s*\.\s*)?PathValidationException\b[^)]*\)",
        RegexOptions.CultureInvariant);

    private static readonly Regex DeterministicPathTextPattern = new(
        @"\bDeterministicPathText\s*\.\s*ForIdentity\s*\(",
        RegexOptions.CultureInvariant);

    private static readonly Regex PortablePathAdapterPattern = new(
        @"\bUcliPortablePathAdapter\s*\.\s*TryFormat\s*\(",
        RegexOptions.CultureInvariant);

    private static readonly Regex SystemPathAliasPattern = new(
        @"^\s*(?:global\s+)?using\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:global\s*::\s*)?System\s*\.\s*IO\s*\.\s*Path\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly Regex SystemIoNamespaceAliasPattern = new(
        @"^\s*(?:global\s+)?using\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:global\s*::\s*)?System\s*\.\s*IO\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly Regex SystemPathStaticUsingPattern = new(
        @"^\s*(?:global\s+)?using\s+static\s+(?:global\s*::\s*)?System\s*\.\s*IO\s*\.\s*Path\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly Regex GuardedPathAliasPattern = new(
        @"^\s*(?:global\s+)?using\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:global\s*::\s*)?MackySoft\s*\.\s*FileSystem\s*\.\s*(?:AbsolutePath|RootRelativePath|ContainedPath)\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly Regex GuardedPathNamespaceAliasPattern = new(
        @"^\s*(?:global\s+)?using\s+(?<alias>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?:global\s*::\s*)?MackySoft\s*\.\s*FileSystem\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    private static readonly Regex GuardedPathStaticUsingPattern = new(
        @"^\s*(?:global\s+)?using\s+static\s+(?:global\s*::\s*)?MackySoft\s*\.\s*FileSystem\s*\.\s*(?:AbsolutePath|RootRelativePath|ContainedPath)\s*;",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    [Fact]
    [Trait("Size", "Small")]
    public void ProductionSource_DoesNotReintroduceLegacyPathHelpers ()
    {
        AssertNoMatches(
            EnumerateProductionSourceFiles(),
            LegacyHelperPattern,
            "Legacy raw path helper references must converge on guarded path values.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LowLevelLexicalPathApis_AreConfinedToExactConsumerAdapters ()
    {
        AssertExactMatches(
            FindLowLevelPathApiMatches(EnumerateProductionSourceFiles()).ToArray(),
            ExpectedProductLowLevelPathApiMatches,
            "Low-level lexical path APIs must remain in the exact wire or logical consumer adapter expressions.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void LowLevelLexicalPathApiDetection_CoversAliasesAndGlobalQualifiedReferences ()
    {
        var sourceFile = new SourceFile(
            "src/Synthetic/RawPathConsumer.cs",
            """
            using P = System.IO.Path;
            using IO = System.IO;
            using static System.IO.Path;

            var aliased = P.GetFullPath(candidate);
            var globallyQualified = global::System.IO.Path.GetFullPath(candidate);
            var namespaceAliased = IO.Path.GetRelativePath(boundary, candidate);
            var staticallyImported = GetPathRoot(candidate);
            """);

        var matches = FindLowLevelPathApiMatches([sourceFile]).ToArray();

        Assert.Equal(4, matches.Length);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GuardedValues_AreNotReenteredIntoRawPathFactories ()
    {
        AssertExactMatches(
            FindGuardedValueReentryMatches(EnumerateProductionSourceFiles()).ToArray(),
            ExpectedGuardedPathAdapterMatches,
            "A guarded path Value may enter a raw path factory only at an exact product-contract adapter expression.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DeterministicPathText_IsConfinedToExactPersistedIdentityConsumers ()
    {
        AssertExactMatches(
            FindMatches(
                    EnumerateProductionSourceFiles(),
                    DeterministicPathTextPattern)
                .ToArray(),
            ExpectedDeterministicPathTextMatches,
            "Deterministic path text must remain confined to exact persisted identity consumers.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PortablePathConversion_IsConfinedToExactProductContractAdapters ()
    {
        AssertExactMatches(
            FindMatches(
                    EnumerateProductionSourceFiles(),
                    PortablePathAdapterPattern)
                .ToArray(),
            ExpectedPortablePathAdapterMatches,
            "Current-platform guarded paths must enter portable contracts only through exact product adapters.");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PortableContractConsumers_DoNotExposeGuardedPlatformPathTextDirectly ()
    {
        AssertSourceDoesNotContain(
            "src/Ucli.Infrastructure/Paths/ProjectMutationAuditPathAdapter.cs",
            "ProjectMutationAuditPath.TryParse(path.Value");
        AssertSourceDoesNotContain(
            "src/Ucli/Features/Assurance/Build/FileBuildRunArtifactStore.cs",
            "RelativePath.Value");
        AssertSourceDoesNotContain(
            "src/Ucli/Hosting/Cli/Screenshot/ScreenshotCommandResultFactory.cs",
            "artifact.Path.Value");
        AssertSourceDoesNotContain(
            "src/Ucli.Application/Features/Assurance/Verify",
            "RepositoryRelativePath?.Value");
        AssertSourceDoesNotContain(
            "src/Ucli.Unity/Assets/MackySoft/MackySoft.Ucli.Unity/Editor/Execution/Phases/Ops/project/ProjectOperationUtilities.cs",
            "containedPath.RelativePath.Value");
    }

    [Fact]
    [Trait("Size", "Small")]
    public void AssetsSceneValue_DoesNotRoundTripThroughTheBroaderSceneContract ()
    {
        var resolveService = Assert.Single(
            EnumerateSourceFiles("src/Ucli.Application/Features/Requests/Resolve"),
            static source => source.Path.EndsWith(
                "/ResolveService.cs",
                StringComparison.Ordinal));
        var sourceRefreshService = Assert.Single(
            EnumerateSourceFiles("src/Ucli/UnityIntegration/Indexing/Scenes"),
            static source => source.Path.EndsWith(
                "/SceneTreeLiteSourceRefreshService.cs",
                StringComparison.Ordinal));

        Assert.DoesNotContain(
            "new UnityScenePath(selector.Scene.Value)",
            resolveService.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "SceneAssetPath.TryParse(scenePath.Value",
            sourceRefreshService.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildPipelineOutputLayout_IsNotReconstructedByTheArtifactStore ()
    {
        var artifactStore = Assert.Single(
            EnumerateSourceFiles("src/Ucli/Features/Assurance/Build"),
            static source => source.Path.EndsWith(
                "/FileBuildRunArtifactStore.cs",
                StringComparison.Ordinal));

        Assert.DoesNotContain(
            "BuildPipelineOutputLayoutResolver.",
            artifactStore.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "BuildRunnerOutputPathAdapter.",
            artifactStore.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void GuardedValueReentryDetection_CoversAliasesResolveMethodsAndMultilineArguments ()
    {
        var sourceFile = new SourceFile(
            "src/Synthetic/GuardedPathConsumer.cs",
            """
            using GuardedAbsolute = MackySoft.FileSystem.AbsolutePath;
            using Fs = MackySoft.FileSystem;
            using static MackySoft.FileSystem.RootRelativePath;

            var parsed = GuardedAbsolute.Parse(
                candidate
                    .Value);
            var resolved = AbsolutePath.Resolve(
                boundary,
                candidate.Value);
            var contained = ContainedPath.TryResolve(
                boundary,
                candidate
                    .Value,
                out _,
                out _);
            var namespaceAliased = Fs.AbsolutePath.Parse(candidate.Value);
            var transformed = RootRelativePath.Parse(candidate.Value + ".meta");
            var interpolated = RootRelativePath.Parse($"{candidate.Value}.meta");
            var rawPath = candidate.Value;
            var staticallyImported = Parse(rawPath);
            """);

        var matches = FindGuardedValueReentryMatches([sourceFile]).ToArray();

        Assert.Equal(7, matches.Length);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnixDomainSocketWireContract_DoesNotOwnFilesystemNormalization ()
    {
        var endpointSource = Assert.Single(
            EnumerateSourceFiles("src/Ucli.Contracts/Ipc/Bootstrap"),
            static source => source.Path.EndsWith(
                "/IpcEndpoint.cs",
                StringComparison.Ordinal));

        Assert.DoesNotContain("System.IO", endpointSource.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Path.", endpointSource.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "UnixDomainSocketPathMaxBytes",
            endpointSource.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void SupervisorUnixSocketOwnership_GuardsTemporaryRootOnlyOnce ()
    {
        var ownershipSource = Assert.Single(
            EnumerateSourceFiles("src/Ucli/Features/Daemon/Supervisor/Transport"),
            static source => source.Path.EndsWith(
                "/SupervisorUnixSocketEndpointOwnership.cs",
                StringComparison.Ordinal));

        Assert.Single(
            Regex.Matches(
                ownershipSource.Text,
                @"AbsolutePath\s*\.\s*Parse\s*\(\s*Path\s*\.\s*GetTempPath\s*\(\s*\)\s*\)",
                RegexOptions.CultureInvariant)
                .Cast<Match>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ReadIndexArtifactReader_DoesNotClassifyGuardedPathAssemblyAsInputFailure ()
    {
        var readerSource = Assert.Single(
            EnumerateSourceFiles("src/Ucli/UnityIntegration/Indexing/Core"),
            static source => source.Path.EndsWith(
                "/FileReadIndexArtifactReader.cs",
                StringComparison.Ordinal));

        Assert.DoesNotContain(
            "catch (PathValidationException",
            readerSource.Text,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Index path is invalid",
            readerSource.Text,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PathValidationFailures_AreCaughtOnlyAtExactRawRequestAdapters ()
    {
        AssertExactMatches(
            FindMatches(
                    EnumerateProductionSourceFiles(),
                    PathValidationCatchPattern)
                .ToArray(),
            ExpectedPathValidationCatchMatches,
            "Path validation failures must not be reclassified after guarded paths enter product internals.");
    }

    private static IEnumerable<SourceFile> EnumerateProductionSourceFiles ()
    {
        return ProductionSourceRoots.SelectMany(EnumerateSourceFiles);
    }

    private static IEnumerable<SourceFile> EnumerateSourceFiles (string root)
    {
        return TestRepositoryPaths
            .EnumerateRegularFiles(root, "*.cs")
            .Select(static path => new SourceFile(
                TestRepositoryPaths.NormalizeRepositoryRelativePath(path),
                File.ReadAllText(path)));
    }

    private static void AssertSourceDoesNotContain (
        string path,
        string value)
    {
        var sourceFiles = path.EndsWith(".cs", StringComparison.Ordinal)
            ? new[]
            {
                new SourceFile(path, File.ReadAllText(TestRepositoryPaths.GetRegularFileFullPath(path))),
            }
            : EnumerateSourceFiles(path).ToArray();
        Assert.DoesNotContain(
            sourceFiles,
            source => source.Text.Contains(value, StringComparison.Ordinal));
    }

    private static void AssertNoMatches (
        IEnumerable<SourceFile> sourceFiles,
        Regex pattern,
        string message)
    {
        AssertNoViolations(FindMatches(sourceFiles, pattern).ToArray(), message);
    }

    private static IEnumerable<SourceMatch> FindMatches (
        IEnumerable<SourceFile> sourceFiles,
        Regex pattern)
    {
        foreach (var sourceFile in sourceFiles)
        {
            foreach (Match match in pattern.Matches(sourceFile.Text))
            {
                yield return new SourceMatch(
                    sourceFile.Path,
                    GetLineNumber(sourceFile.Text, match.Index),
                    match.Value);
            }
        }
    }

    private static IEnumerable<SourceMatch> FindGuardedValueReentryMatches (
        IEnumerable<SourceFile> sourceFiles)
    {
        const string builtInOwnerPattern =
            @"(?:(?:global\s*::\s*)?MackySoft\s*\.\s*FileSystem\s*\.\s*)?(?:AbsolutePath|RootRelativePath|ContainedPath)";
        const string memberAccessPattern =
            @"[A-Za-z_][A-Za-z0-9_]*(?:\s*!?\s*\.\s*[A-Za-z_][A-Za-z0-9_]*)*";
        var sourceFileArray = sourceFiles.ToArray();

        foreach (var sourceFile in sourceFileArray)
        {
            var aliases = GuardedPathAliasPattern
                .Matches(sourceFile.Text)
                .Select(static match => Regex.Escape(match.Groups["alias"].Value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var namespaceAliases = GuardedPathNamespaceAliasPattern
                .Matches(sourceFile.Text)
                .Select(static match =>
                    Regex.Escape(match.Groups["alias"].Value)
                    + @"\s*\.\s*(?:AbsolutePath|RootRelativePath|ContainedPath)")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var ownerAlternatives = new[] { builtInOwnerPattern }
                .Concat(aliases)
                .Concat(namespaceAliases);
            var ownerPattern = $"(?:{string.Join("|", ownerAlternatives)})";
            var allowsStaticFactoryCalls = GuardedPathStaticUsingPattern.IsMatch(sourceFile.Text);
            var factoryPrefixPattern = allowsStaticFactoryCalls
                ? $@"(?:(?:{ownerPattern})\s*\.\s*)?"
                : $@"(?:{ownerPattern})\s*\.\s*";
            var valueAccessPattern = $@"{memberAccessPattern}\s*!?\s*\.\s*Value";
            var directFactoryPattern = new Regex(
                $@"\b{factoryPrefixPattern}(?:Parse|TryParse)\s*\(\s*[^(),;]{{0,300}}?{valueAccessPattern}[^(),;]{{0,300}}?(?:,|\))",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);
            var resolveFactoryPattern = new Regex(
                $@"\b{factoryPrefixPattern}(?:Resolve|TryResolve)\s*\(\s*[^(),;]{{0,300}},\s*[^(),;]{{0,300}}?{valueAccessPattern}[^(),;]{{0,300}}?(?:,|\))",
                RegexOptions.CultureInvariant | RegexOptions.Singleline);

            foreach (var match in FindMatches([sourceFile], directFactoryPattern))
            {
                yield return match;
            }

            foreach (var match in FindMatches([sourceFile], resolveFactoryPattern))
            {
                yield return match;
            }

            var guardedValueAssignments = new Regex(
                    $@"\b(?:var|string)\s+(?<variable>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*{valueAccessPattern}\s*;",
                    RegexOptions.CultureInvariant)
                .Matches(sourceFile.Text)
                .Cast<Match>()
                .Select(static match => match.Groups["variable"].Value)
                .Distinct(StringComparer.Ordinal);
            foreach (var variable in guardedValueAssignments)
            {
                var escapedVariable = Regex.Escape(variable);
                var assignedDirectFactoryPattern = new Regex(
                    $@"\b{factoryPrefixPattern}(?:Parse|TryParse)\s*\(\s*{escapedVariable}\s*(?:,|\))",
                    RegexOptions.CultureInvariant);
                var assignedResolveFactoryPattern = new Regex(
                    $@"\b{factoryPrefixPattern}(?:Resolve|TryResolve)\s*\(\s*(?:(?!;).){{0,300}}?,\s*{escapedVariable}\s*(?:,|\))",
                    RegexOptions.CultureInvariant | RegexOptions.Singleline);

                foreach (var match in FindMatches([sourceFile], assignedDirectFactoryPattern))
                {
                    yield return match;
                }

                foreach (var match in FindMatches([sourceFile], assignedResolveFactoryPattern))
                {
                    yield return match;
                }
            }
        }
    }

    private static IEnumerable<SourceMatch> FindLowLevelPathApiMatches (
        IEnumerable<SourceFile> sourceFiles)
    {
        const string builtInOwnerPattern =
            @"(?:(?:global\s*::\s*)?System\s*\.\s*IO\s*\.\s*)?Path";

        foreach (var sourceFile in sourceFiles)
        {
            var aliases = SystemPathAliasPattern
                .Matches(sourceFile.Text)
                .Select(static match => Regex.Escape(match.Groups["alias"].Value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var namespaceAliases = SystemIoNamespaceAliasPattern
                .Matches(sourceFile.Text)
                .Select(static match =>
                    Regex.Escape(match.Groups["alias"].Value) + @"\s*\.\s*Path")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var ownerAlternatives = new[] { builtInOwnerPattern }
                .Concat(aliases)
                .Concat(namespaceAliases);
            var ownerPattern = $"(?:{string.Join("|", ownerAlternatives)})";
            var allowsStaticCalls = SystemPathStaticUsingPattern.IsMatch(sourceFile.Text);
            var ownerPrefixPattern = allowsStaticCalls
                ? $@"(?:(?:{ownerPattern})\s*\.\s*)?"
                : $@"(?:{ownerPattern})\s*\.\s*";
            var apiPattern = new Regex(
                $@"\b{ownerPrefixPattern}(?:GetDirectoryName|GetFullPath|GetPathRoot|GetRelativePath|IsPathFullyQualified|IsPathRooted)\s*\(",
                RegexOptions.CultureInvariant);

            foreach (var match in FindMatches([sourceFile], apiPattern))
            {
                yield return match;
            }
        }
    }

    private static void AssertExactMatches (
        IReadOnlyCollection<SourceMatch> actualMatches,
        IReadOnlyDictionary<string, int> expectedMatches,
        string message)
    {
        var actualCounts = actualMatches
            .GroupBy(static match => CreateMatchKey(match.Path, match.Expression), StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        var violations = actualCounts
            .Where(pair =>
                !expectedMatches.TryGetValue(pair.Key, out var expectedCount)
                || pair.Value != expectedCount)
            .Select(static pair => $"{pair.Key}: actual={pair.Value}")
            .Concat(
                expectedMatches
                    .Where(pair =>
                        !actualCounts.TryGetValue(pair.Key, out var actualCount)
                        || actualCount != pair.Value)
                    .Select(static pair => $"{pair.Key}: expected={pair.Value}"))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            $"{message}{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static string CreateMatchKey (
        string path,
        string expression)
    {
        return path + "|" + Regex.Replace(expression, @"\s+", string.Empty);
    }

    private static void AssertNoViolations (
        IReadOnlyCollection<SourceMatch> violations,
        string message)
    {
        Assert.True(
            violations.Count == 0,
            $"{message}{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    private static int GetLineNumber (
        string text,
        int characterIndex)
    {
        var lineNumber = 1;
        for (var index = 0; index < characterIndex; index++)
        {
            if (text[index] == '\n')
            {
                lineNumber++;
            }
        }

        return lineNumber;
    }

    private sealed record SourceFile (
        string Path,
        string Text);

    private sealed record SourceMatch (
        string Path,
        int Line,
        string Expression)
    {
        public override string ToString ()
        {
            return $"{Path}:{Line}: {Expression}";
        }
    }
}
