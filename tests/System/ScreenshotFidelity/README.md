# Screenshot Surface GUI System Test

This macOS-only `Medium` test verifies that the public screenshot commands read the presentation surfaces used by Unity's GameView and active SceneView. It runs the production CLI against one GUI Unity Editor in stable Edit Mode and again with the project's normal Play Mode settings.

The test does not define screenshot semantics and does not judge whether the cube, scene, game, lighting, gizmos, overlays, or any other displayed content is correct. Public behavior comes from the command and property references. The test only checks the capture boundary, output integrity, requested dimensions, and request-owned state restoration.

## Capture boundary

Production and reference images use separate paths:

| Target | Production path | Independent reference |
| --- | --- | --- |
| GameView | The existing main GameView presentation texture, production normalization, production PNG encoder | The same visible GameView window captured by WindowServer window ID |
| SceneView | The active SceneView HostView framebuffer content rectangle, production normalization, production PNG encoder | The same visible SceneView window captured by WindowServer window ID |

For current-surface captures, the oracle color-manages both images into Display P3 and compares the complete physical-pixel surface. The fixed limits are mean absolute RGB-channel error `0.5/255`, p95 `2/255`, and maximum `4/255`. Rounded WindowServer corner-mask pixels may be excluded only at the window corners, and at least `0.999` of the surface must remain comparable. The artifact must be fully opaque and contain its required PNG `sRGB` chunk.

The fixture creates a camera-facing colored cube so orientation and crop errors are visible to the full-surface comparison. It does not test the cube's meaning or require a particular display configuration. GameView and SceneView are never compared with each other.

Requested-resolution GameView capture is checked by the returned PNG dimensions and by restoring the original GameView selection, size count, and target dimensions. Its restored WindowServer image has a different physical size, so the test does not invent a pixel comparison for that case.

## Environment boundary

The product does not require Unity to be frontmost or to own keyboard focus. The independent WindowServer reference requires only that its exact fixture window remain on-screen and unminimized. A locked desktop or missing Screen Recording permission prevents the reference capture and is reported as a test-environment failure, not a product contract.

Prerequisites:

- macOS with an unlocked interactive desktop session
- Screen Recording permission for the invoking terminal or automation runner
- Xcode command-line tools with `swiftc`
- .NET 8 SDK
- `jq`, `rsync`, `nuget` or `nuget.exe`, and `system_profiler`
- a licensed Unity Editor able to open the copied `src/Ucli.Unity` project

## Run

```bash
bash tests/System/ScreenshotFidelity/run-macos.sh \
  --unity-editor "/Applications/Unity/Hub/Editor/2023.2.22f1/Unity.app" \
  --color-space linear \
  --results-dir "$PWD/TestResults/ScreenshotFidelity/local"
```

`--color-space` accepts `linear` or `gamma` and changes only the disposable Unity project. Run both values when changing source selection, normalization, or encoding. `--keep-work-directory` retains the disposable project for diagnosis. An explicit results directory must not already exist.

The runner builds all inputs from the recorded source snapshot. It starts the production GUI daemon, invokes the fixture's allowlisted `ucli.cs.eval` entry point, and leaves the project's normal Domain Reload and Scene Reload settings unchanged.

## Cases

The lane runs these public-command cases:

1. SceneView current surface in stable Edit Mode.
2. GameView current surface in stable Edit Mode.
3. GameView requested `321x197` resolution in stable Edit Mode, including restoration.
4. SceneView current surface in stable Play Mode.
5. GameView current surface in stable Play Mode.
6. GameView requested `321x197` resolution in stable Play Mode, including restoration.

The odd, non-power-of-two requested size exposes rounding and one-pixel errors without adding any content requirement.

## Results

`fidelity-result.json` records the source snapshot, Unity and macOS environment, active color space, diagnostics counts, GameView restoration state, and the six case results. Current-surface case directories contain the command result, artifact PNG, same-window reference PNG, and comparison JSON. Requested-resolution directories contain the command result, artifact PNG, before/after GameView state, and dimension analysis.

Warnings unrelated to screenshot execution are retained as diagnostics and are not promoted to screenshot failures. Compiler diagnostics, importer registration failures, fixture errors, command errors, and pixel-surface mismatches fail the lane.
