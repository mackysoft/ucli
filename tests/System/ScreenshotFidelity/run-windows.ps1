[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [Alias("unity-editor")]
    [ValidateNotNullOrEmpty()]
    [string] $UnityEditor,

    [Alias("results-dir")]
    [string] $ResultsDirectory,

    [Alias("keep-work-directory")]
    [switch] $KeepWorkDirectory
)

Set-StrictMode -Version 3.0
$ErrorActionPreference = "Stop"

$expectedUnityVersion = "6000.3.11f1"
$expectedUnityRevision = "3000ef702840"
$expectedGraphicsDeviceType = "Direct3D12"
$expectedColorSpace = "Linear"
$expectedRenderPipelineType = "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset"
$expectedRenderPipelinePackageName = "com.unity.render-pipelines.universal"
$expectedRenderPipelinePackageVersion = "17.3.0"
$expectedUrpMaterialVersion = 10
$utf8WithoutBom = New-Object System.Text.UTF8Encoding($false)

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [AllowNull()]
        [object] $Value
    )

    $json = $Value | ConvertTo-Json -Depth 32
    [System.IO.File]::WriteAllText($Path, $json + [Environment]::NewLine, $utf8WithoutBom)
}

function Read-JsonFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "JSON file does not exist: $Path"
    }

    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
}

function Read-CaptureSurface {
    param(
        [Parameter(Mandatory = $true)]
        [string] $MetadataPath,

        [Parameter(Mandatory = $true)]
        [int] $ExpectedProcessId,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedWindowTitle
    )

    $metadata = Read-JsonFile -Path $MetadataPath
    if (($metadata.schemaVersion -ne 2) -or
        ([int] $metadata.processId -ne $ExpectedProcessId) -or
        ([string] $metadata.windowTitle -ne $ExpectedWindowTitle)) {
        throw "Capture metadata does not identify the expected Unity GameView. See $MetadataPath."
    }

    return [pscustomobject]([ordered]@{
        processId = [int] $metadata.processId
        windowTitle = [string] $metadata.windowTitle
        windowHandle = [string] $metadata.windowHandle
        clientWidth = [int] $metadata.clientBounds.width
        clientHeight = [int] $metadata.clientBounds.height
        captureWidth = [int] $metadata.captureBounds.width
        captureHeight = [int] $metadata.captureBounds.height
        clientCropX = [int] $metadata.clientCrop.x
        clientCropY = [int] $metadata.clientCrop.y
        clientCropWidth = [int] $metadata.clientCrop.width
        clientCropHeight = [int] $metadata.clientCrop.height
        captureMethod = [string] $metadata.captureMethod
    })
}

function Assert-SameCaptureSurface {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Expected,

        [Parameter(Mandatory = $true)]
        [object] $Actual,

        [Parameter(Mandatory = $true)]
        [string] $Context
    )

    foreach ($propertyName in @(
        "processId",
        "windowTitle",
        "windowHandle",
        "clientWidth",
        "clientHeight",
        "captureWidth",
        "captureHeight",
        "clientCropX",
        "clientCropY",
        "clientCropWidth",
        "clientCropHeight",
        "captureMethod")) {
        if ($Expected.$propertyName -ne $Actual.$propertyName) {
            throw "$Context changed '$propertyName' from '$($Expected.$propertyName)' to '$($Actual.$propertyName)'."
        }
    }
}

function Invoke-NativeTool {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Executable,

        [Parameter(Mandatory = $true)]
        [string[]] $ArgumentList,

        [Parameter(Mandatory = $true)]
        [string] $StandardOutputPath,

        [Parameter(Mandatory = $true)]
        [string] $StandardErrorPath
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell 5.1 can promote redirected native stderr lines to
        # ErrorRecord instances. The process exit code remains authoritative.
        $ErrorActionPreference = "Continue"
        & $Executable @ArgumentList 1> $StandardOutputPath 2> $StandardErrorPath
        return [int] $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
}

function Invoke-NativeText {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Executable,

        [Parameter(Mandatory = $true)]
        [string[]] $ArgumentList
    )

    $output = @(& $Executable @ArgumentList 2>&1)
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        $message = ($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine
        throw "Command failed with exit $exitCode`: $Executable $($ArgumentList -join ' ')$([Environment]::NewLine)$message"
    }

    return (($output | ForEach-Object { $_.ToString() }) -join [Environment]::NewLine).Trim()
}

function Test-UnityRunning {
    if ($null -eq $script:unityProcess) {
        return $false
    }

    $script:unityProcess.Refresh()
    return -not $script:unityProcess.HasExited
}

function Wait-ForFile {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path,

        [Parameter(Mandatory = $true)]
        [int] $TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        if (Test-Path -LiteralPath $Path -PathType Leaf) {
            $item = Get-Item -LiteralPath $Path
            if ($item.Length -gt 0) {
                return
            }
        }

        if (($null -ne $script:unityProcess) -and -not (Test-UnityRunning)) {
            throw "Unity exited while waiting for $Path. See $script:unityLogPath."
        }

        Start-Sleep -Milliseconds 100
    }

    throw "Timed out waiting for $Path. See $script:unityLogPath."
}

function Wait-ForGuiSession {
    param(
        [Parameter(Mandatory = $true)]
        [int] $ExpectedProcessId,

        [Parameter(Mandatory = $true)]
        [int] $TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTime]::UtcNow -lt $deadline) {
        $matches = @()
        $sessionRoot = Join-Path $script:testRepository ".ucli\local\projects"
        if (Test-Path -LiteralPath $sessionRoot -PathType Container) {
            foreach ($sessionPath in @(Get-ChildItem -LiteralPath $sessionRoot -Filter "session.json" -File -Recurse -ErrorAction SilentlyContinue)) {
                try {
                    $session = Read-JsonFile -Path $sessionPath.FullName
                    if (($session.editorMode -eq "gui") -and ([int] $session.processId -eq $ExpectedProcessId)) {
                        $matches += $sessionPath.FullName
                    }
                }
                catch {
                    # A session file can be observed between its creation and atomic replacement.
                }
            }
        }

        if ($matches.Count -eq 1) {
            $script:guiSessionPath = $matches[0]
            return
        }

        if ($matches.Count -gt 1) {
            throw "Multiple live uCLI GUI sessions were registered for Unity process $ExpectedProcessId."
        }

        if (-not (Test-UnityRunning)) {
            throw "Unity exited before registering its uCLI GUI session. See $script:unityLogPath."
        }

        Start-Sleep -Milliseconds 100
    }

    throw "Timed out waiting for the Unity uCLI GUI session. See $script:unityLogPath."
}

