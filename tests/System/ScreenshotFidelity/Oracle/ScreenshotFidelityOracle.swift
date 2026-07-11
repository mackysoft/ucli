import CoreGraphics
import Foundation
import ImageIO

private enum OracleError: Error, CustomStringConvertible {
    case invalidArguments(String)
    case failed(String)

    var description: String {
        switch self {
        case .invalidArguments(let message), .failed(let message):
            return message
        }
    }
}

private struct PixelImage {
    let width: Int
    let height: Int
    let pixels: [UInt8]
    let sourceColorSpace: String

    static func load(path: String) throws -> PixelImage {
        let url = URL(fileURLWithPath: path) as CFURL
        guard let source = CGImageSourceCreateWithURL(url, nil),
              let image = CGImageSourceCreateImageAtIndex(source, 0, nil) else {
            throw OracleError.failed("ImageIO could not decode image: \(path)")
        }

        guard image.width > 0, image.height > 0 else {
            throw OracleError.failed("Decoded image dimensions are invalid: \(path)")
        }

        guard let sourceColorSpace = image.colorSpace else {
            throw OracleError.failed("Decoded image has no resolvable color space: \(path)")
        }

        let sourceColorSpaceName = sourceColorSpace.name as String? ?? "unnamed-rgb"
        guard sourceColorSpace.model == .rgb else {
            throw OracleError.failed("Decoded image is not RGB: \(path), colorSpace=\(sourceColorSpaceName)")
        }

        guard let destinationColorSpace = CGColorSpace(name: CGColorSpace.sRGB) else {
            throw OracleError.failed("CoreGraphics could not create the sRGB comparison color space.")
        }

        let byteCount = try checkedMultiply(image.width, image.height, 4)
        var pixels = [UInt8](repeating: 0, count: byteCount)
        let createdContext = pixels.withUnsafeMutableBytes { bytes -> CGContext? in
            guard let baseAddress = bytes.baseAddress else {
                return nil
            }

            return CGContext(
                data: baseAddress,
                width: image.width,
                height: image.height,
                bitsPerComponent: 8,
                bytesPerRow: image.width * 4,
                space: destinationColorSpace,
                bitmapInfo: CGBitmapInfo.byteOrder32Big.rawValue
                    | CGImageAlphaInfo.premultipliedLast.rawValue)
        }
        guard let context = createdContext else {
            throw OracleError.failed("CoreGraphics could not create an RGBA8 sRGB decode context.")
        }

        // ImageIO's CGImage draws into this bitmap with its first decoded row at byte offset zero.
        // Applying a UIKit-style coordinate flip here would invert the owned pixel buffer.
        context.interpolationQuality = .none
        context.draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
        return PixelImage(
            width: image.width,
            height: image.height,
            pixels: pixels,
            sourceColorSpace: sourceColorSpaceName)
    }

    func pixel(x: Int, y: Int) -> Rgba {
        let clampedX = min(max(x, 0), width - 1)
        let clampedY = min(max(y, 0), height - 1)
        let offset = (clampedY * width + clampedX) * 4
        return Rgba(
            red: pixels[offset],
            green: pixels[offset + 1],
            blue: pixels[offset + 2],
            alpha: pixels[offset + 3])
    }
}

private struct Rgba {
    let red: UInt8
    let green: UInt8
    let blue: UInt8
    let alpha: UInt8
}

private struct IntRect: Codable, Equatable {
    let x: Int
    let y: Int
    let width: Int
    let height: Int

    var maxX: Int { x + width - 1 }
    var maxY: Int { y + height - 1 }
}

private struct Component {
    let area: Int
    let minX: Int
    let minY: Int
    let maxX: Int
    let maxY: Int

    var centerX: Double { Double(minX + maxX) / 2.0 }
    var centerY: Double { Double(minY + maxY) / 2.0 }
}

private enum Sentinel {
    case red
    case green
    case blue
    case yellow

    func matches(_ pixel: Rgba) -> Bool {
        let red = Int(pixel.red)
        let green = Int(pixel.green)
        let blue = Int(pixel.blue)
        switch self {
        case .red:
            return red >= 180 && green <= 105 && blue <= 105
        case .green:
            return green >= 180 && red <= 105 && blue <= 105
        case .blue:
            return blue >= 180 && red <= 105 && green <= 135
        case .yellow:
            return red >= 180 && green >= 180 && blue <= 115
        }
    }
}

private struct SampleDefinition {
    let name: String
    let x: Double
    let y: Double
    let isGray: Bool
}

private struct ColorSample: Codable {
    let name: String
    let red: Double
    let green: Double
    let blue: Double
}

private struct ComparisonMetrics: Codable {
    let maximumDeltaE76: Double
    let maximumLuminanceError: Double
    let grayRampRmsLuminanceError: Double
}

private struct FixtureValidity: Codable {
    let isValid: Bool
    let graySampleCount: Int
    let grayDistinctLevelCount: Int
    let grayMonotonicViolationCount: Int
    let grayLuminanceRange: Double
    let colorPatchCount: Int
    let nonBlackColorPatchCount: Int
    let minimumColorPatchDeltaE76: Double
}

