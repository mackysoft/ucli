# Screenshot Surface GUI System Tests

These `Medium` tests verify that the public screenshot commands read the presentation surfaces used by Unity's GameView and active SceneView. The macOS lane covers GameView and SceneView in Edit Mode and normal Play Mode. The Windows lane covers the reported Unity 6000.3, Direct3D 12, Linear-color-space GameView failure in normal Play Mode.

The tests do not define screenshot semantics and do not judge whether the cube, scene, game, lighting, gizmos, overlays, or any other displayed content is correct. Public behavior comes from the command and property references. The tests only check the capture boundary, output integrity, requested dimensions where applicable, and request-owned state restoration.

## Capture boundary

Production and reference images use separate paths:

| Lane | Target | Production path | Independent reference |
| --- | --- | --- | --- |
| macOS | GameView | The existing main GameView presentation texture, production normalization, production PNG encoder | The same visible GameView window captured by WindowServer window ID |
| macOS | SceneView | The active SceneView HostView framebuffer content rectangle, production normalization, production PNG encoder | The same visible SceneView window captured by WindowServer window ID |
| Windows | GameView | The existing main GameView presentation texture, production normalization, production PNG encoder | The client area of the exact standalone GameView window captured by its native window handle |

For macOS current-surface captures, the oracle color-manages both images into Display P3 and compares the complete physical-pixel surface. Rounded WindowServer corner-mask pixels may be excluded only at the window corners, and at least `0.999` of the surface must remain comparable.

For Windows current-surface captures, the oracle uses Windows Graphics Capture to target the GameView window handle directly. It does not read the desktop, request foreground activation, or use `PrintWindow`. It crops the captured window composition to the Win32 client area. A pre-command reference is retained for diagnosis because the public command may legitimately refresh the presented frame. The artifact must match two stable references captured immediately after the command. The process, title, window handle, and capture geometry must identify the same GameView surface across all references and both visual variants. Non-opaque composition pixels may be excluded only when connected to a window corner, and at least `0.999` of the surface must remain comparable; interior transparency or a changing corner mask fails the lane.

Both lanes use fixed limits of mean absolute RGB-channel error `0.5/255`, p95 `2/255`, and maximum `4/255`. The artifact must be fully opaque and contain its required PNG `sRGB` chunk.

The fixture creates a camera-facing colored cube so orientation and crop errors are visible to the full-surface comparison. It does not test the cube's meaning or require a particular display configuration. GameView and SceneView are never compared with each other.

The macOS requested-resolution GameView case checks the returned PNG dimensions and restores the original GameView selection, size count, and target dimensions. Its restored WindowServer image has a different physical size, so the test does not invent a pixel comparison for that case.

## Environment boundary

The product does not require Unity to be frontmost or to own keyboard focus. Independent GUI references have stricter test-environment requirements:

- On macOS, the exact fixture window must remain on-screen and unminimized. A locked desktop or missing Screen Recording permission prevents reference capture.
- On Windows, the oracle captures the exact fixture GameView by native window handle. Other applications may remain focused or cover the fixture window.

These conditions are test-environment requirements, not product contracts.

### macOS prerequisites

- macOS with an unlocked interactive desktop session
- Screen Recording permission for the invoking terminal or automation runner
- Xcode command-line tools with `swiftc`
- .NET 8 SDK
- `jq`, `rsync`, `nuget` or `nuget.exe`, and `system_profiler`
- a licensed Unity Editor able to open the copied `src/Ucli.Unity` project

### Windows prerequisites

- Windows with an unlocked interactive desktop session
- Unity Editor `6000.3.11f1` and a valid license
- a Direct3D 12-capable GPU and driver
- Windows PowerShell 5.1
- .NET 8 SDK
- Git for Windows, including its bundled `bash.exe`

## Run on macOS

```bash
bash tests/System/ScreenshotFidelity/run-macos.sh \
  --unity-editor "/Applications/Unity/Hub/Editor/2023.2.22f1/Unity.app" \
  --color-space linear \
  --results-dir "$PWD/TestResults/ScreenshotFidelity/local"
```

`--color-space` accepts `linear` or `gamma` and changes only the disposable Unity project. Run both values when changing source selection, normalization, or encoding. `--keep-work-directory` retains the disposable project for diagnosis. An explicit results directory must not already exist.

## Run on Windows

```powershell
& .\tests\System\ScreenshotFidelity\run-windows.ps1 `
  -UnityEditor "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" `
  -ResultsDirectory "$PWD\TestResults\ScreenshotFidelity\windows-local"
```

The Windows lane is fixed to Unity `6000.3.11f1` revision `3000ef702840`, URP `17.3.0`, Direct3D 12, Linear color space, and normal Play Mode. It verifies the supplied Editor version before opening a GUI, then verifies the initialized revision and active render pipeline from the Unity process. The copied Unity 2023 project has no persistent `.mat` files, so the runner advances only the disposable `URPProjectSettings.asset` material version from 9 to 10 to prevent the URP material-upgrade modal. If a persistent material is added, the runner fails before opening Unity so a real headless migration can replace this preparation. `-KeepWorkDirectory` retains the disposable project for diagnosis. An explicit results directory must not already exist.

Both runners build all inputs from a recorded source snapshot. They start the production GUI daemon, invoke the fixture's allowlisted `ucli.cs.eval` entry point, and leave the project's normal Domain Reload and Scene Reload settings unchanged.

## Cases

The macOS lane runs these public-command cases:

1. SceneView current surface in stable Edit Mode.
2. GameView current surface in stable Edit Mode.
3. GameView requested `321x197` resolution in stable Edit Mode, including restoration.
4. SceneView current surface in stable Play Mode.
5. GameView current surface in stable Play Mode.
6. GameView requested `321x197` resolution in stable Play Mode, including restoration.

The odd, non-power-of-two requested size exposes rounding and one-pixel errors without adding any content requirement.

The Windows lane opens one standalone GameView window and runs two current-surface variants in normal Play Mode. Variant A establishes the window without an explicit fixture repaint. Variant B changes the camera background, material color, and cube rotation in the same window, again without an explicit fixture repaint. Each public capture must match two stable post-command window references, and the two variants must be visibly distinct. The pre-command reference is diagnostic only.

## Results

The macOS `fidelity-result.json` records the source snapshot, Unity and macOS environment, active color space, diagnostic counts, GameView restoration state, and six case results. Current-surface case directories contain the command result, artifact PNG, same-window reference PNG, and comparison JSON. Requested-resolution directories contain the command result, artifact PNG, before and after GameView state, and dimension analysis.

The Windows `fidelity-result.json` records the source snapshot, Windows and Unity environment, oracle self-check, diagnostic counts, both GameView comparisons, and the variant comparison. Each case contains the public command result, artifact PNG, diagnostic pre-command reference, two post-command window references, window metadata, and comparison JSON.

Warnings unrelated to screenshot execution are retained as diagnostics and are not promoted to screenshot failures. Compiler diagnostics, importer registration failures, fixture errors, command errors, invalid reference conditions, and pixel-surface mismatches fail the lane.