function Invoke-Ucli {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ResultPath,

        [Parameter(Mandatory = $true)]
        [string[]] $ArgumentList
    )

    $stderrPath = [System.IO.Path]::ChangeExtension($ResultPath, ".stderr.log")
    $exitCode = Invoke-NativeTool `
        -Executable $script:ucliExecutable `
        -ArgumentList $ArgumentList `
        -StandardOutputPath $ResultPath `
        -StandardErrorPath $stderrPath
    if ($exitCode -ne 0) {
        throw "uCLI command failed with exit $exitCode. See $ResultPath and $stderrPath."
    }

    $result = Read-JsonFile -Path $ResultPath
    if ($result.status -ne "ok") {
        throw "uCLI command did not return status=ok. See $ResultPath."
    }

    return $result
}

function Start-Fixture {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ResultPath
    )

    Remove-Item -LiteralPath $script:fixtureReadyPath -Force -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath (Join-Path $script:runDirectory "control.json") -Force -ErrorAction SilentlyContinue

    $encodedRunDirectory = $script:runDirectory | ConvertTo-Json -Compress
    $source = "return MackySoft.Ucli.ScreenshotFidelity.ScreenshotFidelityFixture.Start($encodedRunDirectory);"
    $sourcePath = Join-Path $script:runDirectory "fixture-start.cs"
    [System.IO.File]::WriteAllText(
        $sourcePath,
        $source + [Environment]::NewLine,
        $utf8WithoutBom)
    $arguments = @(
        "eval",
        "--projectPath", $script:unityProject,
        "--mode", "daemon",
        "--allowDangerous",
        "--allowPlayMode",
        "--file", $sourcePath,
        "--timeout", "30000"
    )
    $result = Invoke-Ucli -ResultPath $ResultPath -ArgumentList $arguments
    $opResults = @($result.payload.opResults)
    if (($opResults.Count -ne 1) -or
        ($opResults[0].op -ne "ucli.cs.eval") -or
        ($opResults[0].result.compile.status -ne "succeeded") -or
        ($opResults[0].result.returnValue.kind -ne "json") -or
        ($opResults[0].result.returnValue.value -ne $true)) {
        throw "Screenshot fidelity fixture did not report a successful start. See $ResultPath."
    }

    Wait-ForFile -Path $script:fixtureReadyPath -TimeoutSeconds 60
    $ready = Read-JsonFile -Path $script:fixtureReadyPath
    if (($ready.status -ne "ready") -or ([int] $ready.processId -ne $script:unityProcess.Id)) {
        throw "Screenshot fidelity fixture started in an unexpected Unity process. See $script:fixtureReadyPath."
    }
}

function Send-FixtureControl {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Action,

        [Parameter(Mandatory = $true)]
        [string] $Nonce
    )

    $sequence = $script:nextSequence
    $script:nextSequence++
    $responsePath = Join-Path (Join-Path $script:runDirectory "responses") ("{0:D4}.json" -f $sequence)
    $controlPath = Join-Path $script:runDirectory "control.json"
    $temporaryPath = "$controlPath.tmp"
    Write-JsonFile -Path $temporaryPath -Value ([ordered]@{
        sequence = $sequence
        action = $Action
        nonce = $Nonce
    })
    Move-Item -LiteralPath $temporaryPath -Destination $controlPath -Force

    Wait-ForFile -Path $responsePath -TimeoutSeconds 60
    $response = Read-JsonFile -Path $responsePath
    if (($response.status -ne "ready") -or
        ([int] $response.sequence -ne $sequence) -or
        ($response.action -ne $Action) -or
        ($response.nonce -ne $Nonce)) {
        throw "Unity fixture action failed: $Action. See $responsePath."
    }

    if ([int] $response.processId -ne $script:unityProcess.Id) {
        throw "Unity fixture action $Action returned an unexpected process ID. See $responsePath."
    }

    if ([string]::IsNullOrWhiteSpace([string] $response.windowTitle)) {
        throw "Unity fixture action $Action did not return a window title. See $responsePath."
    }

    return [pscustomobject]@{
        Path = $responsePath
        Value = $response
    }
}

function Copy-UcliArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [object] $CommandResult,

        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    $relativePath = [string] $CommandResult.payload.artifact.path
    if ([string]::IsNullOrWhiteSpace($relativePath) -or [System.IO.Path]::IsPathRooted($relativePath)) {
        throw "Screenshot artifact path must be a non-empty repository-relative path."
    }

    $repositoryPrefix = [System.IO.Path]::GetFullPath($script:testRepository).TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    $sourcePath = [System.IO.Path]::GetFullPath((Join-Path $script:testRepository $relativePath))
    if (-not $sourcePath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Screenshot artifact path escapes the disposable repository: $relativePath"
    }

    if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
        throw "Screenshot artifact does not exist: $sourcePath"
    }

    Copy-Item -LiteralPath $sourcePath -Destination $Destination
}

function Invoke-OracleCapture {
    param(
        [Parameter(Mandatory = $true)]
        [int] $ProcessId,

        [Parameter(Mandatory = $true)]
        [string] $WindowTitle,

        [Parameter(Mandatory = $true)]
        [string] $OutputPath,

        [Parameter(Mandatory = $true)]
        [string] $MetadataPath
    )

    $stdoutPath = [System.IO.Path]::ChangeExtension($MetadataPath, ".stdout.log")
    $stderrPath = [System.IO.Path]::ChangeExtension($MetadataPath, ".stderr.log")
    for ($attempt = 1; $attempt -le 25; $attempt++) {
        Remove-Item -LiteralPath $OutputPath, $MetadataPath -Force -ErrorAction SilentlyContinue
        $exitCode = Invoke-NativeTool `
            -Executable $script:oracleExecutable `
            -ArgumentList @(
                "capture-window",
                "--process-id", $ProcessId.ToString([Globalization.CultureInfo]::InvariantCulture),
                "--window-title", $WindowTitle,
                "--output", $OutputPath,
                "--metadata", $MetadataPath
            ) `
            -StandardOutputPath $stdoutPath `
            -StandardErrorPath $stderrPath
        if (($exitCode -eq 0) -and
            (Test-Path -LiteralPath $OutputPath -PathType Leaf) -and
            (Test-Path -LiteralPath $MetadataPath -PathType Leaf)) {
            return
        }

        if (-not (Test-UnityRunning)) {
            throw "Unity exited while waiting for the on-screen GameView capture. See $stderrPath."
        }

        Start-Sleep -Milliseconds 200
    }

    throw "The Windows oracle could not capture the exact on-screen Unity window '$WindowTitle' for process $ProcessId. See $stderrPath."
}

