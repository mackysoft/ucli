using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace MackySoft.Ucli.Tests.Packaging;

public sealed class PackageMetadataManifestTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public void External_filesystem_provider_source_is_not_owned_by_ucli ()
    {
        Assert.False(Directory.Exists(TestRepositoryPaths.GetFullPath("src", "MackySoft.FileSystem")));
        Assert.False(Directory.Exists(TestRepositoryPaths.GetFullPath("tests", "MackySoft.FileSystem.Tests")));
        Assert.False(File.Exists(TestRepositoryPaths.GetFullPath(
            "src",
            "MackySoft.FileSystem",
            "MackySoft.FileSystem.csproj")));

        string solution = File.ReadAllText(TestRepositoryPaths.GetFullPath("Ucli.slnx"));
        Assert.DoesNotContain("MackySoft.FileSystem", solution, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Cli_tool_project_includes_generated_schema_artifacts ()
    {
        XDocument document = XDocument.Load(TestRepositoryPaths.GetFullPath("src/Ucli/Ucli.csproj"));
        var schemaItem = document
            .Descendants("None")
            .SingleOrDefault(static element => string.Equals(
                element.Attribute("Include")?.Value,
                "../../schemas/**/*",
                StringComparison.Ordinal));

        Assert.NotNull(schemaItem);
        Assert.Equal("schemas", schemaItem!.Attribute("LinkBase")?.Value);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Agent_skills_cli_tool_manifest_pins_expected_package ()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(TestRepositoryPaths.GetFullPath(".config", "dotnet-tools.json")));
        JsonElement tool = document.RootElement
            .GetProperty("tools")
            .GetProperty("mackysoft.agentskills.cli");

        Assert.Equal("1.0.0", tool.GetProperty("version").GetString());
        Assert.Contains(
            "agent-skills",
            tool.GetProperty("commands").EnumerateArray().Select(static command => command.GetString()));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Unity_nuspec_metadata_matches_central_package_metadata ()
    {
        IReadOnlyDictionary<string, string> centralProperties = PackageMetadataTestSupport.ReadDirectoryBuildProperties();
        XNamespace nuspecNamespace = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        XDocument nuspecDocument = XDocument.Load(TestRepositoryPaths.GetFullPath("src/Ucli.Unity/MackySoft.Ucli.Unity.nuspec"));
        XElement metadata = nuspecDocument.Root?.Element(nuspecNamespace + "metadata")
            ?? throw new InvalidOperationException("Unity nuspec metadata element was not found.");

        Assert.Equal(centralProperties["Authors"], PackageMetadataTestSupport.ReadRequiredElementValue(metadata, nuspecNamespace, "authors"));
        Assert.Equal(centralProperties["Company"], PackageMetadataTestSupport.ReadRequiredElementValue(metadata, nuspecNamespace, "owners"));
        Assert.Equal("file", PackageMetadataTestSupport.ReadRequiredElement(metadata, nuspecNamespace, "license").Attribute("type")?.Value);
        Assert.Equal(centralProperties["PackageLicenseFile"], PackageMetadataTestSupport.ReadRequiredElementValue(metadata, nuspecNamespace, "license"));
        Assert.Equal(centralProperties["PackageReadmeFile"], PackageMetadataTestSupport.ReadRequiredElementValue(metadata, nuspecNamespace, "readme"));
        Assert.Equal(centralProperties["RepositoryType"], PackageMetadataTestSupport.ReadRequiredElement(metadata, nuspecNamespace, "repository").Attribute("type")?.Value);
        Assert.Equal(centralProperties["RepositoryUrl"], PackageMetadataTestSupport.ReadRequiredElement(metadata, nuspecNamespace, "repository").Attribute("url")?.Value);

        IReadOnlyDictionary<string, string> packageConfigVersions = PackageMetadataTestSupport.ReadUnityPackageConfigVersions();
        IReadOnlyDictionary<string, string> nuspecDependencyVersions = PackageMetadataTestSupport.ReadNuspecDependencyVersions(metadata, nuspecNamespace);
        Assert.Equal("0.1.0", packageConfigVersions["MackySoft.FileSystem"]);
        Assert.Equal(centralProperties["Version"], packageConfigVersions["MackySoft.Ucli.Contracts"]);
        Assert.Equal(centralProperties["Version"], packageConfigVersions["MackySoft.Ucli.Infrastructure"]);
        Assert.Equal($"[{packageConfigVersions["MackySoft.FileSystem"]}]", nuspecDependencyVersions["MackySoft.FileSystem"]);
        Assert.Equal(packageConfigVersions["MackySoft.Ucli.Contracts"], nuspecDependencyVersions["MackySoft.Ucli.Contracts"]);
        Assert.Equal(packageConfigVersions["MackySoft.Ucli.Infrastructure"], nuspecDependencyVersions["MackySoft.Ucli.Infrastructure"]);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Package_version_sync_does_not_modify_external_filesystem_dependency ()
    {
        string script = File.ReadAllText(TestRepositoryPaths.GetFullPath("scripts", "sync-package-version.sh"));

        Assert.DoesNotContain("MackySoft.FileSystem", script, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Cli_tool_redistributes_filesystem_provider_license_from_restored_package ()
    {
        XDocument cliProject = XDocument.Load(
            TestRepositoryPaths.GetFullPath("src", "Ucli", "Ucli.csproj"));
        XElement licenseItem = cliProject
            .Descendants("None")
            .Single(static element => string.Equals(
                element.Attribute("Include")?.Value,
                "$(PkgMackySoft_FileSystem)/LICENSE",
                StringComparison.Ordinal));

        Assert.Equal(
            "third-party/MackySoft.FileSystem/0.1.0/LICENSE",
            licenseItem.Attribute("Link")?.Value);
        Assert.Equal("PreserveNewest", licenseItem.Element("CopyToOutputDirectory")?.Value);
        Assert.Equal("PreserveNewest", licenseItem.Element("CopyToPublishDirectory")?.Value);

        string cliNotice = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("src", "Ucli", "THIRD-PARTY-NOTICES"));
        Assert.Contains("MackySoft.FileSystem 0.1.0", cliNotice, StringComparison.Ordinal);
        Assert.Contains(
            "third-party/MackySoft.FileSystem/0.1.0/LICENSE",
            cliNotice,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Ucli_release_tooling_does_not_own_external_filesystem_package ()
    {
        Assert.False(File.Exists(TestRepositoryPaths.GetFullPath("scripts", "prepare-local-filesystem-package.sh")));

        var workflowPaths = new[]
        {
            ".github/workflows/package-publish.yaml",
            ".github/workflows/verify.yaml",
        };
        foreach (string workflowPath in workflowPaths)
        {
            string contents = File.ReadAllText(TestRepositoryPaths.GetFullPath(workflowPath));
            Assert.DoesNotContain("dotnet pack src/MackySoft.FileSystem", contents, StringComparison.Ordinal);
            Assert.DoesNotContain("scripts/prepare-local-filesystem-package.sh", contents, StringComparison.Ordinal);
        }

        string publishWorkflow = File.ReadAllText(TestRepositoryPaths.GetFullPath(".github", "workflows", "package-publish.yaml"));
        Assert.DoesNotContain(
            publishWorkflow.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            static line => string.Equals(line.Trim(), "MackySoft.FileSystem", StringComparison.Ordinal));

        string releaseArtifacts = File.ReadAllText(TestRepositoryPaths.GetFullPath("scripts", "verify-release-package-artifacts.sh"));
        Assert.DoesNotContain("MackySoft.FileSystem.${expected_version}.nupkg", releaseArtifacts, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Local_shared_package_refresh_only_packs_ucli_owned_projects ()
    {
        string script = File.ReadAllText(TestRepositoryPaths.GetFullPath("scripts", "update-local-shared-packages.sh"));

        Assert.DoesNotContain("src/MackySoft.FileSystem", script, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(script, @"(?m)^\s*dotnet pack ").Count);
        Assert.Contains("dotnet pack \"${contracts_csproj}\"", script, StringComparison.Ordinal);
        Assert.Contains("dotnet pack \"${infrastructure_csproj}\"", script, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Prepublication_filesystem_restore_uses_only_ephemeral_provider_state ()
    {
        string refreshScript = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("scripts", "update-local-shared-packages.sh"));

        Assert.Contains("--filesystem-package-source", refreshScript, StringComparison.Ordinal);
        Assert.Contains(
            "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
            refreshScript,
            StringComparison.Ordinal);
        Assert.True(
            refreshScript.IndexOf(
                "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
                StringComparison.Ordinal)
            < refreshScript.IndexOf("filesystem_package_source=\"$2\"", StringComparison.Ordinal),
            "An explicit filesystem package source must override the environment fallback.");
        Assert.Contains("prepublication_restore_root=\"$(mktemp -d", refreshScript, StringComparison.Ordinal);
        Assert.Contains("isolated_filesystem_package_source", refreshScript, StringComparison.Ordinal);
        Assert.Contains("isolated_nuget_packages", refreshScript, StringComparison.Ordinal);
        Assert.Contains("--no-cache", refreshScript, StringComparison.Ordinal);
        Assert.Contains("--force-evaluate", refreshScript, StringComparison.Ordinal);
        Assert.Contains("-NoCache", refreshScript, StringComparison.Ordinal);
        Assert.Contains("trap cleanup EXIT", refreshScript, StringComparison.Ordinal);
        Assert.Contains("<packageSource key=\"FileSystemCandidate\">", refreshScript, StringComparison.Ordinal);
        Assert.DoesNotContain("<package pattern=\"*\" />", refreshScript, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "cp \"${external_filesystem_package}\" \"${repository_local_package_source}",
            refreshScript,
            StringComparison.Ordinal);

        string repositoryVerificationScript = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("scripts", "verify.sh"));
        Assert.Contains(
            "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
            repositoryVerificationScript,
            StringComparison.Ordinal);
        Assert.True(
            repositoryVerificationScript.IndexOf(
                "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
                StringComparison.Ordinal)
            < repositoryVerificationScript.IndexOf("filesystem_package_source=\"$2\"", StringComparison.Ordinal),
            "verify.sh must prefer an explicit filesystem package source.");
        Assert.Contains("--filesystem-package-source)", repositoryVerificationScript, StringComparison.Ordinal);
        Assert.Contains(
            "test_unity_args+=(--filesystem-package-source \"$filesystem_package_source\")",
            repositoryVerificationScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "--filesystem-package-source requires restore; remove --no-restore.",
            repositoryVerificationScript,
            StringComparison.Ordinal);
        Assert.Contains("<packageSource key=\"FileSystemCandidate\">", repositoryVerificationScript, StringComparison.Ordinal);
        Assert.Contains("--no-cache", repositoryVerificationScript, StringComparison.Ordinal);
        Assert.Contains("--force-evaluate", repositoryVerificationScript, StringComparison.Ordinal);
        Assert.Contains("cmp -s", repositoryVerificationScript, StringComparison.Ordinal);
        Assert.Contains("dotnet pack src/Ucli/Ucli.csproj", repositoryVerificationScript, StringComparison.Ordinal);
        Assert.Contains("bash scripts/verify-cli-package.sh", repositoryVerificationScript, StringComparison.Ordinal);
        Assert.DoesNotContain("<package pattern=\"*\" />", repositoryVerificationScript, StringComparison.Ordinal);

        foreach (string entryPoint in new[]
        {
            "scripts/test-unity.sh",
            "scripts/run-ucli-unity-build.sh",
        })
        {
            string script = File.ReadAllText(TestRepositoryPaths.GetFullPath(entryPoint));
            Assert.Contains("--filesystem-package-source", script, StringComparison.Ordinal);
            Assert.Contains(
                "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
                script,
                StringComparison.Ordinal);
            Assert.True(
                script.IndexOf(
                    "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
                    StringComparison.Ordinal)
                < script.IndexOf("filesystem_package_source=\"$2\"", StringComparison.Ordinal),
                $"{entryPoint} must prefer an explicit filesystem package source.");
            Assert.Contains("--configfile", script, StringComparison.Ordinal);
            Assert.Contains("--no-cache", script, StringComparison.Ordinal);
            Assert.Contains("--force-evaluate", script, StringComparison.Ordinal);
            Assert.Contains("cmp -s", script, StringComparison.Ordinal);
            Assert.DoesNotContain("<package pattern=\"*\" />", script, StringComparison.Ordinal);
            Assert.Single(
                Regex
                    .Matches(script, "<package pattern=\"MackySoft.FileSystem\" />")
                    .Cast<Match>());
            Assert.Contains(
                "unity_package_restore_args+=(--filesystem-package-source",
                script,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"${repository_root}/src/Ucli.Unity/Assets/Packages\"",
                script,
                StringComparison.Ordinal);
        }

        string verificationScript = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("scripts", "verify-shared-packages.sh"));
        Assert.Contains(
            "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
            verificationScript,
            StringComparison.Ordinal);
        Assert.True(
            verificationScript.IndexOf(
                "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
                StringComparison.Ordinal)
            < verificationScript.IndexOf("filesystem_package_source=\"$2\"", StringComparison.Ordinal),
            "verify-shared-packages.sh must prefer an explicit filesystem package source.");
        Assert.Contains("<packageSource key=\"FileSystemCandidate\">", verificationScript, StringComparison.Ordinal);
        Assert.Contains("--configfile", verificationScript, StringComparison.Ordinal);
        Assert.Contains("--no-cache", verificationScript, StringComparison.Ordinal);
        Assert.Contains("--force-evaluate", verificationScript, StringComparison.Ordinal);
        Assert.Contains("consumer_nuget_http_cache", verificationScript, StringComparison.Ordinal);
        Assert.Contains("cmp -s", verificationScript, StringComparison.Ordinal);
        Assert.Contains(".nupkg.metadata", verificationScript, StringComparison.Ordinal);
        Assert.Contains("EXPECTED_FILESYSTEM_SOURCE", verificationScript, StringComparison.Ordinal);
        Assert.Contains("public_filesystem_package_mapping=\"\"", verificationScript, StringComparison.Ordinal);
        Assert.DoesNotContain("<package pattern=\"*\" />", verificationScript, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "--source \"${filesystem_package_source}\"",
            verificationScript,
            StringComparison.Ordinal);

        string unityVerificationScript = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("scripts", "verify-unity-plugin-package.sh"));
        Assert.Contains("--filesystem-package-source", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains(
            "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
            unityVerificationScript,
            StringComparison.Ordinal);
        Assert.True(
            unityVerificationScript.IndexOf(
                "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
                StringComparison.Ordinal)
            < unityVerificationScript.IndexOf("filesystem_package_source=\"$2\"", StringComparison.Ordinal),
            "verify-unity-plugin-package.sh must prefer an explicit filesystem package source.");
        Assert.Contains("<packageSource key=\"FileSystemCandidate\">", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("<packageSource key=\"UnityPackage\">", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("<packageSource key=\"UcliPackages\">", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("isolated_nuget_packages", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("isolated_nuget_http_cache", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("NUGET_PACKAGES=\"${isolated_nuget_packages}\"", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("nuget install \"${package_id}\"", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("-Framework netstandard2.1", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("-ConfigFile", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("-NoCache", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("-DirectDownload", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("cmp -s", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains(".nupkg.metadata", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("EXPECTED_FILESYSTEM_SOURCE", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("ucli_dependency_package_versions=()", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("restored_plugin_root", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains("restored_filesystem_root", unityVerificationScript, StringComparison.Ordinal);
        Assert.Contains(
            "(.references | index($dependency_assembly) != null)",
            unityVerificationScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "lib/netstandard2.1/${filesystem_package_id}.dll",
            unityVerificationScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"${filesystem_package_id}.dll\"",
            unityVerificationScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"${filesystem_package_id}.*.nupkg\"",
            unityVerificationScript,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<package pattern=\"*\" />", unityVerificationScript, StringComparison.Ordinal);

        string cliVerificationScript = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("scripts", "verify-cli-package.sh"));
        Assert.Contains("--filesystem-package-source", cliVerificationScript, StringComparison.Ordinal);
        Assert.Contains(
            "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
            cliVerificationScript,
            StringComparison.Ordinal);
        Assert.True(
            cliVerificationScript.IndexOf(
                "filesystem_package_source=\"${FILESYSTEM_PACKAGE_SOURCE:-}\"",
                StringComparison.Ordinal)
            < cliVerificationScript.IndexOf("filesystem_package_source=\"$2\"", StringComparison.Ordinal),
            "verify-cli-package.sh must prefer an explicit filesystem package source.");
        Assert.Contains("<packageSource key=\"FileSystemCandidate\">", cliVerificationScript, StringComparison.Ordinal);
        Assert.Contains("tool_packages_root", cliVerificationScript, StringComparison.Ordinal);
        Assert.Contains("tool_http_cache", cliVerificationScript, StringComparison.Ordinal);
        Assert.Contains("--no-cache", cliVerificationScript, StringComparison.Ordinal);
        Assert.Contains("--force-evaluate", cliVerificationScript, StringComparison.Ordinal);
        Assert.Contains(".nupkg.metadata", cliVerificationScript, StringComparison.Ordinal);
        Assert.Contains("project.assets.json", cliVerificationScript, StringComparison.Ordinal);
        Assert.Contains("dotnet publish", cliVerificationScript, StringComparison.Ordinal);
        Assert.Contains("cmp -s", cliVerificationScript, StringComparison.Ordinal);
        Assert.Contains(
            "tools/net8.0/any/${filesystem_package_id}.dll",
            cliVerificationScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "third-party/${filesystem_package_id}/${filesystem_package_version}/LICENSE",
            cliVerificationScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "${filesystem_package_id}.*.nupkg",
            cliVerificationScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "find \"${publish_path}\" -type f -iname \"${filesystem_package_id}.*.nupkg\"",
            cliVerificationScript,
            StringComparison.Ordinal);
        Assert.DoesNotContain("<package pattern=\"*\" />", cliVerificationScript, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Default_restore_uses_repository_local_packages_only_when_the_source_exists ()
    {
        string dotnetCommon = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("scripts", "dotnet-common.sh"));
        Assert.Contains(
            "if [ -d \"${DOTNET_REPO_ROOT}/src/Ucli.Unity/Packages/nuget-local-source\" ]; then",
            dotnetCommon,
            StringComparison.Ordinal);
        Assert.Contains(
            "local additional_sources=\"${RestoreAdditionalProjectSources:-}\"",
            dotnetCommon,
            StringComparison.Ordinal);
        Assert.Contains(
            "if [ -n \"${additional_sources}\" ]; then",
            dotnetCommon,
            StringComparison.Ordinal);
        Assert.Contains(
            "additional_sources=\"${additional_sources};${DOTNET_LOCAL_PACKAGE_SOURCE}\"",
            dotnetCommon,
            StringComparison.Ordinal);
        Assert.True(
            dotnetCommon.IndexOf(
                "if [ -d \"${DOTNET_REPO_ROOT}/src/Ucli.Unity/Packages/nuget-local-source\" ]; then",
                StringComparison.Ordinal)
            < dotnetCommon.IndexOf(
                "additional_sources=\"${additional_sources};${DOTNET_LOCAL_PACKAGE_SOURCE}\"",
                StringComparison.Ordinal),
            "The shared restore helper must test the generated source before adding it.");

        foreach (string entryPoint in new[]
        {
            "scripts/test-unity.sh",
            "scripts/run-ucli-unity-build.sh",
        })
        {
            string script = File.ReadAllText(TestRepositoryPaths.GetFullPath(entryPoint));
            Assert.Contains(
                "if [[ -d \"${dotnet_local_package_source}\" ]]; then",
                script,
                StringComparison.Ordinal);
            Assert.Contains(
                "dotnet_additional_package_sources=\"${RestoreAdditionalProjectSources:-}\"",
                script,
                StringComparison.Ordinal);
            Assert.Contains(
                "dotnet_additional_package_sources=\"${dotnet_additional_package_sources};${dotnet_local_package_source}\"",
                script,
                StringComparison.Ordinal);
            Assert.Contains(
                "\"-p:RestoreAdditionalProjectSources=${dotnet_additional_package_sources}\"",
                script,
                StringComparison.Ordinal);
            Assert.True(
                script.IndexOf(
                    "if [[ -d \"${dotnet_local_package_source}\" ]]; then",
                    StringComparison.Ordinal)
                < script.IndexOf(
                    "dotnet_additional_package_sources=\"${dotnet_additional_package_sources};${dotnet_local_package_source}\"",
                    StringComparison.Ordinal),
                $"{entryPoint} must test the generated source before adding it.");
        }

        foreach (string workflowPath in new[]
        {
            ".github/workflows/verify.yaml",
            ".github/workflows/package-publish.yaml",
        })
        {
            string workflow = File.ReadAllText(TestRepositoryPaths.GetFullPath(workflowPath));
            Assert.DoesNotContain("nuget-local-source", workflow, StringComparison.Ordinal);
            Assert.DoesNotContain("RestoreAdditionalProjectSources", workflow, StringComparison.Ordinal);
        }

        string refreshScript = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("scripts", "update-local-shared-packages.sh"));
        Assert.Contains(
            "mkdir -p \"${repository_local_package_source}\"",
            refreshScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"-p:RestoreAdditionalProjectSources=${dotnet_local_package_source}\"",
            refreshScript,
            StringComparison.Ordinal);
        Assert.True(
            refreshScript.IndexOf(
                "mkdir -p \"${repository_local_package_source}\"",
                StringComparison.Ordinal)
            < refreshScript.IndexOf(
                "\"-p:RestoreAdditionalProjectSources=${dotnet_local_package_source}\"",
                StringComparison.Ordinal),
            "The package refresh script must materialize its owned source before using it.");
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Prepublication_package_verifiers_receive_additive_external_dependency_arguments ()
    {
        string workflow = File.ReadAllText(
            TestRepositoryPaths.GetFullPath(".github", "workflows", "verify.yaml"));

        Assert.Equal(5, Regex.Matches(
            workflow,
            @"(?m)^\s*external_dependency_args=\(\)$").Count);
        Assert.Equal(5, Regex.Matches(
            workflow,
            @"(?m)^\s*external_dependency_args\+=\(--filesystem-package-source ""\$\{FILESYSTEM_PACKAGE_SOURCE\}""\)$").Count);

        foreach (string verifier in new[]
        {
            "verify-shared-packages.sh",
            "verify-unity-plugin-package.sh",
            "verify-cli-package.sh",
        })
        {
            int invocationCount = Regex.Matches(
                workflow,
                $@"\./scripts/{Regex.Escape(verifier)}").Count;
            int forwardedInvocationCount = Regex.Matches(
                workflow,
                $@"(?s)\./scripts/{Regex.Escape(verifier)}.*?""\$\{{external_dependency_args\[@\]\}}""").Count;

            Assert.Equal(2, invocationCount);
            Assert.Equal(invocationCount, forwardedInvocationCount);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Unity_pack_materializes_declared_ucli_dependencies_only_as_verification_inputs ()
    {
        string workflow = File.ReadAllText(
            TestRepositoryPaths.GetFullPath(".github", "workflows", "verify.yaml"));
        Match unityPackJob = Regex.Match(
            workflow,
            @"(?ms)^  unity_pack:\s*(?<body>.*?)(?=^  release_pack:)");

        Assert.True(unityPackJob.Success, "verify.yaml must declare the unity_pack job.");
        string job = unityPackJob.Groups["body"].Value;
        Assert.Contains(
            "./scripts/update-local-shared-packages.sh",
            job,
            StringComparison.Ordinal);
        Assert.Contains(
            "--shared-package-output artifacts/packages",
            job,
            StringComparison.Ordinal);
        Assert.Contains(
            "path: artifacts/packages/MackySoft.Ucli.Unity.0.0.0-ci.nupkg",
            job,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            job.Split('\n', StringSplitOptions.RemoveEmptyEntries),
            static line => string.Equals(line.Trim(), "path: artifacts/packages", StringComparison.Ordinal));

        string dependencyPackScript = File.ReadAllText(
            TestRepositoryPaths.GetFullPath("scripts", "update-local-shared-packages.sh"));
        Assert.Contains(
            "contracts_package_version=\"$(read_package_version \"${contracts_package_id}\")\"",
            dependencyPackScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "infrastructure_package_version=\"$(read_package_version \"${infrastructure_package_id}\")\"",
            dependencyPackScript,
            StringComparison.Ordinal);
        Assert.Contains("--shared-package-output", dependencyPackScript, StringComparison.Ordinal);
        Assert.Contains(
            "${local_package_source}/${contracts_package_id}.${contracts_package_version}.nupkg",
            dependencyPackScript,
            StringComparison.Ordinal);
        Assert.Contains(
            "${local_package_source}/${infrastructure_package_id}.${infrastructure_package_version}.nupkg",
            dependencyPackScript,
            StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Distribution_workflows_preserve_both_shared_and_unity_dependency_verification ()
    {
        foreach (string workflowPath in new[]
        {
            ".github/workflows/verify.yaml",
            ".github/workflows/package-publish.yaml",
        })
        {
            string workflow = File.ReadAllText(TestRepositoryPaths.GetFullPath(workflowPath));
            Assert.Contains("./scripts/verify-shared-packages.sh", workflow, StringComparison.Ordinal);
            Assert.Contains("./scripts/verify-unity-plugin-package.sh", workflow, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Size", "Medium")]
    public void Shared_package_verification_requires_only_ucli_owned_artifacts ()
    {
        string script = File.ReadAllText(TestRepositoryPaths.GetFullPath("scripts", "verify-shared-packages.sh"));
        Match packageIdsMatch = Regex.Match(
            script,
            @"(?ms)^package_ids=\(\s*(?<body>.*?)^\)");

        Assert.True(packageIdsMatch.Success, "verify-shared-packages.sh must declare package_ids.");
        string[] packageIds = Regex
            .Matches(packageIdsMatch.Groups["body"].Value, "\"(?<id>[^\"]+)\"")
            .Cast<Match>()
            .Select(static match => match.Groups["id"].Value)
            .ToArray();
        Assert.Equal(
            new[]
            {
                "MackySoft.Ucli.Contracts",
                "MackySoft.Ucli.Infrastructure",
            },
            packageIds);
    }
}