private struct WindowMetadata: Codable {
    let windowId: UInt32
    let processId: Int32
    let title: String
    let boundsX: Double
    let boundsY: Double
    let boundsWidth: Double
    let boundsHeight: Double
    let imageWidth: Int
    let imageHeight: Int
    let sourceColorSpace: String
    let capturedAtUtc: String
}

private struct DisplayMetadata: Codable {
    let displayId: UInt32
    let boundsX: Double
    let boundsY: Double
    let boundsWidth: Double
    let boundsHeight: Double
    let isMain: Bool
    let colorSpace: String
}

private struct EnvironmentMetadata: Codable {
    let observedAtUtc: String
    let operatingSystem: String
    let architecture: String
    let screenCapturePermissionGranted: Bool
    let screenLocked: Bool
    let displays: [DisplayMetadata]
}

private struct AnalysisResult: Codable {
    let target: String
    let passed: Bool
    let artifactWidth: Int
    let artifactHeight: Int
    let artifactColorSpace: String
    let referenceBeforeColorSpace: String
    let referenceAfterColorSpace: String
    let artifactContentRect: IntRect
    let referenceBeforeContentRect: IntRect
    let referenceAfterContentRect: IntRect
    let artifactAlphaOpaque: Bool
    let decodedResolutionWidth: Int?
    let decodedResolutionHeight: Int?
    let artifactFixtureValidity: FixtureValidity
    let referenceBeforeFixtureValidity: FixtureValidity
    let referenceAfterFixtureValidity: FixtureValidity
    let referenceStability: ComparisonMetrics
    let artifactFidelity: ComparisonMetrics
    let failures: [String]
    let analyzedAtUtc: String
}

private struct SelfCheckResult: Codable {
    let passed: Bool
    let validFixture: FixtureValidity
    let blackFixture: FixtureValidity
    let checkedAtUtc: String
}

private struct ResolvedWindow {
    let id: CGWindowID
    let processId: pid_t
    let title: String
    let bounds: CGRect
}

private struct ParsedArguments {
    let command: String
    let values: [String: String]

    static func parse() throws -> ParsedArguments {
        let arguments = Array(CommandLine.arguments.dropFirst())
        guard let command = arguments.first else {
            throw OracleError.invalidArguments("Expected command: environment, self-check, capture, or analyze.")
        }

        var values: [String: String] = [:]
        var index = 1
        while index < arguments.count {
            let name = arguments[index]
            guard name.hasPrefix("--"), index + 1 < arguments.count else {
                throw OracleError.invalidArguments("Expected --name value argument pair. Actual: \(name)")
            }

            values[String(name.dropFirst(2))] = arguments[index + 1]
            index += 2
        }

        return ParsedArguments(command: command, values: values)
    }

    func required(_ name: String) throws -> String {
        guard let value = values[name], !value.isEmpty else {
            throw OracleError.invalidArguments("Missing required argument: --\(name)")
        }

        return value
    }

    func optionalInt(_ name: String) throws -> Int? {
        guard let value = values[name] else {
            return nil
        }

        guard let parsed = Int(value) else {
            throw OracleError.invalidArguments("Argument --\(name) must be an integer. Actual: \(value)")
        }

        return parsed
    }
}

private let sampleDefinitions: [SampleDefinition] = {
    var definitions: [SampleDefinition] = []
    for index in 0..<17 {
        definitions.append(SampleDefinition(
            name: String(format: "gray-%02d", index),
            x: (Double(index) + 0.5) / 17.0,
            y: 0.5,
            isGray: true))
    }

    definitions.append(contentsOf: [
        SampleDefinition(name: "warm-patch", x: 0.17, y: 0.71, isGray: false),
        SampleDefinition(name: "green-patch", x: 0.35, y: 0.71, isGray: false),
        SampleDefinition(name: "skin-patch", x: 0.53, y: 0.71, isGray: false),
        SampleDefinition(name: "blue-patch", x: 0.71, y: 0.71, isGray: false),
        SampleDefinition(name: "gradient-a", x: 0.27, y: 0.24, isGray: false),
        SampleDefinition(name: "gradient-b", x: 0.73, y: 0.24, isGray: false),
    ])
    return definitions
}()

private let maximumFidelityDeltaE76 = 5.0
private let maximumFidelityLuminanceError = 0.03
private let maximumGrayRampRmsLuminanceError = 0.02
private let maximumStabilityDeltaE76 = 2.0
private let maximumStabilityLuminanceError = 0.012
private let minimumGrayDistinctLevelCount = 12
private let minimumGrayLuminanceRange = 0.45
private let minimumColorPatchLuminance = 0.015
private let minimumColorPatchDeltaE76 = 5.0

