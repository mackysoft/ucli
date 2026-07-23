using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
            [CreateMatchKey(
            "src/Ucli/Features/Daemon/Supervisor/Transport/SupervisorUnixSocketEndpointOwnership.cs",
            """RootRelativePath.Parse($".{Path.GetFileName(canonicalAddress.Value)}.{publicationToken:N}.link")""")] = 1,
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
    public void GuardedValueReentryDetection_CoversAliasesNestedTransformationsAndMultilineArguments ()
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
            var nested = RootRelativePath.Parse(Path.GetFileName(candidate.Value));
            var rawPath = candidate.Value;
            var staticallyImported = Parse(rawPath);
            var transformedPath = Path.GetFileName(candidate.Value);
            var transformedLocal = RootRelativePath.Parse(transformedPath);
            var rawFileNameSource = candidate.Value;
            var nestedTransformedLocal = RootRelativePath.Parse(Path.GetFileName(rawFileNameSource));
            var instanceRawPath = candidate.Value;
            var instanceTransformedLocal = RootRelativePath.Parse(instanceRawPath.TrimEnd());
            """);
        var unrelatedScopeSourceFile = new SourceFile(
            "src/Synthetic/UnrelatedLocalScope.cs",
            """
            static void CaptureGuardedText (AbsolutePath candidate)
            {
                var transformedPath = Path.GetFileName(candidate.Value);
            }

            static void ParseUnrelatedText (string transformedPath)
            {
                _ = RootRelativePath.Parse(transformedPath);
            }
            """);
        var unrelatedFieldSourceFile = new SourceFile(
            "src/Synthetic/UnrelatedFieldScope.cs",
            """
            internal sealed class GuardedCapture
            {
                private static readonly AbsolutePath Candidate = AbsolutePath.Parse("/tmp");
                private static readonly string TransformedPath = Candidate.Value;
            }

            internal sealed class UnrelatedParser
            {
                private static void ParseUnrelatedText (string TransformedPath)
                {
                    _ = RootRelativePath.Parse(TransformedPath);
                }
            }
            """);
        var unrelatedMemberNameSourceFile = new SourceFile(
            "src/Synthetic/UnrelatedMemberName.cs",
            """
            var GetFileName = candidate.Value;
            var unrelated = "unrelated";
            _ = RootRelativePath.Parse(Path.GetFileName(unrelated));
            """);

        var matches = FindGuardedValueReentryMatches(
            [
                sourceFile,
                unrelatedScopeSourceFile,
                unrelatedFieldSourceFile,
                unrelatedMemberNameSourceFile,
            ]).ToArray();

        AssertExactMatches(
            matches,
            new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [CreateMatchKey(sourceFile.Path, "GuardedAbsolute.Parse(candidate.Value)")] = 1,
                [CreateMatchKey(sourceFile.Path, "AbsolutePath.Resolve(boundary,candidate.Value)")] = 1,
                [CreateMatchKey(sourceFile.Path, "ContainedPath.TryResolve(boundary,candidate.Value,out_,out_)")] = 1,
                [CreateMatchKey(sourceFile.Path, "Fs.AbsolutePath.Parse(candidate.Value)")] = 1,
                [CreateMatchKey(sourceFile.Path, """RootRelativePath.Parse(candidate.Value+".meta")""")] = 1,
                [CreateMatchKey(sourceFile.Path, """RootRelativePath.Parse($"{candidate.Value}.meta")""")] = 1,
                [CreateMatchKey(sourceFile.Path, "RootRelativePath.Parse(Path.GetFileName(candidate.Value))")] = 1,
                [CreateMatchKey(sourceFile.Path, "Parse(rawPath)")] = 1,
                [CreateMatchKey(sourceFile.Path, "RootRelativePath.Parse(transformedPath)")] = 1,
                [CreateMatchKey(sourceFile.Path, "RootRelativePath.Parse(Path.GetFileName(rawFileNameSource))")] = 1,
                [CreateMatchKey(sourceFile.Path, "RootRelativePath.Parse(instanceRawPath.TrimEnd())")] = 1,
            },
            "Guarded path factory re-entry detection must cover each supported syntax without crossing local lexical scopes.");
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
        foreach (var sourceFile in sourceFiles)
        {
            var root = CSharpSyntaxTree.ParseText(sourceFile.Text).GetRoot();
            var guardedPathAliases = root
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Where(static directive =>
                    directive.Alias is not null
                    && IsGuardedPathTypeName(directive.Name))
                .Select(static directive => directive.Alias!.Name.Identifier.ValueText)
                .ToHashSet(StringComparer.Ordinal);
            var allowsStaticFactoryCalls = root
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Any(static directive =>
                    !directive.StaticKeyword.IsKind(SyntaxKind.None)
                    && IsGuardedPathTypeName(directive.Name));

            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!TryGetRawPathFactoryArgument(
                        invocation,
                        guardedPathAliases,
                        allowsStaticFactoryCalls,
                        out var rawPathArgument)
                    || !ReferencesGuardedPathValue(rawPathArgument, invocation))
                {
                    continue;
                }

                var line = invocation
                    .GetLocation()
                    .GetLineSpan()
                    .StartLinePosition
                    .Line + 1;
                yield return new SourceMatch(
                    sourceFile.Path,
                    line,
                    invocation.ToString());
            }
        }
    }

    private static bool ReferencesGuardedPathValue (
        ExpressionSyntax expression,
        SyntaxNode useSite)
    {
        if (expression
            .DescendantNodesAndSelf()
            .OfType<MemberAccessExpressionSyntax>()
            .Any(static memberAccess =>
                memberAccess.Name.Identifier.ValueText == "Value"))
        {
            return true;
        }

        return expression
            .DescendantNodesAndSelf()
            .OfType<IdentifierNameSyntax>()
            .Where(IsRawPathTextOperand)
            .Select(static identifier => identifier.Identifier.ValueText)
            .Distinct(StringComparer.Ordinal)
            .Select(variableName => FindNearestVisibleAssignment(variableName, useSite))
            .Any(static assignedExpression =>
                assignedExpression is not null
                && assignedExpression
                    .DescendantNodesAndSelf()
                    .OfType<MemberAccessExpressionSyntax>()
                    .Any(static memberAccess =>
                        memberAccess.Name.Identifier.ValueText == "Value"));
    }

    private static bool IsRawPathTextOperand (IdentifierNameSyntax identifier)
    {
        return identifier.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess
                when ReferenceEquals(memberAccess.Name, identifier) => false,
            MemberAccessExpressionSyntax memberAccess
                when ReferenceEquals(memberAccess.Expression, identifier) =>
                memberAccess.Parent is InvocationExpressionSyntax invocation
                && ReferenceEquals(invocation.Expression, memberAccess),
            ElementAccessExpressionSyntax elementAccess
                when ReferenceEquals(elementAccess.Expression, identifier) => false,
            _ => true,
        };
    }

    private static ExpressionSyntax? FindNearestVisibleAssignment (
        string variableName,
        SyntaxNode useSite)
    {
        var root = useSite.SyntaxTree.GetRoot();
        var declarationAssignments = root
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(declarator =>
                declarator.Identifier.ValueText == variableName
                && declarator.Initializer is not null
                && IsLocalVariableDeclarator(declarator))
            .Select(static declarator => (
                Node: (SyntaxNode)declarator,
                Value: declarator.Initializer!.Value));
        var explicitAssignments = root
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment =>
                assignment.Left is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == variableName)
            .Select(static assignment => (
                Node: (SyntaxNode)assignment,
                Value: assignment.Right));

        return declarationAssignments
            .Concat(explicitAssignments)
            .Where(candidate =>
                candidate.Node.SpanStart < useSite.SpanStart
                && FindLexicalScope(candidate.Node) is { } scope
                && scope.FullSpan.Contains(useSite.SpanStart))
            .OrderByDescending(static candidate => candidate.Node.SpanStart)
            .Select(static candidate => candidate.Value)
            .FirstOrDefault();
    }

    private static bool IsLocalVariableDeclarator (VariableDeclaratorSyntax declarator)
    {
        return declarator.Parent?.Parent is LocalDeclarationStatementSyntax
            or ForStatementSyntax
            or UsingStatementSyntax
            or FixedStatementSyntax;
    }

    private static SyntaxNode? FindLexicalScope (SyntaxNode node)
    {
        return node
            .Ancestors()
            .FirstOrDefault(static ancestor =>
                ancestor is BlockSyntax
                    or ForStatementSyntax
                    or SwitchSectionSyntax
                    or CompilationUnitSyntax);
    }

    private static bool TryGetRawPathFactoryArgument (
        InvocationExpressionSyntax invocation,
        IReadOnlySet<string> guardedPathAliases,
        bool allowsStaticFactoryCalls,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ExpressionSyntax? rawPathArgument)
    {
        rawPathArgument = null;
        string factoryName;
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            factoryName = memberAccess.Name.Identifier.ValueText;
            var ownerName = GetRightmostIdentifier(memberAccess.Expression);
            if (ownerName is null
                || (!IsGuardedPathTypeName(ownerName)
                    && !guardedPathAliases.Contains(ownerName)))
            {
                return false;
            }
        }
        else if (invocation.Expression is IdentifierNameSyntax identifier
                 && allowsStaticFactoryCalls)
        {
            factoryName = identifier.Identifier.ValueText;
        }
        else
        {
            return false;
        }

        var argumentIndex = factoryName switch
        {
            "Parse" or "TryParse" => 0,
            "Resolve" or "TryResolve" => 1,
            _ => -1,
        };
        if (argumentIndex < 0
            || invocation.ArgumentList.Arguments.Count <= argumentIndex)
        {
            return false;
        }

        rawPathArgument = invocation.ArgumentList.Arguments[argumentIndex].Expression;
        return true;
    }

    private static bool IsGuardedPathTypeName (NameSyntax? name)
    {
        return name is not null
            && IsGuardedPathTypeName(GetRightmostIdentifier(name));
    }

    private static bool IsGuardedPathTypeName (string? name)
    {
        return name is "AbsolutePath" or "RootRelativePath" or "ContainedPath";
    }

    private static string? GetRightmostIdentifier (SyntaxNode expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
            _ => null,
        };
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