function Invoke-OracleAnalysis {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Command,

        [Parameter(Mandatory = $true)]
        [string[]] $ArgumentList,

        [Parameter(Mandatory = $true)]
        [string] $OutputPath
    )

    $stdoutPath = [System.IO.Path]::ChangeExtension($OutputPath, ".stdout.log")
    $stderrPath = [System.IO.Path]::ChangeExtension($OutputPath, ".stderr.log")
    $exitCode = Invoke-NativeTool `
        -Executable $script:oracleExecutable `
        -ArgumentList (@($Command) + $ArgumentList + @("--output", $OutputPath)) `
        -StandardOutputPath $stdoutPath `
        -StandardErrorPath $stderrPath
    if (-not (Test-Path -LiteralPath $OutputPath -PathType Leaf)) {
        throw "Windows oracle did not write its analysis result. See $stdoutPath and $stderrPath."
    }

    $analysis = Read-JsonFile -Path $OutputPath
    if (($exitCode -ne 0) -or ($analysis.passed -ne $true)) {
        throw "Windows oracle analysis '$Command' failed. See $OutputPath."
    }

    return $analysis
}

function Run-GameVariant {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Name,

        [Parameter(Mandatory = $true)]
        [string] $Action
    )

    $directory = Join-Path $script:caseDirectory $Name
    New-Item -ItemType Directory -Path $directory | Out-Null
    $fixture = Send-FixtureControl -Action $Action -Nonce $Name
    Copy-Item -LiteralPath $fixture.Path -Destination (Join-Path $directory "fixture.json")

    $processId = [int] $fixture.Value.processId
    $windowTitle = [string] $fixture.Value.windowTitle

    # The references and public command are intentionally adjacent. No fixture repaint is
    # allowed to manufacture a fresh frame around the public capture.
    Invoke-OracleCapture `
        -ProcessId $processId `
        -WindowTitle $windowTitle `
        -OutputPath (Join-Path $directory "os-before.png") `
        -MetadataPath (Join-Path $directory "os-before.json")
    $commandPath = Join-Path $directory "command.json"
    $commandErrorPath = Join-Path $directory "command.stderr.log"
    $commandExitCode = Invoke-NativeTool `
        -Executable $script:ucliExecutable `
        -ArgumentList @(
            "screenshot", "game",
            "--projectPath", $script:unityProject,
            "--timeout", "30000"
        ) `
        -StandardOutputPath $commandPath `
        -StandardErrorPath $commandErrorPath
    Invoke-OracleCapture `
        -ProcessId $processId `
        -WindowTitle $windowTitle `
        -OutputPath (Join-Path $directory "os-after.png") `
        -MetadataPath (Join-Path $directory "os-after.json")
    Invoke-OracleCapture `
        -ProcessId $processId `
        -WindowTitle $windowTitle `
        -OutputPath (Join-Path $directory "os-confirmation.png") `
        -MetadataPath (Join-Path $directory "os-confirmation.json")

    $beforeSurface = Read-CaptureSurface `
        -MetadataPath (Join-Path $directory "os-before.json") `
        -ExpectedProcessId $processId `
        -ExpectedWindowTitle $windowTitle
    $afterSurface = Read-CaptureSurface `
        -MetadataPath (Join-Path $directory "os-after.json") `
        -ExpectedProcessId $processId `
        -ExpectedWindowTitle $windowTitle
    $confirmationSurface = Read-CaptureSurface `
        -MetadataPath (Join-Path $directory "os-confirmation.json") `
        -ExpectedProcessId $processId `
        -ExpectedWindowTitle $windowTitle
    Assert-SameCaptureSurface `
        -Expected $beforeSurface `
        -Actual $afterSurface `
        -Context "GameView reference captures for '$Name'"
    Assert-SameCaptureSurface `
        -Expected $afterSurface `
        -Actual $confirmationSurface `
        -Context "Post-command GameView reference captures for '$Name'"

    if ($commandExitCode -ne 0) {
        throw "uCLI screenshot command failed with exit $commandExitCode. See $commandPath and $commandErrorPath."
    }

    $commandResult = Read-JsonFile -Path $commandPath
    if ($commandResult.status -ne "ok") {
        throw "uCLI screenshot command did not return status=ok. See $commandPath."
    }

    if ($commandResult.payload.capture.colorSpace -ne "linear") {
        throw "Screenshot result did not report the configured linear color space. See $(Join-Path $directory 'command.json')."
    }

    Copy-UcliArtifact -CommandResult $commandResult -Destination (Join-Path $directory "artifact.png")
    $analysisPath = Join-Path $directory "analysis.json"
    $analysis = Invoke-OracleAnalysis `
        -Command "analyze-current" `
        -ArgumentList @(
            "--artifact", (Join-Path $directory "artifact.png"),
            "--reference", (Join-Path $directory "os-after.png"),
            "--confirmation-reference", (Join-Path $directory "os-confirmation.png")
        ) `
        -OutputPath $analysisPath

    return [pscustomobject]@{
        Fixture = $fixture.Value
        Analysis = $analysis
        AnalysisPath = $analysisPath
        CaptureSurface = $beforeSurface
    }
}

function Copy-DirectoryContents {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Source,

        [Parameter(Mandatory = $true)]
        [string] $Destination
    )

    if (-not (Test-Path -LiteralPath $Source -PathType Container)) {
        throw "Source directory does not exist: $Source"
    }

    [System.IO.Directory]::CreateDirectory($Destination) | Out-Null
    foreach ($item in @(Get-ChildItem -LiteralPath $Source -Force)) {
        Copy-Item -LiteralPath $item.FullName -Destination $Destination -Recurse -Force
    }
}