private func main() throws {
    let arguments = try ParsedArguments.parse()
    switch arguments.command {
    case "environment":
        try writeEnvironment(outputPath: arguments.required("output"))
    case "self-check":
        try runSelfCheck(outputPath: arguments.required("output"))
    case "capture":
        let processIdValue = try arguments.required("pid")
        guard let processId = Int32(processIdValue) else {
            throw OracleError.invalidArguments("--pid must be a 32-bit process identifier.")
        }

        try captureWindow(
            processId: processId,
            title: arguments.required("title"),
            outputPath: arguments.required("output"),
            metadataPath: arguments.required("metadata"))
    case "analyze":
        try analyze(
            target: arguments.required("target"),
            artifactPath: arguments.required("artifact"),
            referenceBeforePath: arguments.required("reference-before"),
            referenceAfterPath: arguments.required("reference-after"),
            expectedWidth: arguments.optionalInt("expected-width"),
            expectedHeight: arguments.optionalInt("expected-height"),
            outputPath: arguments.required("output"))
    default:
        throw OracleError.invalidArguments("Unknown command: \(arguments.command)")
    }
}

private func runSelfCheck(outputPath: String) throws {
    var validSamples: [ColorSample] = (0..<17).map { index in
        let value = Double(index) / 16.0
        return ColorSample(
            name: String(format: "gray-%02d", index),
            red: value,
            green: value,
            blue: value)
    }
    validSamples.append(contentsOf: [
        ColorSample(name: "warm-patch", red: 0.62, green: 0.11, blue: 0.055),
        ColorSample(name: "green-patch", red: 0.08, green: 0.48, blue: 0.16),
        ColorSample(name: "skin-patch", red: 0.55, green: 0.25, blue: 0.16),
        ColorSample(name: "blue-patch", red: 0.07, green: 0.18, blue: 0.68),
        ColorSample(name: "gradient-a", red: 0.2528, green: 0.4856, blue: 0.4066),
        ColorSample(name: "gradient-b", red: 0.5472, green: 0.4856, blue: 0.2134),
    ])
    let blackSamples = sampleDefinitions.map { definition in
        ColorSample(name: definition.name, red: 0, green: 0, blue: 0)
    }
    let validFixture = validateFixtureSamples(validSamples)
    let blackFixture = validateFixtureSamples(blackSamples)
    let passed = validFixture.isValid && !blackFixture.isValid
    try writeJson(
        SelfCheckResult(
            passed: passed,
            validFixture: validFixture,
            blackFixture: blackFixture,
            checkedAtUtc: iso8601Now()),
        path: outputPath)
    if !passed {
        throw OracleError.failed("Screenshot fidelity oracle fixture-validity self-check failed; see \(outputPath)")
    }
}

private func writeEnvironment(outputPath: String) throws {
    var displayCount: UInt32 = 0
    let countResult = CGGetOnlineDisplayList(0, nil, &displayCount)
    guard countResult == .success else {
        throw OracleError.failed("CGGetOnlineDisplayList count failed: \(countResult.rawValue)")
    }

    var displayIds = [CGDirectDisplayID](repeating: 0, count: Int(displayCount))
    let listResult = CGGetOnlineDisplayList(displayCount, &displayIds, &displayCount)
    guard listResult == .success else {
        throw OracleError.failed("CGGetOnlineDisplayList failed: \(listResult.rawValue)")
    }

    let displays = displayIds.prefix(Int(displayCount)).map { displayId -> DisplayMetadata in
        let bounds = CGDisplayBounds(displayId)
        let displayColorSpace = CGDisplayCopyColorSpace(displayId)
        let iccByteCount = displayColorSpace.copyICCData().map(CFDataGetLength) ?? 0
        let colorSpace = displayColorSpace.name as String?
            ?? "unnamed-model-\(displayColorSpace.model.rawValue)-icc-\(iccByteCount)-bytes"
        return DisplayMetadata(
            displayId: displayId,
            boundsX: bounds.origin.x,
            boundsY: bounds.origin.y,
            boundsWidth: bounds.width,
            boundsHeight: bounds.height,
            isMain: CGDisplayIsMain(displayId) != 0,
            colorSpace: colorSpace)
    }

    let metadata = EnvironmentMetadata(
        observedAtUtc: iso8601Now(),
        operatingSystem: ProcessInfo.processInfo.operatingSystemVersionString,
        architecture: architectureName(),
        screenCapturePermissionGranted: CGPreflightScreenCaptureAccess(),
        screenLocked: isScreenLocked(),
        displays: displays)
    try writeJson(metadata, path: outputPath)
}

private func isScreenLocked() -> Bool {
    guard let session = CGSessionCopyCurrentDictionary() as? [String: Any] else {
        return true
    }

    return session["CGSSessionScreenIsLocked"] as? Bool ?? false
}

