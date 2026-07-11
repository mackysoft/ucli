# Screenshot Fidelity GUI System Test

This macOS-only system-test lane compares committed screenshot artifacts with an independent WindowServer capture of the same Unity presentation. It is a `Medium` test because it coordinates the uCLI process, one GUI Unity Editor process, and the macOS window-capture process on one machine.

The lane does not establish public screenshot semantics. The command and property references define the public contract; the screenshot compatibility document is a non-normative implementation and measurement record. A successful run is evidence for the exact environment and source snapshot recorded in `fidelity-result.json`; it does not implicitly enable other Unity versions, operating-system versions, graphics devices, display modes, or render pipelines.

## Independent oracle boundary

Production and reference pixels follow separate paths:

| Target | Production path | Reference path |
| --- | --- | --- |
| GameView | GameView backing texture, production normalization, production PNG encoder | WindowServer window ID, `/usr/sbin/screencapture`, ImageIO and ColorSync decode |
| SceneView | Unity Editor window-framebuffer helper, production normalization, production PNG encoder | WindowServer window ID, `/usr/sbin/screencapture`, ImageIO and ColorSync decode |

The oracle does not reference the production capture API, normalization shader, row-order calibration, or PNG validator. GameView uses four asymmetric fixture sentinels. SceneView uses three asymmetric edge sentinels placed from the active `OnSceneGUI` clip observed through `GUIClip.visibleRect`; the fixture does not use the production `SceneView.cameraViewport` crop authority.

Before comparing artifact and reference colors, the oracle requires the camera-rendered 17-step gray ramp to retain sufficient luminance range and distinct levels, and requires every named color patch to be non-black and distinguishable from the other patches. The Canvas and `OnSceneGUI` probes do not duplicate those samples. An absent world-render path therefore cannot be hidden by a later UI composite.

GameView additionally requires independent presence signatures for the world pattern, the intentional post-process Volume, the overlay camera in the camera stack, Screen Space Overlay UI, and runtime IMGUI. The post-process probe starts as a neutral shader color and must show the predeclared warm-channel separation after presentation. The resolution marker is encoded independently by the Canvas and must decode to the artifact dimensions. The runner executes synthetic positive and one-missing-route oracle checks before launching Unity and writes the result to `oracle-self-check.json`.

GameView current-surface and SceneView capture additionally compare every corresponding decoded sRGB8 RGB channel at the same physical pixel. The fixed limits are mean absolute error `0.5/255`, p95 absolute error `2/255`, and maximum absolute error `4/255`. Requested-resolution GameView capture has a different physical size from the restored WindowServer reference, so it uses the route, sample, exact-dimension, and restoration gates instead of a false one-to-one full-image comparison. These limits and route-presence thresholds are benchmark inputs fixed before the candidate run and are not calibrated from its output.

WindowServer's rounded window mask can make the outer corner pixels non-opaque. Such pixels are excluded only when every non-opaque component touches an image corner, remains inside the coverage-derived corner envelope, and the compared before/after WindowServer masks are identical. At least `0.999` of physical pixels must remain compared. An interior mask, a changed mask, or lower coverage fails the lane. The screenshot artifact itself must remain fully opaque.

## Prerequisites

- macOS with an unlocked interactive desktop session
- Screen Recording permission for the invoking terminal or automation runner
- Xcode command-line tools with `swiftc`
- .NET 8 SDK
- `jq`, `rsync`, `nuget` (or `nuget.exe`), and `system_profiler`
- a licensed Unity Editor that can open the copied `src/Ucli.Unity` project

The Unity window must remain on-screen and unminimized. Each WindowServer reference waits until the Unity fixture process is frontmost, and the Scene fixture fixes its selected object, transform tool, camera mode, lighting, gizmo, and Scene effects state before measurement. A locked desktop, missing Screen Recording permission, an ambiguous window, an empty OS capture, an unknown image color space, or a missing fixture border fails the lane.

The disposable fixture contains no `Light`. Fog, skybox, ambient contribution, reflections, shadows, light probes, reflection probes, occlusion culling, dynamic resolution, antialiasing, XR, and project renderer features are disabled. The pattern and overlay marker use separate explicit layers and an `SRPDefaultUnlit` pass. The only intentional presentation effects are the declared post-process Volume, camera stack, Game-only Screen Space Overlay Canvas and runtime IMGUI, and Scene-only handles. The Game probes are disabled during Scene measurement because Unity otherwise represents the Screen Space Overlay Canvas as world content that covers the independent Scene samples. Their target-specific state is checked after every fixture action and recorded in `fixture-render-isolation.json`.