function Write-SourceSnapshot {
    $sourceIndex = Join-Path `
        ([System.IO.Path]::GetTempPath()) `
        ("ucli-screenshot-fidelity-{0}.index" -f [Guid]::NewGuid().ToString("N"))
    $archivePath = Join-Path $script:runDirectory "repository-source.zip"
    $previousIndex = $env:GIT_INDEX_FILE
    try {
        $env:GIT_INDEX_FILE = $sourceIndex
        Invoke-NativeText -Executable $script:gitExecutable -ArgumentList @("-C", $script:repositoryRoot, "read-tree", "HEAD") | Out-Null
        Invoke-NativeText -Executable $script:gitExecutable -ArgumentList @("-C", $script:repositoryRoot, "add", "-A", "--", ".") | Out-Null
        $sourceTree = Invoke-NativeText -Executable $script:gitExecutable -ArgumentList @("-C", $script:repositoryRoot, "write-tree")
    }
    finally {
        if ($null -eq $previousIndex) {
            Remove-Item Env:GIT_INDEX_FILE -ErrorAction SilentlyContinue
        }
        else {
            $env:GIT_INDEX_FILE = $previousIndex
        }

        Remove-Item -LiteralPath $sourceIndex -Force -ErrorAction SilentlyContinue
    }

    $revision = Invoke-NativeText -Executable $script:gitExecutable -ArgumentList @("-C", $script:repositoryRoot, "rev-parse", "HEAD")
    $objectFormat = Invoke-NativeText -Executable $script:gitExecutable -ArgumentList @("-C", $script:repositoryRoot, "rev-parse", "--show-object-format")
    $status = Invoke-NativeText -Executable $script:gitExecutable -ArgumentList @("-C", $script:repositoryRoot, "status", "--porcelain=v1", "--untracked-files=all")

    $archiveExit = Invoke-NativeTool `
        -Executable $script:gitExecutable `
        -ArgumentList @("-C", $script:repositoryRoot, "archive", "--format=zip", "--output=$archivePath", $sourceTree) `
        -StandardOutputPath (Join-Path $script:resultsDirectory "source-archive.stdout.log") `
        -StandardErrorPath (Join-Path $script:resultsDirectory "source-archive.stderr.log")
    if ($archiveExit -ne 0) {
        throw "Could not materialize the repository source snapshot. See source-archive.stderr.log."
    }

    Expand-Archive -LiteralPath $archivePath -DestinationPath $script:sourceSnapshot
    Remove-Item -LiteralPath $archivePath -Force
    Copy-DirectoryContents -Source $script:sourceSnapshot -Destination $script:buildWorkspace

    Write-JsonFile -Path $script:sourceProvenancePath -Value ([ordered]@{
        repositoryRevision = $revision
        repositoryObjectFormat = $objectFormat
        repositorySourceTree = $sourceTree
        worktreeDirty = -not [string]::IsNullOrWhiteSpace($status)
    })
}

function Assert-UnityLogClean {
    if (-not (Test-Path -LiteralPath $script:unityLogPath -PathType Leaf)) {
        throw "Unity log does not exist: $script:unityLogPath"
    }

    $diagnosticsPath = Join-Path $script:resultsDirectory "unity-compiler-importer-diagnostics.txt"
    $pattern = '(^|[^A-Za-z0-9_])(warning|error) CS[0-9]{4}([^0-9]|$)|The scripted importer .+ Registration rejected\.|Shader (warning|error) in|Scripts have compiler errors|Compilation failed'
    $matches = @(Select-String -LiteralPath $script:unityLogPath -Pattern $pattern -AllMatches)
    if ($matches.Count -gt 0) {
        $text = ($matches | ForEach-Object { $_.Line }) -join [Environment]::NewLine
        [System.IO.File]::WriteAllText($diagnosticsPath, $text + [Environment]::NewLine, $utf8WithoutBom)
        throw "Unity emitted compiler or importer diagnostics. See $diagnosticsPath."
    }

    [System.IO.File]::WriteAllText($diagnosticsPath, [string]::Empty, $utf8WithoutBom)
}