private func captureWindow(
    processId: Int32,
    title: String,
    outputPath: String,
    metadataPath: String) throws {
    guard CGPreflightScreenCaptureAccess() else {
        throw OracleError.failed(
            "macOS Screen Recording permission is required for the independent WindowServer oracle.")
    }

    let window = try resolveWindow(processId: processId, title: title)
    let process = Process()
    process.executableURL = URL(fileURLWithPath: "/usr/sbin/screencapture")
    process.arguments = ["-x", "-o", "-l\(window.id)", outputPath]
    let errorPipe = Pipe()
    process.standardError = errorPipe
    try process.run()
    process.waitUntilExit()
    if process.terminationStatus != 0 {
        let data = errorPipe.fileHandleForReading.readDataToEndOfFile()
        let error = String(data: data, encoding: .utf8) ?? "unknown screencapture error"
        throw OracleError.failed("screencapture failed with exit \(process.terminationStatus): \(error)")
    }

    let image = try PixelImage.load(path: outputPath)
    let opaqueOrColoredPixels = stride(from: 0, to: image.pixels.count, by: 4).reduce(into: 0) { count, offset in
        if image.pixels[offset + 3] != 0
            && (image.pixels[offset] != 0 || image.pixels[offset + 1] != 0 || image.pixels[offset + 2] != 0) {
            count += 1
        }
    }
    guard opaqueOrColoredPixels > max(64, image.width * image.height / 100) else {
        throw OracleError.failed("WindowServer capture is empty, transparent, or blocked by privacy controls.")
    }

    let metadata = WindowMetadata(
        windowId: window.id,
        processId: window.processId,
        title: window.title,
        boundsX: window.bounds.origin.x,
        boundsY: window.bounds.origin.y,
        boundsWidth: window.bounds.width,
        boundsHeight: window.bounds.height,
        imageWidth: image.width,
        imageHeight: image.height,
        sourceColorSpace: image.sourceColorSpace,
        capturedAtUtc: iso8601Now())
    try writeJson(metadata, path: metadataPath)
}

private func resolveWindow(processId: Int32, title: String) throws -> ResolvedWindow {
    guard let entries = CGWindowListCopyWindowInfo(
        [.optionOnScreenOnly, .excludeDesktopElements],
        kCGNullWindowID) as? [[String: Any]] else {
        throw OracleError.failed("WindowServer window list is unavailable.")
    }

    let matches = entries.compactMap { entry -> ResolvedWindow? in
        guard let ownerPid = entry[kCGWindowOwnerPID as String] as? Int32,
              ownerPid == processId,
              let layer = entry[kCGWindowLayer as String] as? Int,
              layer == 0,
              let onScreen = entry[kCGWindowIsOnscreen as String] as? Bool,
              onScreen,
              let name = entry[kCGWindowName as String] as? String,
              name == title,
              let idNumber = entry[kCGWindowNumber as String] as? NSNumber,
              let boundsDictionary = entry[kCGWindowBounds as String] as? NSDictionary else {
            return nil
        }

        var bounds = CGRect.zero
        guard CGRectMakeWithDictionaryRepresentation(boundsDictionary, &bounds),
              bounds.width > 0,
              bounds.height > 0 else {
            return nil
        }

        return ResolvedWindow(
            id: CGWindowID(idNumber.uint32Value),
            processId: ownerPid,
            title: name,
            bounds: bounds)
    }

    guard matches.count == 1, let match = matches.first else {
        throw OracleError.failed(
            "Expected exactly one on-screen Unity window titled '\(title)' for PID \(processId); found \(matches.count).")
    }

    return match
}