## Run

Pass either a Unity application bundle or the Unity executable:

```bash
bash tests/System/ScreenshotFidelity/run-macos.sh \
  --unity-editor "/Applications/Unity/Hub/Editor/2023.2.22f1/Unity.app" \
  --results-dir "$PWD/TestResults/ScreenshotFidelity/local"
```

Use `--keep-work-directory` to retain the imported disposable Unity project for diagnosis. Without it, the runner removes the copied Unity `Library`, `Temp`, and `Logs` directories after the run.

The fixture starts only through its `InitializeOnLoad` bootstrap. The runner does not also use `-executeMethod`, which would request a redundant reload during a cold GUI import and duplicate scripted-importer registration. The complete GUI log is rejected if it contains C# diagnostics, shader compilation failures, or rejected importer registrations.

## Cases

The lane runs these contracts through the public CLI:

1. GameView current surface matches the color-managed WindowServer presentation.
2. GameView requested `321x197` capture contains the requested-resolution marker and restores the original GameView state.
3. SceneView current presentation matches the complete physical-pixel WindowServer content while the Overlay Menu is closed and every configurable Overlay except the included orientation gizmo is hidden.
4. SceneView capture fails with `SCREENSHOT_CAPTURE_UNSUPPORTED`, preserves the displayed UI state, and commits no screenshot artifact while the actual Overlay Menu popup is displayed.
5. SceneView capture has the same fail-closed and state-preservation behavior while one real configurable Overlay panel is displayed.

The fixture discovers the Overlay Menu and configurable panel through the members exposed by the running Editor. It does not branch on a Unity version string. If the fixture cannot construct or observe the required condition, the harness reports an unsupported fixture failure rather than treating the case as passing.

The fixture includes asymmetric corners, a one-pixel edge signature, a camera-rendered 17-step gray ramp and color patches, an independently identifiable URP post-process probe, camera-stack marker, Screen Space resolution marker, runtime IMGUI signature, and SceneView handles. The oracle rejects vertical or horizontal inversion, channel swaps, crop padding, non-opaque artifact alpha, a missing render route, invalid or collapsed fixture samples, material color or luminance changes, stale requested-resolution frames, and presentation changes between the before and after OS captures. GameView current-surface and SceneView full-image comparison also reject any localized difference beyond the fixed limits.

## Results

`fidelity-result.json` contains:

- Unity, macOS, GPU, graphics API, active color space, render pipeline, display, and backing-scale observations
- per-case crop, alpha-mask topology, physical-pixel coverage, full-image RGB error, color-difference, luminance, gray-ramp, and resolution-marker results
- the GameView state comparison and `GameViewSizes.asset` hash before and after Editor exit
- the fixture lighting, RenderSettings, renderer-feature, camera, layer, Volume, Canvas, shader-message, and SceneView isolation state
- the measurement-window Unity runtime error and warning counts, plus pre-bootstrap compiler and importer-registration diagnostics from the complete GUI log
- the source revision, exact Git tree assembled from tracked and non-ignored untracked working-tree files, and whether that source snapshot differed from the revision
- the path, digest, and file count of `execution-input-manifest.json`, which hashes every file and symbolic link supplied to the disposable Unity project, published uCLI host, and WindowServer oracle

The runner materializes the recorded Git tree before building. It creates the ignored Unity shared-package restore outputs in a separate build workspace from that tree, then records the complete derived execution inputs before launching Unity. It never consumes ignored package DLLs from the caller's working tree.

Each case directory retains the command result, standard error, available artifact PNG, WindowServer captures, WindowServer metadata, fixture state, and analysis JSON. Fail-closed cases retain the screenshot artifact path/digest set from before and after the command. A failed run leaves `runner-status.json` and the available intermediate evidence.

The runner copies `GameViewSizes.asset` into the results directory for diagnosis and compares its hash after Unity exits. It never overwrites or deletes the real preference file because another Unity process or the user may have changed it concurrently. A hash difference fails the lane and reports the backup path. Fixture Scene and Overlay state live in the disposable Unity project; production capture remains responsible for observing existing state without changing it.