function Read-UnityEditorIdentity {
    if (-not (Test-Path -LiteralPath $script:unityLogPath -PathType Leaf)) {
        throw "Unity log does not exist: $script:unityLogPath"
    }

    $fileShare = [System.IO.FileShare](
        [int] [System.IO.FileShare]::ReadWrite -bor
        [int] [System.IO.FileShare]::Delete)
    $stream = [System.IO.FileStream]::new(
        $script:unityLogPath,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        $fileShare)
    try {
        $reader = [System.IO.StreamReader]::new(
            $stream,
            [System.Text.Encoding]::UTF8,
            $true)
        try {
            $unityLog = $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
    $identityPattern = '(?m)^Initialize engine version: (?<version>\S+) \((?<revision>[0-9a-fA-F]+)\)\r?$'
    $identityMatches = [regex]::Matches($unityLog, $identityPattern)
    if ($identityMatches.Count -ne 1) {
        throw "Unity log must contain exactly one initialized engine version and revision. See $script:unityLogPath."
    }

    $observedVersion = $identityMatches[0].Groups["version"].Value
    $observedRevision = $identityMatches[0].Groups["revision"].Value
    if (($observedVersion -ne $expectedUnityVersion) -or
        ($observedRevision -ne $expectedUnityRevision)) {
        throw "Unity Editor must initialize exactly $expectedUnityVersion ($expectedUnityRevision), but initialized $observedVersion ($observedRevision)."
    }

    return [pscustomobject]([ordered]@{
        version = $observedVersion
        revision = $observedRevision
    })
}

function Remove-TemporaryWorkDirectory {
    $temporaryRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd('\', '/')
    $target = [System.IO.Path]::GetFullPath($script:runDirectory).TrimEnd('\', '/')
    $targetParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetDirectoryName($target)).TrimEnd('\', '/')
    $targetName = [System.IO.Path]::GetFileName($target)
    $requiredPrefix = $temporaryRoot + [System.IO.Path]::DirectorySeparatorChar + "u-"
    if (($targetParent -ne $temporaryRoot) -or
        (-not $target.StartsWith($requiredPrefix, [StringComparison]::OrdinalIgnoreCase)) -or
        ($targetName -notmatch '^u-[0-9a-f]{12}$')) {
        throw "Refusing to remove a work directory that is not a verified direct child of the temporary directory: $target"
    }

    if (Test-Path -LiteralPath $target -PathType Container) {
        $targetItem = Get-Item -LiteralPath $target -Force
        if (($targetItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing to recursively remove a temporary work directory that is a reparse point: $target"
        }

        for ($attempt = 1; $attempt -le 20; $attempt++) {
            try {
                Remove-Item -LiteralPath $target -Recurse -Force
            }
            catch {
                if (-not (Test-Path -LiteralPath $target)) {
                    return
                }

                if ($attempt -eq 20) {
                    throw
                }

                Start-Sleep -Milliseconds 500
            }

            if (-not (Test-Path -LiteralPath $target)) {
                return
            }
        }

        if (Test-Path -LiteralPath $target) {
            throw "Could not remove the verified temporary work directory after twenty attempts: $target"
        }
    }
}

function Preserve-FixtureDiagnostics {
    $destination = Join-Path $script:resultsDirectory "fixture-runtime"
    [System.IO.Directory]::CreateDirectory($destination) | Out-Null
    foreach ($name in @("fixture-ready.json", "control.json", "fixture-start.cs", "unity-environment.json")) {
        $sourcePath = Join-Path $script:runDirectory $name
        if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
            Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $destination $name) -Force
        }
    }

    $responseSource = Join-Path $script:runDirectory "responses"
    if (Test-Path -LiteralPath $responseSource -PathType Container) {
        Copy-DirectoryContents `
            -Source $responseSource `
            -Destination (Join-Path $destination "responses")
    }
}

function Stop-UnityProcess {
    if (-not (Test-UnityRunning)) {
        return
    }

    $script:unityProcess.CloseMainWindow() | Out-Null
    $script:unityProcess.WaitForExit(5000) | Out-Null
    if (Test-UnityRunning) {
        Stop-Process -Id $script:unityProcess.Id -Force
        $script:unityProcess.WaitForExit(10000) | Out-Null
    }
}

function Invoke-Cleanup {
    if ($script:playModeEntered -and $script:daemonStarted -and (Test-UnityRunning) -and (Test-Path -LiteralPath $script:ucliExecutable -PathType Leaf)) {
        Invoke-NativeTool `
            -Executable $script:ucliExecutable `
            -ArgumentList @("play", "exit", "--projectPath", $script:unityProject, "--timeout", "30000") `
            -StandardOutputPath (Join-Path $script:resultsDirectory "cleanup-play-exit.json") `
            -StandardErrorPath (Join-Path $script:resultsDirectory "cleanup-play-exit.stderr.log") | Out-Null
    }

    if ($script:daemonStarted -and (Test-UnityRunning) -and (Test-Path -LiteralPath $script:ucliExecutable -PathType Leaf)) {
        Invoke-NativeTool `
            -Executable $script:ucliExecutable `
            -ArgumentList @("daemon", "stop", "--projectPath", $script:unityProject, "--timeout", "10000") `
            -StandardOutputPath (Join-Path $script:resultsDirectory "cleanup-daemon-stop.json") `
            -StandardErrorPath (Join-Path $script:resultsDirectory "cleanup-daemon-stop.stderr.log") | Out-Null
    }

    Stop-UnityProcess
    Preserve-FixtureDiagnostics
    if (-not $KeepWorkDirectory) {
        Remove-TemporaryWorkDirectory
    }
}

if ($env:OS -ne "Windows_NT") {
    throw "This system-test runner requires Windows."
}

if (-not [Environment]::UserInteractive) {
    throw "This system-test runner requires an interactive Windows desktop session."
}

$unityEditorPath = [System.IO.Path]::GetFullPath($UnityEditor)
if (-not (Test-Path -LiteralPath $unityEditorPath -PathType Leaf)) {
    throw "Unity Editor executable does not exist: $unityEditorPath"
}

$gitCommand = Get-Command "git.exe" -ErrorAction Stop
$dotnetCommand = Get-Command "dotnet.exe" -ErrorAction Stop
$script:gitExecutable = $gitCommand.Source
$script:dotnetExecutable = $dotnetCommand.Source
$gitCommandDirectory = Split-Path -Parent $script:gitExecutable
$gitInstallationDirectory = Split-Path -Parent $gitCommandDirectory
$gitBashCandidates = @(
    (Join-Path $gitInstallationDirectory "bin\bash.exe"),
    (Join-Path $gitCommandDirectory "bash.exe")
)
$script:bashExecutable = $null
foreach ($candidate in $gitBashCandidates) {
    if (Test-Path -LiteralPath $candidate -PathType Leaf) {
        $script:bashExecutable = $candidate
        break
    }
}

if ([string]::IsNullOrWhiteSpace($script:bashExecutable)) {
    throw "Git for Windows Bash is required to build the disposable Unity project's shared packages."
}

$script:repositoryRoot = Invoke-NativeText -Executable $script:gitExecutable -ArgumentList @("-C", $PSScriptRoot, "rev-parse", "--show-toplevel")

if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $timestamp = [DateTime]::UtcNow.ToString("yyyyMMddTHHmmssZ", [Globalization.CultureInfo]::InvariantCulture)
    $ResultsDirectory = Join-Path $script:repositoryRoot "TestResults\ScreenshotFidelity\windows-$timestamp"
}

if (-not [System.IO.Path]::IsPathRooted($ResultsDirectory)) {
    throw "ResultsDirectory must be an absolute path: $ResultsDirectory"
}

$script:resultsDirectory = [System.IO.Path]::GetFullPath($ResultsDirectory)
if (Test-Path -LiteralPath $script:resultsDirectory) {
    throw "ResultsDirectory must not already exist: $script:resultsDirectory"
}

[System.IO.Directory]::CreateDirectory($script:resultsDirectory) | Out-Null
$unityVersionStandardOutputPath = Join-Path $script:resultsDirectory "unity-version.stdout.log"
$unityVersionStandardErrorPath = Join-Path $script:resultsDirectory "unity-version.stderr.log"
$unityVersionProcess = Start-Process `
    -FilePath $unityEditorPath `
    -ArgumentList "-version" `
    -PassThru `
    -Wait `
    -WindowStyle Hidden `
    -RedirectStandardOutput $unityVersionStandardOutputPath `
    -RedirectStandardError $unityVersionStandardErrorPath
$observedUnityVersion = (Get-Content -LiteralPath $unityVersionStandardOutputPath -Raw).Trim()
if (($unityVersionProcess.ExitCode -ne 0) -or ($observedUnityVersion -ne $expectedUnityVersion)) {
    throw "Unity Editor version must be exactly $expectedUnityVersion, but '$observedUnityVersion' was reported."
}

$script:runDirectory = Join-Path `
    ([System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())) `
    ("u-{0}" -f [Guid]::NewGuid().ToString("N").Substring(0, 12))
$script:testRepository = Join-Path $script:runDirectory "r"
$script:unityProject = Join-Path $script:testRepository "p"
$script:toolDirectory = Join-Path $script:runDirectory "t"
$script:caseDirectory = Join-Path $script:resultsDirectory "cases"
$script:sourceSnapshot = Join-Path $script:runDirectory "s"
$script:buildWorkspace = Join-Path $script:runDirectory "b"
$script:sourceProvenancePath = Join-Path $script:resultsDirectory "source-provenance.json"
$script:fixtureReadyPath = Join-Path $script:runDirectory "fixture-ready.json"
$script:unityLogPath = Join-Path $script:resultsDirectory "unity.log"
$ucliDirectory = Join-Path $script:toolDirectory "ucli"
$oracleDirectory = Join-Path $script:toolDirectory "oracle-windows"
$script:ucliExecutable = Join-Path $ucliDirectory "MackySoft.Ucli.exe"
$script:oracleExecutable = Join-Path $oracleDirectory "screenshot-fidelity-oracle-windows.exe"
$script:unityProcess = $null
$script:guiSessionPath = $null
$script:daemonStarted = $false
$script:playModeEntered = $false
$script:nextSequence = 1
$overallStatus = "error"
$failureMessage = "System-test runner did not reach completion."
$exitCode = 0

foreach ($directory in @(
    $script:runDirectory,
    $script:testRepository,
    $script:unityProject,
    $script:toolDirectory,
    $script:caseDirectory,
    $script:sourceSnapshot,
    $script:buildWorkspace,
    (Join-Path $script:runDirectory "responses"),
    $ucliDirectory,
    $oracleDirectory
)) {
    [System.IO.Directory]::CreateDirectory($directory) | Out-Null
}

try {
    Write-Host "Recording the repository source snapshot..."
    Write-SourceSnapshot

    $windowsEnvironmentPath = Join-Path $script:resultsDirectory "windows-environment.json"
    Write-JsonFile -Path $windowsEnvironmentPath -Value ([ordered]@{
        observedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        operatingSystem = [Environment]::OSVersion.VersionString
        is64BitOperatingSystem = [Environment]::Is64BitOperatingSystem
        is64BitProcess = [Environment]::Is64BitProcess
        userInteractive = [Environment]::UserInteractive
        sessionName = $env:SESSIONNAME
        powerShellVersion = $PSVersionTable.PSVersion.ToString()
    })

    Write-Host "Building Unity shared packages from the recorded source snapshot..."
    $sharedPackageExit = Invoke-NativeTool `
        -Executable $script:bashExecutable `
        -ArgumentList @(
            (Join-Path $script:buildWorkspace "scripts\update-local-shared-packages.sh"),
            "--repo-root", $script:buildWorkspace,
            "--prune"
        ) `
        -StandardOutputPath (Join-Path $script:resultsDirectory "shared-package-build.log") `
        -StandardErrorPath (Join-Path $script:resultsDirectory "shared-package-build.stderr.log")
    if ($sharedPackageExit -ne 0) {
        throw "Shared package build failed. See shared-package-build.log and shared-package-build.stderr.log."
    }

    Write-Host "Preparing the disposable Unity fixture repository..."
    Copy-DirectoryContents `
        -Source (Join-Path $script:buildWorkspace "src\Ucli.Unity") `
        -Destination $script:unityProject
    Copy-DirectoryContents `
        -Source (Join-Path $script:sourceSnapshot "tests\System\ScreenshotFidelity\UnityFixture\Assets") `
        -Destination (Join-Path $script:unityProject "Assets")

    $projectSettingsPath = Join-Path $script:unityProject "ProjectSettings\ProjectSettings.asset"
    $projectSettings = [System.IO.File]::ReadAllText($projectSettingsPath)
    $colorSpacePattern = '(?m)^  m_ActiveColorSpace: [01](?=\r?$)'
    if ([regex]::Matches($projectSettings, $colorSpacePattern).Count -ne 1) {
        throw "Disposable Unity project must contain exactly one supported m_ActiveColorSpace setting."
    }

    $projectSettings = [regex]::Replace($projectSettings, $colorSpacePattern, "  m_ActiveColorSpace: 1")
    [System.IO.File]::WriteAllText($projectSettingsPath, $projectSettings, $utf8WithoutBom)

    $projectVersionPath = Join-Path $script:unityProject "ProjectSettings\ProjectVersion.txt"
    if (-not (Test-Path -LiteralPath $projectVersionPath -PathType Leaf)) {
        throw "Disposable Unity project does not contain ProjectSettings\\ProjectVersion.txt."
    }

    $projectVersion = @(
        "m_EditorVersion: $expectedUnityVersion",
        "m_EditorVersionWithRevision: $expectedUnityVersion ($expectedUnityRevision)"
    ) -join [Environment]::NewLine
    [System.IO.File]::WriteAllText(
        $projectVersionPath,
        $projectVersion + [Environment]::NewLine,
        $utf8WithoutBom)

    # Unity 6000.3 resolves URP 17.3, whose material migration has ten stages.
    # This fixture has no persistent materials that require the tenth migration,
    # so align only its disposable project before opening the GUI. If persistent
    # materials are added later, fail here instead of silently skipping migration.
    $persistentMaterialPaths = @(
        Get-ChildItem `
            -LiteralPath (Join-Path $script:unityProject "Assets") `
            -Filter "*.mat" `
            -File `
            -Recurse)
    if ($persistentMaterialPaths.Count -ne 0) {
        throw "Disposable Unity fixture contains persistent materials and requires a real URP migration before GUI startup."
    }

    $urpProjectSettingsPath = Join-Path $script:unityProject "ProjectSettings\URPProjectSettings.asset"
    if (-not (Test-Path -LiteralPath $urpProjectSettingsPath -PathType Leaf)) {
        throw "Disposable Unity project does not contain ProjectSettings\\URPProjectSettings.asset."
    }

    $urpProjectSettings = [System.IO.File]::ReadAllText($urpProjectSettingsPath)
    $urpMaterialVersionPattern = '(?m)^  m_LastMaterialVersion: 9(?=\r?$)'
    if ([regex]::Matches($urpProjectSettings, $urpMaterialVersionPattern).Count -ne 1) {
        throw "Disposable Unity project must contain exactly one URP material version setting at version 9."
    }

    $urpProjectSettings = [regex]::Replace(
        $urpProjectSettings,
        $urpMaterialVersionPattern,
        "  m_LastMaterialVersion: $expectedUrpMaterialVersion")
    [System.IO.File]::WriteAllText(
        $urpProjectSettingsPath,
        $urpProjectSettings,
        $utf8WithoutBom)

    Invoke-NativeText -Executable $script:gitExecutable -ArgumentList @("-C", $script:testRepository, "init", "-q") | Out-Null
    Invoke-NativeText -Executable $script:gitExecutable -ArgumentList @("-C", $script:testRepository, "config", "user.email", "screenshot-fidelity@example.invalid") | Out-Null
    Invoke-NativeText -Executable $script:gitExecutable -ArgumentList @("-C", $script:testRepository, "config", "user.name", "Screenshot Fidelity Harness") | Out-Null
    $ucliConfigurationDirectory = Join-Path $script:testRepository ".ucli"
    [System.IO.Directory]::CreateDirectory($ucliConfigurationDirectory) | Out-Null
    Write-JsonFile -Path (Join-Path $ucliConfigurationDirectory "config.json") -Value ([ordered]@{
        schemaVersion = 1
        operationPolicy = "dangerous"
        planTokenMode = "optional"
        operationAllowlist = @("^ucli\.cs\.eval$")
    })

    Write-Host "Publishing the current uCLI host and Windows oracle..."
    $ucliPublishExit = Invoke-NativeTool `
        -Executable $script:dotnetExecutable `
        -ArgumentList @(
            "publish",
            (Join-Path $script:buildWorkspace "src\Ucli\Ucli.csproj"),
            "--configuration", "Debug",
            "--output", $ucliDirectory
        ) `
        -StandardOutputPath (Join-Path $script:resultsDirectory "dotnet-publish-ucli.log") `
        -StandardErrorPath (Join-Path $script:resultsDirectory "dotnet-publish-ucli.stderr.log")
    if (($ucliPublishExit -ne 0) -or -not (Test-Path -LiteralPath $script:ucliExecutable -PathType Leaf)) {
        throw "uCLI publish failed. See dotnet-publish-ucli.log and dotnet-publish-ucli.stderr.log."
    }

    $oraclePublishExit = Invoke-NativeTool `
        -Executable $script:dotnetExecutable `
        -ArgumentList @(
            "publish",
            (Join-Path $script:sourceSnapshot "tests\System\ScreenshotFidelity\Oracle.Windows\ScreenshotFidelityOracle.Windows.csproj"),
            "--configuration", "Release",
            "--output", $oracleDirectory
        ) `
        -StandardOutputPath (Join-Path $script:resultsDirectory "dotnet-publish-oracle.log") `
        -StandardErrorPath (Join-Path $script:resultsDirectory "dotnet-publish-oracle.stderr.log")
    if (($oraclePublishExit -ne 0) -or -not (Test-Path -LiteralPath $script:oracleExecutable -PathType Leaf)) {
        throw "Windows oracle publish failed. See dotnet-publish-oracle.log and dotnet-publish-oracle.stderr.log."
    }

    $oracleSelfCheckPath = Join-Path $script:resultsDirectory "oracle-self-check.json"
    $oracleSelfCheckExit = Invoke-NativeTool `
        -Executable $script:oracleExecutable `
        -ArgumentList @("self-check", "--output", $oracleSelfCheckPath) `
        -StandardOutputPath (Join-Path $script:resultsDirectory "oracle-self-check.stdout.log") `
        -StandardErrorPath (Join-Path $script:resultsDirectory "oracle-self-check.stderr.log")
    if (($oracleSelfCheckExit -ne 0) -or -not (Test-Path -LiteralPath $oracleSelfCheckPath -PathType Leaf)) {
        throw "Windows oracle self-check failed. See oracle-self-check.json and oracle-self-check.stderr.log."
    }

    $oracleSelfCheck = Read-JsonFile -Path $oracleSelfCheckPath
    if ($oracleSelfCheck.passed -ne $true) {
        throw "Windows oracle self-check did not report passed=true. See oracle-self-check.json."
    }

    Write-Host "Launching the Unity GUI fixture with Direct3D 12..."
    if (($unityEditorPath.Contains('"')) -or ($script:unityProject.Contains('"')) -or ($script:unityLogPath.Contains('"'))) {
        throw "Unity launch paths must not contain quotation marks."
    }

    $unityArgumentLine = '-projectPath "{0}" -logFile "{1}" -force-d3d12 -accept-apiupdate' -f $script:unityProject, $script:unityLogPath
    $script:unityProcess = Start-Process `
        -FilePath $unityEditorPath `
        -ArgumentList $unityArgumentLine `
        -PassThru
    Wait-ForGuiSession -ExpectedProcessId $script:unityProcess.Id -TimeoutSeconds 360
    $unityEditorIdentity = Read-UnityEditorIdentity
    Copy-Item -LiteralPath $script:guiSessionPath -Destination (Join-Path $script:resultsDirectory "gui-session.json")

    $daemonStartPath = Join-Path $script:resultsDirectory "daemon-start.json"
    Invoke-Ucli -ResultPath $daemonStartPath -ArgumentList @(
        "daemon", "start",
        "--projectPath", $script:unityProject,
        "--editorMode", "gui",
        "--timeout", "180000"
    ) | Out-Null
    $script:daemonStarted = $true

    Write-Host "Entering the project's normal Play Mode..."
    $playEnterPath = Join-Path $script:resultsDirectory "play-enter.json"
    $playEnter = Invoke-Ucli -ResultPath $playEnterPath -ArgumentList @(
        "play", "enter",
        "--projectPath", $script:unityProject,
        "--timeout", "60000"
    )
    if (($playEnter.payload.lifecycleState -ne "playmode") -or
        ($playEnter.payload.playMode.state -ne "playing") -or
        ($playEnter.payload.playMode.transition -ne "none") -or
        ($playEnter.payload.playMode.isPlaying -ne $true) -or
        ($playEnter.payload.playMode.isPlayingOrWillChangePlaymode -ne $true)) {
        throw "Play Mode enter did not report a stable playing state. See $playEnterPath."
    }

    $script:playModeEntered = $true
    Start-Fixture -ResultPath (Join-Path $script:resultsDirectory "fixture-start-play.json")
    $unityEnvironmentPath = Join-Path $script:runDirectory "unity-environment.json"
    $unityEnvironment = Read-JsonFile -Path $unityEnvironmentPath
    Copy-Item -LiteralPath $unityEnvironmentPath -Destination (Join-Path $script:resultsDirectory "unity-environment.json")
    if (($unityEnvironment.unityVersion -ne $expectedUnityVersion) -or
        ($unityEnvironment.platform -ne "WindowsEditor") -or
        ($unityEnvironment.graphicsDeviceType -ne $expectedGraphicsDeviceType) -or
        ($unityEnvironment.colorSpace -ne $expectedColorSpace) -or
        ($unityEnvironment.renderPipelineType -ne $expectedRenderPipelineType) -or
        ($unityEnvironment.renderPipelinePackageName -ne $expectedRenderPipelinePackageName) -or
        ($unityEnvironment.renderPipelinePackageVersion -ne $expectedRenderPipelinePackageVersion)) {
        throw "Unity environment must be exactly $expectedUnityVersion, WindowsEditor, $expectedGraphicsDeviceType, $expectedColorSpace, and URP $expectedRenderPipelinePackageVersion. See unity-environment.json."
    }

    Invoke-Ucli -ResultPath (Join-Path $script:resultsDirectory "unity-console-clear.json") -ArgumentList @(
        "logs", "unity", "clear",
        "--projectPath", $script:unityProject,
        "--timeout", "30000"
    ) | Out-Null
    $baselinePath = Join-Path $script:resultsDirectory "unity-log-baseline.json"
    $baseline = Invoke-Ucli -ResultPath $baselinePath -ArgumentList @(
        "logs", "unity", "read",
        "--projectPath", $script:unityProject,
        "--tail", "1",
        "--level", "all",
        "--source", "all",
        "--stackTrace", "none",
        "--format", "json",
        "--timeout", "30000"
    )
    $baselineCursor = [string] $baseline.payload.nextCursor
    if ([string]::IsNullOrWhiteSpace($baselineCursor)) {
        throw "Unity log baseline did not return an incremental cursor. See $baselinePath."
    }

    Write-Host "Capturing passive GameView variant A..."
    $variantA = Run-GameVariant -Name "game-a" -Action "prepareWindowsGameA"
    Write-Host "Capturing passive GameView variant B in the same window..."
    $variantB = Run-GameVariant -Name "game-b" -Action "prepareWindowsGameB"
    if (([int] $variantA.Fixture.windowInstanceId -ne [int] $variantB.Fixture.windowInstanceId) -or
        ([int] $variantA.Fixture.processId -ne [int] $variantB.Fixture.processId) -or
        ([string] $variantA.Fixture.windowTitle -ne [string] $variantB.Fixture.windowTitle)) {
        throw "Variants A and B were not observed in the same GameView window. See cases\game-a\fixture.json and cases\game-b\fixture.json."
    }

    Assert-SameCaptureSurface `
        -Expected $variantA.CaptureSurface `
        -Actual $variantB.CaptureSurface `
        -Context "GameView reference captures across variants A and B"

    $variantsAnalysisPath = Join-Path $script:caseDirectory "variants-analysis.json"
    $variantsAnalysis = Invoke-OracleAnalysis `
        -Command "analyze-variants" `
        -ArgumentList @(
            "--left-reference", (Join-Path (Join-Path $script:caseDirectory "game-a") "os-after.png"),
            "--right-reference", (Join-Path (Join-Path $script:caseDirectory "game-b") "os-after.png")
        ) `
        -OutputPath $variantsAnalysisPath

    $unityErrorsPath = Join-Path $script:resultsDirectory "unity-errors-play.json"
    $unityErrors = Invoke-Ucli -ResultPath $unityErrorsPath -ArgumentList @(
        "logs", "unity", "read",
        "--projectPath", $script:unityProject,
        "--after", $baselineCursor,
        "--level", "error",
        "--source", "all",
        "--stackTrace", "all",
        "--format", "json",
        "--timeout", "30000"
    )
    if (($unityErrors.payload.count -ne 0) -or ($unityErrors.payload.completionReason -ne "completed")) {
        throw "Windows screenshot measurement emitted Unity errors. See $unityErrorsPath."
    }

    $unityWarningsPath = Join-Path $script:resultsDirectory "unity-warnings-play.json"
    $unityWarnings = Invoke-Ucli -ResultPath $unityWarningsPath -ArgumentList @(
        "logs", "unity", "read",
        "--projectPath", $script:unityProject,
        "--after", $baselineCursor,
        "--level", "warning",
        "--source", "all",
        "--stackTrace", "all",
        "--format", "json",
        "--timeout", "30000"
    )

    $playExitPath = Join-Path $script:resultsDirectory "play-exit.json"
    $playExit = Invoke-Ucli -ResultPath $playExitPath -ArgumentList @(
        "play", "exit",
        "--projectPath", $script:unityProject,
        "--timeout", "60000"
    )
    if (($playExit.payload.lifecycleState -ne "ready") -or
        ($playExit.payload.playMode.state -ne "stopped") -or
        ($playExit.payload.playMode.transition -ne "none") -or
        ($playExit.payload.playMode.isPlaying -ne $false) -or
        ($playExit.payload.playMode.isPlayingOrWillChangePlaymode -ne $false)) {
        throw "Play Mode exit did not report a stable stopped state. See $playExitPath."
    }

    $script:playModeEntered = $false
    Invoke-Ucli -ResultPath (Join-Path $script:resultsDirectory "daemon-stop.json") -ArgumentList @(
        "daemon", "stop",
        "--projectPath", $script:unityProject,
        "--timeout", "30000"
    ) | Out-Null
    $script:daemonStarted = $false
    Stop-UnityProcess
    Assert-UnityLogClean

    Write-JsonFile -Path (Join-Path $script:resultsDirectory "fidelity-result.json") -Value ([ordered]@{
        schemaVersion = 1
        status = "ok"
        configuration = [ordered]@{
            unityVersion = $unityEditorIdentity.version
            unityRevision = $unityEditorIdentity.revision
            graphicsDeviceType = $expectedGraphicsDeviceType
            colorSpace = "linear"
            playMode = "normal"
        }
        source = Read-JsonFile -Path $script:sourceProvenancePath
        environment = [ordered]@{
            windows = Read-JsonFile -Path $windowsEnvironmentPath
            unity = $unityEnvironment
            oracle = $oracleSelfCheck
        }
        verification = [ordered]@{
            sameGameView = $true
            gameViewCaptureSurface = $variantA.CaptureSurface
            unityErrorCount = [int] $unityErrors.payload.count
            unityWarningCount = [int] $unityWarnings.payload.count
            compilerOrImporterDiagnosticCount = 0
        }
        cases = [ordered]@{
            gameA = $variantA.Analysis
            gameB = $variantB.Analysis
            variants = $variantsAnalysis
        }
    })

    $overallStatus = "ok"
    $failureMessage = ""
    Write-Host "Windows screenshot fidelity lane passed: $(Join-Path $script:resultsDirectory 'fidelity-result.json')"
}
catch {
    $exitCode = 1
    $failureMessage = $_.Exception.Message
    [Console]::Error.WriteLine("screenshot-fidelity: $failureMessage")
}
finally {
    try {
        Invoke-Cleanup
    }
    catch {
        if ($exitCode -eq 0) {
            $exitCode = 1
            $overallStatus = "error"
            $failureMessage = "Cleanup failed: $($_.Exception.Message)"
        }

        Write-Warning "Screenshot fidelity cleanup failed: $($_.Exception.Message)"
    }

    Write-JsonFile -Path (Join-Path $script:resultsDirectory "runner-status.json") -Value ([ordered]@{
        status = $overallStatus
        message = $failureMessage
        observedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
        workDirectory = $script:runDirectory
        workDirectoryRetained = Test-Path -LiteralPath $script:runDirectory -PathType Container
    })
}

exit $exitCode