private func analyze(
    target: String,
    artifactPath: String,
    referenceBeforePath: String,
    referenceAfterPath: String,
    expectedWidth: Int?,
    expectedHeight: Int?,
    outputPath: String) throws {
    guard target == "game" || target == "scene" else {
        throw OracleError.invalidArguments("--target must be game or scene.")
    }

    guard try pngContainsSrgbChunk(path: artifactPath) else {
        throw OracleError.failed("Artifact PNG does not contain an explicit sRGB chunk.")
    }

    let artifact = try PixelImage.load(path: artifactPath)
    let referenceBefore = try PixelImage.load(path: referenceBeforePath)
    let referenceAfter = try PixelImage.load(path: referenceAfterPath)
    let artifactRect = try locateFixtureContent(in: artifact)
    let referenceBeforeRect = try locateFixtureContent(in: referenceBefore)
    let referenceAfterRect = try locateFixtureContent(in: referenceAfter)

    let artifactSamples = sampleColors(image: artifact, contentRect: artifactRect)
    let referenceBeforeSamples = sampleColors(image: referenceBefore, contentRect: referenceBeforeRect)
    let referenceAfterSamples = sampleColors(image: referenceAfter, contentRect: referenceAfterRect)
    let artifactFixtureValidity = validateFixtureSamples(artifactSamples)
    let referenceBeforeFixtureValidity = validateFixtureSamples(referenceBeforeSamples)
    let referenceAfterFixtureValidity = validateFixtureSamples(referenceAfterSamples)
    let stability = compareSamples(referenceBeforeSamples, referenceAfterSamples)
    let fidelity = compareSamples(artifactSamples, referenceAfterSamples)

    let alphaOpaque = stride(from: 3, to: artifact.pixels.count, by: 4).allSatisfy {
        artifact.pixels[$0] == 255
    }

    var failures: [String] = []
    if !artifactFixtureValidity.isValid {
        failures.append("Artifact fixture samples are missing, black, collapsed, or not mutually identifiable.")
    }

    if !referenceBeforeFixtureValidity.isValid {
        failures.append("Before-reference fixture samples are missing, black, collapsed, or not mutually identifiable.")
    }

    if !referenceAfterFixtureValidity.isValid {
        failures.append("After-reference fixture samples are missing, black, collapsed, or not mutually identifiable.")
    }

    let edgeTolerance = 2
    if artifactRect.x > edgeTolerance
        || artifactRect.y > edgeTolerance
        || artifact.width - artifactRect.maxX - 1 > edgeTolerance
        || artifact.height - artifactRect.maxY - 1 > edgeTolerance {
        failures.append("Artifact contains crop padding or content outside the fixture presentation border.")
    }

    if !alphaOpaque {
        failures.append("Artifact alpha is not fully opaque.")
    }

    if let expectedWidth, artifact.width != expectedWidth {
        failures.append("Artifact width \(artifact.width) does not equal expected width \(expectedWidth).")
    }

    if let expectedHeight, artifact.height != expectedHeight {
        failures.append("Artifact height \(artifact.height) does not equal expected height \(expectedHeight).")
    }

    if stability.maximumDeltaE76 > maximumStabilityDeltaE76
        || stability.maximumLuminanceError > maximumStabilityLuminanceError {
        failures.append("Window presentation changed between the before and after oracle captures.")
    }

    if fidelity.maximumDeltaE76 > maximumFidelityDeltaE76 {
        failures.append(String(
            format: "Artifact color differs from the color-managed window oracle: max DeltaE76 %.3f > %.3f.",
            fidelity.maximumDeltaE76,
            maximumFidelityDeltaE76))
    }

    if fidelity.maximumLuminanceError > maximumFidelityLuminanceError {
        failures.append(String(
            format: "Artifact luminance differs from the window oracle: max %.4f > %.4f.",
            fidelity.maximumLuminanceError,
            maximumFidelityLuminanceError))
    }

    if fidelity.grayRampRmsLuminanceError > maximumGrayRampRmsLuminanceError {
        failures.append(String(
            format: "Artifact gray-ramp transfer differs from the window oracle: RMS %.4f > %.4f.",
            fidelity.grayRampRmsLuminanceError,
            maximumGrayRampRmsLuminanceError))
    }

    var decodedWidth: Int?
    var decodedHeight: Int?
    if target == "game", expectedWidth != nil || expectedHeight != nil {
        let decoded = try decodeResolutionMarker(image: artifact)
        decodedWidth = decoded.width
        decodedHeight = decoded.height
        if decodedWidth != expectedWidth || decodedHeight != expectedHeight {
            failures.append(
                "Artifact resolution marker is stale: decoded \(decoded.width)x\(decoded.height), "
                    + "expected \(expectedWidth ?? -1)x\(expectedHeight ?? -1).")
        }
    }

    let result = AnalysisResult(
        target: target,
        passed: failures.isEmpty,
        artifactWidth: artifact.width,
        artifactHeight: artifact.height,
        artifactColorSpace: artifact.sourceColorSpace,
        referenceBeforeColorSpace: referenceBefore.sourceColorSpace,
        referenceAfterColorSpace: referenceAfter.sourceColorSpace,
        artifactContentRect: artifactRect,
        referenceBeforeContentRect: referenceBeforeRect,
        referenceAfterContentRect: referenceAfterRect,
        artifactAlphaOpaque: alphaOpaque,
        decodedResolutionWidth: decodedWidth,
        decodedResolutionHeight: decodedHeight,
        artifactFixtureValidity: artifactFixtureValidity,
        referenceBeforeFixtureValidity: referenceBeforeFixtureValidity,
        referenceAfterFixtureValidity: referenceAfterFixtureValidity,
        referenceStability: stability,
        artifactFidelity: fidelity,
        failures: failures,
        analyzedAtUtc: iso8601Now())
    try writeJson(result, path: outputPath)
    if !failures.isEmpty {
        throw OracleError.failed("Screenshot fidelity analysis failed; see \(outputPath)")
    }
}

private func locateFixtureContent(in image: PixelImage) throws -> IntRect {
    let green = try selectCornerComponent(in: image, sentinel: .green, corner: .topRight)
    let blue = try selectCornerComponent(in: image, sentinel: .blue, corner: .bottomLeft)
    let yellow = try selectCornerComponent(in: image, sentinel: .yellow, corner: .bottomRight)
    let red = try selectAlignedComponent(
        in: image,
        sentinel: .red,
        expectedX: blue.centerX,
        expectedY: green.centerY)

    let x = min(red.minX, blue.minX)
    let y = min(red.minY, green.minY)
    let maxX = max(green.maxX, yellow.maxX)
    let maxY = max(blue.maxY, yellow.maxY)
    let rect = IntRect(x: x, y: y, width: maxX - x + 1, height: maxY - y + 1)
    guard rect.width >= 100, rect.height >= 80 else {
        throw OracleError.failed("Fixture content rectangle is too small: \(rect.width)x\(rect.height).")
    }

    let aspect = Double(rect.width) / Double(rect.height)
    guard aspect >= 1.1, aspect <= 2.6 else {
        throw OracleError.failed("Fixture content rectangle has an invalid aspect ratio: \(aspect).")
    }

    let horizontalTolerance = max(4.0, Double(rect.width) * 0.035)
    let verticalTolerance = max(4.0, Double(rect.height) * 0.035)
    guard abs(red.centerX - blue.centerX) <= horizontalTolerance,
          abs(green.centerX - yellow.centerX) <= horizontalTolerance,
          abs(red.centerY - green.centerY) <= verticalTolerance,
          abs(blue.centerY - yellow.centerY) <= verticalTolerance else {
        throw OracleError.failed(
            "Fixture corner sentinels do not form one axis-aligned presentation rectangle. "
                + "red=\(describe(red)), green=\(describe(green)), "
                + "blue=\(describe(blue)), yellow=\(describe(yellow)).")
    }

    return rect
}

