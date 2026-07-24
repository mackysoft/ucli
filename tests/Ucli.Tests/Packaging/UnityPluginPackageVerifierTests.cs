namespace MackySoft.Ucli.Tests.Packaging;

public sealed class UnityPluginPackageVerifierTests
{
    private const string PackageVersion = "0.0.0";

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Verifier_AcceptsExactExternalVocabularyRangesWithoutChangingOtherDependencyVersions ()
    {
        using var scope = TestDirectories.CreateTempScope(
            "unity-plugin-package-verifier",
            "exact-external-vocabulary-ranges");
        string packageDirectoryPath = scope.CreateDirectory("packages");
        scope.WriteFile(
            Path.Combine("packages", $"MackySoft.Ucli.Unity.{PackageVersion}.nupkg"),
            string.Empty);

        string toolDirectoryPath = scope.CreateDirectory("tools");
        string unzipPath = scope.WriteFile(
            Path.Combine("tools", "unzip"),
            NormalizeBashScript(
                """
                #!/usr/bin/env bash
                set -euo pipefail

                case "${1:-}" in
                  -Z1)
                    cat <<'ENTRIES'
                MackySoft.Ucli.Unity.nuspec
                ucli-plugin.json
                Editor/MackySoft.Ucli.Unity.Editor.asmdef
                Editor/csc.rsp
                Editor/csc.rsp.meta
                Editor/AssemblyInfo.cs
                Editor/Ipc/Bootstrap/UnityDaemonBootstrap.cs
                Editor/Execution/UnityExecutionServiceCollectionExtensions.cs
                README.md
                LICENSE
                ENTRIES
                    ;;
                  -p)
                    cat "${UCLI_TEST_UNITY_NUSPEC_PATH}"
                    ;;
                  *)
                    exit 2
                    ;;
                esac
                """));
        string nugetPath = scope.WriteFile(
            Path.Combine("tools", "nuget"),
            NormalizeBashScript(
                """
                #!/usr/bin/env bash
                set -euo pipefail

                [[ "${1:-}" == "restore" ]]
                shift
                packages_directory=""
                while [[ "$#" -gt 0 ]]; do
                  case "$1" in
                    -PackagesDirectory)
                      packages_directory="$2"
                      shift 2
                      ;;
                    *)
                      shift
                      ;;
                  esac
                done

                [[ -n "${packages_directory}" ]]
                marker_directory="${packages_directory}/MackySoft.Ucli.Unity.${UCLI_TEST_PACKAGE_VERSION}"
                mkdir -p "${marker_directory}"
                : > "${marker_directory}/ucli-plugin.json"
                """));

        string runnerPath = scope.WriteFile(
            "run-verifier.sh",
            NormalizeBashScript(
                $$"""
                #!/usr/bin/env bash
                set -euo pipefail

                chmod +x {{TestShellPaths.QuoteBashArgument(TestShellPaths.ToBashPath(unzipPath))}}
                chmod +x {{TestShellPaths.QuoteBashArgument(TestShellPaths.ToBashPath(nugetPath))}}
                export PATH={{TestShellPaths.QuoteBashArgument(TestShellPaths.ToBashPath(toolDirectoryPath))}}:"${PATH}"
                export UCLI_TEST_PACKAGE_VERSION={{TestShellPaths.QuoteBashArgument(PackageVersion)}}
                export UCLI_TEST_UNITY_NUSPEC_PATH={{TestShellPaths.QuoteBashArgument(TestShellPaths.ToBashPath(
                    TestRepositoryPaths.GetFullPath("src", "Ucli.Unity", "MackySoft.Ucli.Unity.nuspec")))}}

                exec bash \
                  {{TestShellPaths.QuoteBashArgument(TestShellPaths.ToBashPath(
                      TestRepositoryPaths.GetFullPath("scripts", "verify-unity-plugin-package.sh")))}} \
                  {{TestShellPaths.QuoteBashArgument(TestShellPaths.ToBashPath(packageDirectoryPath))}} \
                  {{TestShellPaths.QuoteBashArgument(PackageVersion)}}
                """));

        await TestProcessRunner.RunRequiredAsync(
            TestShellPaths.ResolveBashFileName(),
            [TestShellPaths.ToBashPath(runnerPath)],
            scope.FullPath,
            timeout: TimeSpan.FromSeconds(10));
    }

    private static string NormalizeBashScript (string script)
    {
        return script.ReplaceLineEndings("\n");
    }
}
