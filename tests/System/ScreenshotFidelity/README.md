# Screenshot Fidelity GUI System Test

This macOS-only system-test lane compares committed screenshot artifacts with an independent WindowServer capture of the same Unity presentation. It is a `Medium` test because it coordinates the uCLI process, one GUI Unity Editor process, and the macOS window-capture process on one machine.

The lane does not establish public screenshot semantics. The command reference and screenshot compatibility document remain normative. A successful run is evidence for the exact environment recorded in `fidelity-result.json`; it does not implicitly enable other Unity versions, operating-system versions, graphics devices, display modes, or render pipelines.

## Independent oracle boundary

Production and reference pixels follow separate paths:

| Target | Production path | Reference path |
| --- | --- | --- |
| GameView | GameView backing texture, production normalization, production PNG encoder | WindowServer window ID, `/usr/sbin/screencapture`, ImageIO and ColorSync decode |
| SceneView | Unity Editor window-framebuffer helper, production normalization, production PNG encoder | WindowServer window ID, `/usr/sbin/screencapture`, ImageIO and ColorSync decode |

The oracle does not reference the production capture API, normalization shader, row-order calibration, or PNG validator. It finds the presentation rectangle from four asymmetric fixture sentinels rather than using the production crop rectangle.

Before comparing artifact and reference colors, the oracle requires the 17-step gray ramp to retain sufficient luminance range and distinct levels, and requires every named color patch to be non-black and distinguishable from the other patches. An all-black artifact and all-black reference therefore cannot pass with a zero color difference. The runner also executes a synthetic oracle self-check before launching Unity and writes its result to `oracle-self-check.json`.

## Prerequisites

- macOS with an unlocked interactive desktop session
- Screen Recording permission for the invoking terminal or automation runner
- Xcode command-line tools with `swiftc`
- .NET 8 SDK
- `jq`, `rsync`, and `system_profiler`
- a licensed Unity Editor that can open the copied `src/Ucli.Unity` project

The Unity window must remain on-screen and unminimized. A locked desktop, missing Screen Recording permission, an ambiguous window, an empty OS capture, an unknown image color space, or a missing fixture border fails the lane.

## Run

Pass either a Unity application bundle or the Unity executable:

```bash
bash tests/System/ScreenshotFidelity/run-macos.sh \
  --unity-editor "/Applications/Unity/Hub/Editor/2023.2.22f1/Unity.app" \
  --results-dir "$PWD/TestResults/ScreenshotFidelity/local"
```

Use `--keep-work-directory` to retain the imported disposable Unity project for diagnosis. Without it, the runner removes the copied Unity `Library`, `Temp`, and `Logs` directories after the run.

## Cases

The lane runs these contracts through the public CLI:

1. GameView current surface matches the color-managed WindowServer presentation.
2. GameView requested `321x197` capture contains the requested-resolution marker and restores the original GameView state.
3. SceneView capture fails with `SCREENSHOT_CAPTURE_UNSUPPORTED` and commits no PNG while Unity's standard Overlay Menu control is displayed.

Unity 2023.2 renders the standard three-line Overlay Menu control in the SceneView window outside the configurable `Overlay` collection. Hiding that control would mutate the observed presentation, so this lane does not manufacture a SceneView pixel-success precondition for that environment. Scene pixel fidelity remains `notRun` until a naturally panel-free source or a version capability that excludes the control is established.

The fixture includes asymmetric corners, a one-pixel edge signature, a Screen Space 17-step gray ramp and color patches, a URP post-process volume, a camera-stack marker, runtime IMGUI, SceneView handles, and a GameView resolution marker. The GameView oracle rejects vertical or horizontal inversion, channel swaps, crop padding, non-opaque artifact alpha, invalid or collapsed fixture samples, material color or luminance changes, a stale requested-resolution frame, and presentation changes between the before and after OS captures.

## Results

`fidelity-result.json` contains:

- Unity, macOS, GPU, graphics API, active color space, render pipeline, display, and backing-scale observations
- per-case crop, alpha, color-difference, luminance, gray-ramp, and resolution-marker results
- the GameView state comparison and `GameViewSizes.asset` hash before and after Editor exit
- the source revision used by the run

Each case directory retains the command result, standard error, artifact PNG, WindowServer captures, WindowServer metadata, fixture state, and analysis JSON. A failed run leaves `runner-status.json` and the available intermediate evidence.

The runner copies `GameViewSizes.asset` into the results directory for diagnosis and compares its hash after Unity exits. It never overwrites or deletes the real preference file because another Unity process or the user may have changed it concurrently. A hash difference fails the lane and reports the backup path. Fixture Scene and Overlay state live in the disposable Unity project; production capture remains responsible for observing existing state without changing it.