private func describe(_ component: Component) -> String {
    "(center=\(component.centerX),\(component.centerY); "
        + "bounds=\(component.minX),\(component.minY)-\(component.maxX),\(component.maxY); area=\(component.area))"
}

private func selectAlignedComponent(
    in image: PixelImage,
    sentinel: Sentinel,
    expectedX: Double,
    expectedY: Double) throws -> Component {
    let components = connectedComponents(in: image, matching: sentinel).filter { $0.area >= 9 }
    guard let selected = components.min(by: { left, right in
        hypot(left.centerX - expectedX, left.centerY - expectedY)
            < hypot(right.centerX - expectedX, right.centerY - expectedY)
    }) else {
        throw OracleError.failed("Fixture \(sentinel) corner sentinel was not found.")
    }

    return selected
}

private enum Corner {
    case topLeft
    case topRight
    case bottomLeft
    case bottomRight
}

private func selectCornerComponent(
    in image: PixelImage,
    sentinel: Sentinel,
    corner: Corner) throws -> Component {
    let components = connectedComponents(in: image, matching: sentinel).filter { $0.area >= 9 }
    guard !components.isEmpty else {
        throw OracleError.failed("Fixture \(sentinel) corner sentinel was not found.")
    }

    let selected = components.min { left, right in
        cornerDistance(left, image: image, corner: corner)
            < cornerDistance(right, image: image, corner: corner)
    }
    guard let selected else {
        throw OracleError.failed("Fixture \(sentinel) corner sentinel selection failed.")
    }

    return selected
}

private func cornerDistance(_ component: Component, image: PixelImage, corner: Corner) -> Double {
    switch corner {
    case .topLeft:
        return component.centerX + component.centerY
    case .topRight:
        return Double(image.width - 1) - component.centerX + component.centerY
    case .bottomLeft:
        return component.centerX + Double(image.height - 1) - component.centerY
    case .bottomRight:
        return Double(image.width - 1) - component.centerX
            + Double(image.height - 1) - component.centerY
    }
}

private func connectedComponents(in image: PixelImage, matching sentinel: Sentinel) -> [Component] {
    let pixelCount = image.width * image.height
    var visited = [Bool](repeating: false, count: pixelCount)
    var components: [Component] = []
    var queue: [Int] = []

    for index in 0..<pixelCount {
        if visited[index] {
            continue
        }

        visited[index] = true
        let x = index % image.width
        let y = index / image.width
        if !sentinel.matches(image.pixel(x: x, y: y)) {
            continue
        }

        queue.removeAll(keepingCapacity: true)
        queue.append(index)
        var queueIndex = 0
        var area = 0
        var minX = x
        var minY = y
        var maxX = x
        var maxY = y
        while queueIndex < queue.count {
            let current = queue[queueIndex]
            queueIndex += 1
            let currentX = current % image.width
            let currentY = current / image.width
            area += 1
            minX = min(minX, currentX)
            minY = min(minY, currentY)
            maxX = max(maxX, currentX)
            maxY = max(maxY, currentY)

            let neighbors = [
                (currentX - 1, currentY),
                (currentX + 1, currentY),
                (currentX, currentY - 1),
                (currentX, currentY + 1),
            ]
            for (neighborX, neighborY) in neighbors {
                if neighborX < 0 || neighborY < 0 || neighborX >= image.width || neighborY >= image.height {
                    continue
                }

                let neighborIndex = neighborY * image.width + neighborX
                if visited[neighborIndex] {
                    continue
                }

                visited[neighborIndex] = true
                if sentinel.matches(image.pixel(x: neighborX, y: neighborY)) {
                    queue.append(neighborIndex)
                }
            }
        }

        components.append(Component(
            area: area,
            minX: minX,
            minY: minY,
            maxX: maxX,
            maxY: maxY))
    }

    return components
}

private func sampleColors(image: PixelImage, contentRect: IntRect) -> [ColorSample] {
    sampleDefinitions.map { definition in
        let centerX = contentRect.x + Int((Double(contentRect.width - 1) * definition.x).rounded())
        let centerY = contentRect.y + Int((Double(contentRect.height - 1) * definition.y).rounded())
        let radiusX = max(1, Int(Double(contentRect.width) * 0.006))
        let radiusY = max(1, Int(Double(contentRect.height) * 0.006))
        var red: [UInt8] = []
        var green: [UInt8] = []
        var blue: [UInt8] = []
        for y in (centerY - radiusY)...(centerY + radiusY) {
            for x in (centerX - radiusX)...(centerX + radiusX) {
                let pixel = image.pixel(x: x, y: y)
                red.append(pixel.red)
                green.append(pixel.green)
                blue.append(pixel.blue)
            }
        }

        return ColorSample(
            name: definition.name,
            red: Double(median(red)) / 255.0,
            green: Double(median(green)) / 255.0,
            blue: Double(median(blue)) / 255.0)
    }
}

private func validateFixtureSamples(_ samples: [ColorSample]) -> FixtureValidity {
    let graySamples = samples.filter { $0.name.hasPrefix("gray-") }
    let grayLuminances = graySamples.map { luminance(linearRgb($0)) }
    let grayDistinctLevelCount = Set(grayLuminances.map { value in
        Int((value * 4096.0).rounded())
    }).count
    let grayMonotonicViolationCount = zip(grayLuminances, grayLuminances.dropFirst()).reduce(0) { count, pair in
        pair.1 + 0.002 < pair.0 ? count + 1 : count
    }
    let grayLuminanceRange: Double
    if let minimumGray = grayLuminances.min(), let maximumGray = grayLuminances.max() {
        grayLuminanceRange = maximumGray - minimumGray
    } else {
        grayLuminanceRange = 0
    }

    let colorPatches = samples.filter { !$0.name.hasPrefix("gray-") }
    let colorPatchLabs = colorPatches.map { lab(linearRgb($0)) }
    let nonBlackColorPatchCount = colorPatches.filter {
        luminance(linearRgb($0)) >= minimumColorPatchLuminance
    }.count
    var minimumPatchDeltaE = Double.greatestFiniteMagnitude
    if colorPatchLabs.count >= 2 {
        for leftIndex in 0..<(colorPatchLabs.count - 1) {
            for rightIndex in (leftIndex + 1)..<colorPatchLabs.count {
                minimumPatchDeltaE = min(
                    minimumPatchDeltaE,
                    deltaE76(colorPatchLabs[leftIndex], colorPatchLabs[rightIndex]))
            }
        }
    } else {
        minimumPatchDeltaE = 0
    }

    let expectedGraySampleCount = sampleDefinitions.filter(\.isGray).count
    let expectedColorPatchCount = sampleDefinitions.count - expectedGraySampleCount
    let isValid = graySamples.count == expectedGraySampleCount
        && grayDistinctLevelCount >= minimumGrayDistinctLevelCount
        && grayMonotonicViolationCount == 0
        && grayLuminanceRange >= minimumGrayLuminanceRange
        && colorPatches.count == expectedColorPatchCount
        && nonBlackColorPatchCount == expectedColorPatchCount
        && minimumPatchDeltaE >= minimumColorPatchDeltaE76
    return FixtureValidity(
        isValid: isValid,
        graySampleCount: graySamples.count,
        grayDistinctLevelCount: grayDistinctLevelCount,
        grayMonotonicViolationCount: grayMonotonicViolationCount,
        grayLuminanceRange: grayLuminanceRange,
        colorPatchCount: colorPatches.count,
        nonBlackColorPatchCount: nonBlackColorPatchCount,
        minimumColorPatchDeltaE76: minimumPatchDeltaE)
}

private func compareSamples(_ left: [ColorSample], _ right: [ColorSample]) -> ComparisonMetrics {
    var maximumDeltaE = 0.0
    var maximumLuminanceError = 0.0
    var graySquaredError = 0.0
    var grayCount = 0
    for (leftSample, rightSample) in zip(left, right) {
        let leftLinear = linearRgb(leftSample)
        let rightLinear = linearRgb(rightSample)
        let deltaE = deltaE76(lab(leftLinear), lab(rightLinear))
        let luminanceError = abs(luminance(leftLinear) - luminance(rightLinear))
        maximumDeltaE = max(maximumDeltaE, deltaE)
        maximumLuminanceError = max(maximumLuminanceError, luminanceError)
        if leftSample.name.hasPrefix("gray-") {
            graySquaredError += luminanceError * luminanceError
            grayCount += 1
        }
    }

    return ComparisonMetrics(
        maximumDeltaE76: maximumDeltaE,
        maximumLuminanceError: maximumLuminanceError,
        grayRampRmsLuminanceError: grayCount == 0 ? 0 : sqrt(graySquaredError / Double(grayCount)))
}

private func decodeResolutionMarker(image: PixelImage) throws -> (width: Int, height: Int) {
    var bits: [Bool] = []
    let sampleY = image.height - 24
    for index in 0..<20 {
        let cellIndex = index + (index >= 10 ? 1 : 0)
        let sampleX = 42 + cellIndex * 8
        let pixel = image.pixel(x: sampleX, y: sampleY)
        let red = Int(pixel.red)
        let green = Int(pixel.green)
        let blue = Int(pixel.blue)
        if green >= 165 && blue >= 165 && red <= 125 {
            bits.append(true)
        } else if red >= 165 && blue >= 165 && green <= 125 {
            bits.append(false)
        } else {
            throw OracleError.failed(
                "Resolution marker bit \(index) has an unknown color: \(red),\(green),\(blue).")
        }
    }

    func decode(_ range: Range<Int>) -> Int {
        range.reduce(0) { value, index in
            (value << 1) | (bits[index] ? 1 : 0)
        }
    }

    return (width: decode(0..<10), height: decode(10..<20))
}

private struct LinearRgb {
    let red: Double
    let green: Double
    let blue: Double
}

private struct Lab {
    let l: Double
    let a: Double
    let b: Double
}

private func linearRgb(_ sample: ColorSample) -> LinearRgb {
    LinearRgb(
        red: srgbToLinear(sample.red),
        green: srgbToLinear(sample.green),
        blue: srgbToLinear(sample.blue))
}

private func srgbToLinear(_ value: Double) -> Double {
    value <= 0.04045
        ? value / 12.92
        : pow((value + 0.055) / 1.055, 2.4)
}

private func luminance(_ color: LinearRgb) -> Double {
    color.red * 0.2126 + color.green * 0.7152 + color.blue * 0.0722
}

private func lab(_ color: LinearRgb) -> Lab {
    let x = color.red * 0.4124564 + color.green * 0.3575761 + color.blue * 0.1804375
    let y = color.red * 0.2126729 + color.green * 0.7151522 + color.blue * 0.0721750
    let z = color.red * 0.0193339 + color.green * 0.1191920 + color.blue * 0.9503041
    let fx = labFunction(x / 0.95047)
    let fy = labFunction(y)
    let fz = labFunction(z / 1.08883)
    return Lab(l: 116.0 * fy - 16.0, a: 500.0 * (fx - fy), b: 200.0 * (fy - fz))
}

private func labFunction(_ value: Double) -> Double {
    let threshold = pow(6.0 / 29.0, 3.0)
    return value > threshold
        ? pow(value, 1.0 / 3.0)
        : value / (3.0 * pow(6.0 / 29.0, 2.0)) + 4.0 / 29.0
}

private func deltaE76(_ left: Lab, _ right: Lab) -> Double {
    let deltaL = left.l - right.l
    let deltaA = left.a - right.a
    let deltaB = left.b - right.b
    return sqrt(deltaL * deltaL + deltaA * deltaA + deltaB * deltaB)
}

private func median(_ values: [UInt8]) -> UInt8 {
    let sorted = values.sorted()
    return sorted[sorted.count / 2]
}

private func pngContainsSrgbChunk(path: String) throws -> Bool {
    let data = try Data(contentsOf: URL(fileURLWithPath: path))
    let signature: [UInt8] = [137, 80, 78, 71, 13, 10, 26, 10]
    guard data.count >= signature.count,
          Array(data.prefix(signature.count)) == signature else {
        throw OracleError.failed("Artifact does not have a PNG signature: \(path)")
    }

    var offset = signature.count
    while offset + 12 <= data.count {
        let length = Int(readUInt32BigEndian(data, offset: offset))
        let typeStart = offset + 4
        let typeEnd = typeStart + 4
        guard typeEnd <= data.count,
              let type = String(data: data[typeStart..<typeEnd], encoding: .ascii) else {
            throw OracleError.failed("Artifact PNG chunk header is invalid.")
        }

        if type == "sRGB" {
            return true
        }

        if type == "IDAT" || type == "IEND" {
            return false
        }

        let nextOffset = offset + 12 + length
        guard nextOffset > offset, nextOffset <= data.count else {
            throw OracleError.failed("Artifact PNG chunk length is invalid.")
        }

        offset = nextOffset
    }

    return false
}

private func readUInt32BigEndian(_ data: Data, offset: Int) -> UInt32 {
    let bytes = data[offset..<(offset + 4)]
    return bytes.reduce(UInt32(0)) { value, byte in
        (value << 8) | UInt32(byte)
    }
}

private func checkedMultiply(_ values: Int...) throws -> Int {
    var result = 1
    for value in values {
        let multiplication = result.multipliedReportingOverflow(by: value)
        if multiplication.overflow {
            throw OracleError.failed("Image byte count overflowed Int.")
        }

        result = multiplication.partialValue
    }

    return result
}

private func writeJson<T: Encodable>(_ value: T, path: String) throws {
    let encoder = JSONEncoder()
    encoder.outputFormatting = [.prettyPrinted, .sortedKeys, .withoutEscapingSlashes]
    let data = try encoder.encode(value)
    let url = URL(fileURLWithPath: path)
    try FileManager.default.createDirectory(
        at: url.deletingLastPathComponent(),
        withIntermediateDirectories: true)
    try data.write(to: url, options: .atomic)
}

private func iso8601Now() -> String {
    ISO8601DateFormatter().string(from: Date())
}

private func architectureName() -> String {
#if arch(arm64)
    return "arm64"
#elseif arch(x86_64)
    return "x86_64"
#else
    return "unknown"
#endif
}

do {
    try main()
} catch {
    FileHandle.standardError.write(Data("screenshot-fidelity-oracle: \(error)\n".utf8))
    exit(1)
}
