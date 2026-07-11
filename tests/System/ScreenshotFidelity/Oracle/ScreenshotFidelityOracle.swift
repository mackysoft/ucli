import AppKit
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

    static func load(
        path: String,
        destinationColorSpaceName: CFString = CGColorSpace.sRGB) throws -> PixelImage {
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

        guard let destinationColorSpace = CGColorSpace(name: destinationColorSpaceName) else {
            throw OracleError.failed(
                "CoreGraphics could not create the requested comparison color space: \(destinationColorSpaceName)")
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
            throw OracleError.failed(
                "CoreGraphics could not create an RGBA8 decode context for \(destinationColorSpaceName).")
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
    case cyan
    case magenta
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
        case .cyan:
            return green >= 180 && blue >= 180 && red <= 115
        case .magenta:
            return red >= 180 && blue >= 180 && green <= 115
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

private struct FullImageComparisonMetrics: Codable {
    let dimensionsMatch: Bool
    let leftWidth: Int
    let leftHeight: Int
    let rightWidth: Int
    let rightHeight: Int
    let totalPixelCount: Int
    let comparedPixelCount: Int
    let excludedNonOpaquePixelCount: Int
    let leftNonOpaquePixelCount: Int
    let rightNonOpaquePixelCount: Int
    let alphaMasksMatch: Bool
    let alphaMaskTopologyValid: Bool
    let matchingAlphaMasksRequired: Bool
    let comparedRgbChannelCount: Int
    let meanAbsoluteRgbChannelErrorNormalized: Double?
    let percentile95AbsoluteRgbChannelErrorNormalized: Double?
    let maximumAbsoluteRgbChannelErrorNormalized: Double?
}

private enum FullImageAlphaMaskPolicy: Equatable {
    case matchingWindowMasks
    case opaqueArtifactAndWindowMask
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

private struct RouteValidity: Codable {
    let isValid: Bool
    let worldPatternValid: Bool
    let cameraStackMarkerPresent: Bool
    let cameraStackMarkerSample: ColorSample
    let postProcessProbePresent: Bool
    let postProcessProbeSample: ColorSample
    let runtimeImguiPresent: Bool
    let runtimeImguiWhiteSample: ColorSample
    let runtimeImguiBlackSample: ColorSample
    let runtimeImguiCyanSample: ColorSample
    let resolutionMarkerValid: Bool
    let decodedResolutionWidth: Int?
    let decodedResolutionHeight: Int?
    let expectedResolutionWidth: Int
    let expectedResolutionHeight: Int
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
    let applicationFrontmost: Bool
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
    let fullImageComparisonColorSpace: String
    let artifactContentRect: IntRect
    let referenceBeforeContentRect: IntRect
    let referenceAfterContentRect: IntRect
    let artifactAlphaOpaque: Bool
    let decodedResolutionWidth: Int?
    let decodedResolutionHeight: Int?
    let artifactFixtureValidity: FixtureValidity
    let referenceBeforeFixtureValidity: FixtureValidity
    let referenceAfterFixtureValidity: FixtureValidity
    let artifactRouteValidity: RouteValidity?
    let referenceBeforeRouteValidity: RouteValidity?
    let referenceAfterRouteValidity: RouteValidity?
    let referenceStability: ComparisonMetrics
    let artifactFidelity: ComparisonMetrics
    let referenceFullImageStability: FullImageComparisonMetrics?
    let artifactFullImageFidelity: FullImageComparisonMetrics?
    let failures: [String]
    let analyzedAtUtc: String
}

private struct SelfCheckResult: Codable {
    let passed: Bool
    let validFixture: FixtureValidity
    let blackFixture: FixtureValidity
    let sceneSentinelLocatorPassed: Bool
    let fullImageComparisonPassed: Bool
    let gameRouteValidityPassed: Bool
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
// Full-image byte metrics use Display P3 as their common RGB space because it contains
// the artifact's sRGB gamut. Decoding an 8-bit Display P3 WindowServer reference down to
// 8-bit sRGB amplifies quantization at saturated sRGB boundaries before comparison.
private let fullImageComparisonColorSpaceName = CGColorSpace.displayP3
// These full-image limits were fixed from the documented pre-implementation
// measurement (mean 0.073/255, p95 1/255, max 1/255). Do not tune them from a
// candidate run of this benchmark.
private let maximumFullImageMeanAbsoluteRgbChannelErrorNormalized = 0.5 / 255.0
private let maximumFullImagePercentile95AbsoluteRgbChannelErrorNormalized = 2.0 / 255.0
private let maximumFullImageAbsoluteRgbChannelErrorNormalized = 4.0 / 255.0
private let minimumFullImageOpaquePixelCoverage = 0.999
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
    let sceneSentinelLocatorPassed = runSceneSentinelLocatorSelfCheck()
    let fullImageComparisonPassed = try runFullImageComparisonSelfCheck()
    let gameRouteValidityPassed = runGameRouteValiditySelfCheck(
        validWorldPattern: validFixture,
        invalidWorldPattern: blackFixture)
    let passed = validFixture.isValid
        && !blackFixture.isValid
        && sceneSentinelLocatorPassed
        && fullImageComparisonPassed
        && gameRouteValidityPassed
    try writeJson(
        SelfCheckResult(
            passed: passed,
            validFixture: validFixture,
            blackFixture: blackFixture,
            sceneSentinelLocatorPassed: sceneSentinelLocatorPassed,
            fullImageComparisonPassed: fullImageComparisonPassed,
            gameRouteValidityPassed: gameRouteValidityPassed,
            checkedAtUtc: iso8601Now()),
        path: outputPath)
    if !passed {
        throw OracleError.failed("Screenshot fidelity oracle fixture-validity self-check failed; see \(outputPath)")
    }
}

private func runSceneSentinelLocatorSelfCheck() -> Bool {
    let width = 160
    let height = 120
    var pixels = [UInt8](repeating: 0, count: width * height * 4)
    for alphaOffset in stride(from: 3, to: pixels.count, by: 4) {
        pixels[alphaOffset] = 255
    }

    func paint(x: Range<Int>, y: Range<Int>, color: Rgba) {
        for pixelY in y {
            for pixelX in x {
                let offset = (pixelY * width + pixelX) * 4
                pixels[offset] = color.red
                pixels[offset + 1] = color.green
                pixels[offset + 2] = color.blue
                pixels[offset + 3] = color.alpha
            }
        }
    }

    let cyan = Rgba(red: 0, green: 255, blue: 255, alpha: 255)
    let magenta = Rgba(red: 255, green: 0, blue: 255, alpha: 255)
    let yellow = Rgba(red: 255, green: 255, blue: 0, alpha: 255)
    paint(x: 0..<6, y: 0..<6, color: cyan)
    paint(x: 27..<132, y: 20..<22, color: cyan)
    paint(x: 27..<125, y: 98..<100, color: magenta)
    paint(x: 125..<140, y: 88..<100, color: yellow)
    paint(x: 20..<28, y: 20..<28, color: cyan)
    paint(x: 20..<28, y: 92..<100, color: magenta)
    paint(x: 132..<140, y: 92..<100, color: yellow)

    let image = PixelImage(
        width: width,
        height: height,
        pixels: pixels,
        sourceColorSpace: "synthetic-sRGB")
    guard (try? locateSceneFixtureContent(in: image))
        == IntRect(x: 20, y: 20, width: 120, height: 80) else {
        return false
    }

    let horizontalFlip = flipPixelImage(image, horizontally: true, vertically: false)
    let verticalFlip = flipPixelImage(image, horizontally: false, vertically: true)
    let missingSentinel = removingPixels(matching: .yellow, from: image)
    let ambiguous = syntheticAmbiguousSceneLocatorImage()
    return rejectsSceneFixture(horizontalFlip)
        && rejectsSceneFixture(verticalFlip)
        && rejectsSceneFixture(missingSentinel)
        && rejectsSceneFixture(ambiguous)
}

private func runFullImageComparisonSelfCheck() throws -> Bool {
    let baseline = syntheticSolidImage(width: 10, height: 1, channelValue: 100)
    var boundaryPixels = baseline.pixels
    boundaryPixels[0] = 104
    boundaryPixels[1] = 102
    let boundary = PixelImage(
        width: baseline.width,
        height: baseline.height,
        pixels: boundaryPixels,
        sourceColorSpace: "synthetic-sRGB")
    let rect = IntRect(x: 0, y: 0, width: baseline.width, height: baseline.height)
    let boundaryMetrics = try compareFullImageRgb(
        left: baseline,
        leftRect: rect,
        right: boundary,
        rightRect: rect,
        alphaMaskPolicy: .matchingWindowMasks)
    let epsilon = 0.000_000_000_001
    guard let boundaryMean = boundaryMetrics.meanAbsoluteRgbChannelErrorNormalized,
          let boundaryPercentile95 = boundaryMetrics.percentile95AbsoluteRgbChannelErrorNormalized,
          let boundaryMaximum = boundaryMetrics.maximumAbsoluteRgbChannelErrorNormalized,
          abs(boundaryMean - (0.2 / 255.0)) <= epsilon,
          abs(boundaryPercentile95 - (2.0 / 255.0)) <= epsilon,
          abs(boundaryMaximum - (4.0 / 255.0)) <= epsilon,
          fullImageFailures(
              metrics: boundaryMetrics,
              comparisonName: "self-check boundary").isEmpty else {
        return false
    }

    let overMean = syntheticSolidImage(width: 10, height: 1, channelValue: 101)
    let overMeanMetrics = try compareFullImageRgb(
        left: baseline,
        leftRect: rect,
        right: overMean,
        rightRect: rect,
        alphaMaskPolicy: .matchingWindowMasks)
    guard !fullImageFailures(
        metrics: overMeanMetrics,
        comparisonName: "self-check over-mean").isEmpty else {
        return false
    }

    var overPercentilePixels = baseline.pixels
    overPercentilePixels[0] = 103
    overPercentilePixels[1] = 103
    let overPercentile = PixelImage(
        width: baseline.width,
        height: baseline.height,
        pixels: overPercentilePixels,
        sourceColorSpace: "synthetic-sRGB")
    let overPercentileMetrics = try compareFullImageRgb(
        left: baseline,
        leftRect: rect,
        right: overPercentile,
        rightRect: rect,
        alphaMaskPolicy: .matchingWindowMasks)
    guard let overPercentileMean = overPercentileMetrics.meanAbsoluteRgbChannelErrorNormalized,
          let overPercentile95 = overPercentileMetrics.percentile95AbsoluteRgbChannelErrorNormalized,
          let overPercentileMaximum = overPercentileMetrics.maximumAbsoluteRgbChannelErrorNormalized,
          overPercentileMean <= maximumFullImageMeanAbsoluteRgbChannelErrorNormalized,
          overPercentile95 > maximumFullImagePercentile95AbsoluteRgbChannelErrorNormalized,
          overPercentileMaximum <= maximumFullImageAbsoluteRgbChannelErrorNormalized,
          !fullImageFailures(
              metrics: overPercentileMetrics,
              comparisonName: "self-check over-p95").isEmpty else {
        return false
    }

    var overMaximumPixels = baseline.pixels
    overMaximumPixels[0] = 105
    let overMaximum = PixelImage(
        width: baseline.width,
        height: baseline.height,
        pixels: overMaximumPixels,
        sourceColorSpace: "synthetic-sRGB")
    let overMaximumMetrics = try compareFullImageRgb(
        left: baseline,
        leftRect: rect,
        right: overMaximum,
        rightRect: rect,
        alphaMaskPolicy: .matchingWindowMasks)
    guard let overMaximumMean = overMaximumMetrics.meanAbsoluteRgbChannelErrorNormalized,
          let overMaximum95 = overMaximumMetrics.percentile95AbsoluteRgbChannelErrorNormalized,
          let overMaximumError = overMaximumMetrics.maximumAbsoluteRgbChannelErrorNormalized,
          overMaximumMean <= maximumFullImageMeanAbsoluteRgbChannelErrorNormalized,
          overMaximum95 <= maximumFullImagePercentile95AbsoluteRgbChannelErrorNormalized,
          overMaximumError > maximumFullImageAbsoluteRgbChannelErrorNormalized,
          !fullImageFailures(
              metrics: overMaximumMetrics,
              comparisonName: "self-check over-maximum").isEmpty else {
        return false
    }

    let coverageBaseline = syntheticSolidImage(width: 100, height: 100, channelValue: 100)
    var roundedMaskPixels = coverageBaseline.pixels
    let bottomLeftOffset = ((coverageBaseline.height - 1) * coverageBaseline.width) * 4
    roundedMaskPixels[bottomLeftOffset] = 255
    roundedMaskPixels[bottomLeftOffset + 1] = 0
    roundedMaskPixels[bottomLeftOffset + 2] = 255
    roundedMaskPixels[bottomLeftOffset + 3] = 0
    let roundedMaskReference = PixelImage(
        width: coverageBaseline.width,
        height: coverageBaseline.height,
        pixels: roundedMaskPixels,
        sourceColorSpace: "synthetic-sRGB")
    let coverageRect = IntRect(x: 0, y: 0, width: 100, height: 100)
    let roundedMaskMetrics = try compareFullImageRgb(
        left: coverageBaseline,
        leftRect: coverageRect,
        right: roundedMaskReference,
        rightRect: coverageRect,
        alphaMaskPolicy: .opaqueArtifactAndWindowMask)
    guard roundedMaskMetrics.excludedNonOpaquePixelCount == 1,
          roundedMaskMetrics.comparedPixelCount == 9_999,
          roundedMaskMetrics.alphaMaskTopologyValid,
          fullImageFailures(
              metrics: roundedMaskMetrics,
              comparisonName: "self-check rounded mask").isEmpty else {
        return false
    }

    var excessiveMaskPixels = coverageBaseline.pixels
    for pixelIndex in 0..<11 {
        let x = pixelIndex % 3
        let y = pixelIndex / 3
        excessiveMaskPixels[(y * coverageBaseline.width + x) * 4 + 3] = 0
    }
    let excessiveMaskReference = PixelImage(
        width: coverageBaseline.width,
        height: coverageBaseline.height,
        pixels: excessiveMaskPixels,
        sourceColorSpace: "synthetic-sRGB")
    let excessiveMaskMetrics = try compareFullImageRgb(
        left: coverageBaseline,
        leftRect: coverageRect,
        right: excessiveMaskReference,
        rightRect: coverageRect,
        alphaMaskPolicy: .opaqueArtifactAndWindowMask)
    guard excessiveMaskMetrics.alphaMaskTopologyValid,
          !fullImageFailures(
        metrics: excessiveMaskMetrics,
        comparisonName: "self-check excessive mask").isEmpty else {
        return false
    }

    var interiorMaskPixels = coverageBaseline.pixels
    interiorMaskPixels[(50 * coverageBaseline.width + 50) * 4 + 3] = 0
    let interiorMaskReference = PixelImage(
        width: coverageBaseline.width,
        height: coverageBaseline.height,
        pixels: interiorMaskPixels,
        sourceColorSpace: "synthetic-sRGB")
    let interiorMaskMetrics = try compareFullImageRgb(
        left: coverageBaseline,
        leftRect: coverageRect,
        right: interiorMaskReference,
        rightRect: coverageRect,
        alphaMaskPolicy: .opaqueArtifactAndWindowMask)
    guard !interiorMaskMetrics.alphaMaskTopologyValid,
          !fullImageFailures(
              metrics: interiorMaskMetrics,
              comparisonName: "self-check interior mask").isEmpty else {
        return false
    }

    let mismatchedMaskMetrics = try compareFullImageRgb(
        left: coverageBaseline,
        leftRect: coverageRect,
        right: roundedMaskReference,
        rightRect: coverageRect,
        alphaMaskPolicy: .matchingWindowMasks)
    guard mismatchedMaskMetrics.alphaMaskTopologyValid,
          !mismatchedMaskMetrics.alphaMasksMatch,
          !fullImageFailures(
              metrics: mismatchedMaskMetrics,
              comparisonName: "self-check mismatched masks").isEmpty else {
        return false
    }

    let mismatched = syntheticSolidImage(width: 9, height: 1, channelValue: 100)
    let mismatchedMetrics = try compareFullImageRgb(
        left: baseline,
        leftRect: rect,
        right: mismatched,
        rightRect: IntRect(x: 0, y: 0, width: mismatched.width, height: mismatched.height),
        alphaMaskPolicy: .matchingWindowMasks)
    return !mismatchedMetrics.dimensionsMatch
        && mismatchedMetrics.meanAbsoluteRgbChannelErrorNormalized == nil
        && !fullImageFailures(
            metrics: mismatchedMetrics,
            comparisonName: "self-check dimensions").isEmpty
}

private func runGameRouteValiditySelfCheck(
    validWorldPattern: FixtureValidity,
    invalidWorldPattern: FixtureValidity) -> Bool {
    // Both dimensions exceed the former ten-bit ceiling, so the self-check proves
    // that the complete twelve-bit width and height fields participate in decoding.
    let image = syntheticGameRouteImage(width: 1_200, height: 1_100)
    let presentationRect = IntRect(x: 0, y: 0, width: image.width, height: image.height)
    let valid = evaluateGameRouteValidity(
        image: image,
        presentationRect: presentationRect,
        worldPatternValidity: validWorldPattern)
    guard valid.isValid,
          valid.decodedResolutionWidth == image.width,
          valid.decodedResolutionHeight == image.height else {
        return false
    }

    let missingWorldPattern = evaluateGameRouteValidity(
        image: image,
        presentationRect: presentationRect,
        worldPatternValidity: invalidWorldPattern)
    let missingCameraStack = evaluateGameRouteValidity(
        image: paintingNormalizedRect(
            in: image,
            normalizedRect: CGRect(x: 0.47, y: 0.12, width: 0.06, height: 0.05),
            color: Rgba(red: 0, green: 0, blue: 0, alpha: 255)),
        presentationRect: presentationRect,
        worldPatternValidity: validWorldPattern)
    let missingPostProcess = evaluateGameRouteValidity(
        image: paintingNormalizedRect(
            in: image,
            normalizedRect: CGRect(x: 0.82, y: 0.20, width: 0.08, height: 0.08),
            color: Rgba(red: 160, green: 160, blue: 160, alpha: 255)),
        presentationRect: presentationRect,
        worldPatternValidity: validWorldPattern)
    var missingImguiImage = image
    for rect in [
        CGRect(x: 0.020, y: 0.070, width: 0.035, height: 0.015),
        CGRect(x: 0.020, y: 0.085, width: 0.020, height: 0.015),
        CGRect(x: 0.040, y: 0.085, width: 0.015, height: 0.015),
    ] {
        missingImguiImage = paintingNormalizedRect(
            in: missingImguiImage,
            normalizedRect: rect,
            color: Rgba(red: 0, green: 0, blue: 0, alpha: 255))
    }
    let missingImgui = evaluateGameRouteValidity(
        image: missingImguiImage,
        presentationRect: presentationRect,
        worldPatternValidity: validWorldPattern)
    let missingResolution = evaluateGameRouteValidity(
        image: removingSyntheticResolutionMarker(
            from: image,
            presentationRect: presentationRect),
        presentationRect: presentationRect,
        worldPatternValidity: validWorldPattern)

    return !missingWorldPattern.isValid
        && !missingWorldPattern.worldPatternValid
        && !missingCameraStack.isValid
        && !missingCameraStack.cameraStackMarkerPresent
        && !missingPostProcess.isValid
        && !missingPostProcess.postProcessProbePresent
        && !missingImgui.isValid
        && !missingImgui.runtimeImguiPresent
        && !missingResolution.isValid
        && !missingResolution.resolutionMarkerValid
}

private func syntheticGameRouteImage(width: Int, height: Int) -> PixelImage {
    var image = syntheticSolidImage(width: width, height: height, channelValue: 0)
    image = paintingNormalizedRect(
        in: image,
        normalizedRect: CGRect(x: 0.47, y: 0.12, width: 0.06, height: 0.05),
        color: Rgba(red: 255, green: 0, blue: 255, alpha: 255))
    image = paintingNormalizedRect(
        in: image,
        normalizedRect: CGRect(x: 0.82, y: 0.20, width: 0.08, height: 0.08),
        color: Rgba(red: 140, green: 110, blue: 90, alpha: 255))
    image = paintingNormalizedRect(
        in: image,
        normalizedRect: CGRect(x: 0.020, y: 0.070, width: 0.035, height: 0.015),
        color: Rgba(red: 255, green: 255, blue: 255, alpha: 255))
    image = paintingNormalizedRect(
        in: image,
        normalizedRect: CGRect(x: 0.020, y: 0.085, width: 0.020, height: 0.015),
        color: Rgba(red: 0, green: 0, blue: 0, alpha: 255))
    image = paintingNormalizedRect(
        in: image,
        normalizedRect: CGRect(x: 0.040, y: 0.085, width: 0.015, height: 0.015),
        color: Rgba(red: 0, green: 255, blue: 255, alpha: 255))
    return paintingSyntheticResolutionMarker(
        in: image,
        presentationRect: IntRect(x: 0, y: 0, width: width, height: height),
        width: width,
        height: height)
}

private func paintingNormalizedRect(
    in image: PixelImage,
    normalizedRect: CGRect,
    color: Rgba) -> PixelImage {
    var pixels = image.pixels
    let minX = min(max(Int(floor(normalizedRect.minX * Double(image.width))), 0), image.width)
    let maxX = min(max(Int(ceil(normalizedRect.maxX * Double(image.width))), minX), image.width)
    let minY = min(max(Int(floor(normalizedRect.minY * Double(image.height))), 0), image.height)
    let maxY = min(max(Int(ceil(normalizedRect.maxY * Double(image.height))), minY), image.height)
    paintPixels(
        &pixels,
        imageWidth: image.width,
        xRange: minX..<maxX,
        yRange: minY..<maxY,
        color: color)
    return PixelImage(
        width: image.width,
        height: image.height,
        pixels: pixels,
        sourceColorSpace: image.sourceColorSpace)
}

private func paintingSyntheticResolutionMarker(
    in image: PixelImage,
    presentationRect: IntRect,
    width: Int,
    height: Int) -> PixelImage {
    var pixels = image.pixels
    let values = [width, height]
    let sampleY = presentationRect.maxY - 23
    for index in 0..<24 {
        let dimensionIndex = index / 12
        let bitIndex = index % 12
        let cellIndex = index + (index >= 12 ? 1 : 0)
        let sampleX = presentationRect.x + 42 + cellIndex * 8
        let value = (values[dimensionIndex] & (1 << (11 - bitIndex))) != 0
        let color = value
            ? Rgba(red: 0, green: 255, blue: 255, alpha: 255)
            : Rgba(red: 255, green: 0, blue: 255, alpha: 255)
        paintPixels(
            &pixels,
            imageWidth: image.width,
            xRange: (sampleX - 1)...(sampleX + 1),
            yRange: (sampleY - 1)...(sampleY + 1),
            color: color)
    }

    return PixelImage(
        width: image.width,
        height: image.height,
        pixels: pixels,
        sourceColorSpace: image.sourceColorSpace)
}

private func removingSyntheticResolutionMarker(
    from image: PixelImage,
    presentationRect: IntRect) -> PixelImage {
    var pixels = image.pixels
    let sampleY = presentationRect.maxY - 23
    for index in 0..<24 {
        let cellIndex = index + (index >= 12 ? 1 : 0)
        let sampleX = presentationRect.x + 42 + cellIndex * 8
        paintPixels(
            &pixels,
            imageWidth: image.width,
            xRange: (sampleX - 1)...(sampleX + 1),
            yRange: (sampleY - 1)...(sampleY + 1),
            color: Rgba(red: 0, green: 0, blue: 0, alpha: 255))
    }

    return PixelImage(
        width: image.width,
        height: image.height,
        pixels: pixels,
        sourceColorSpace: image.sourceColorSpace)
}

private func paintPixels<RX: Sequence, RY: Sequence>(
    _ pixels: inout [UInt8],
    imageWidth: Int,
    xRange: RX,
    yRange: RY,
    color: Rgba) where RX.Element == Int, RY.Element == Int {
    for y in yRange {
        for x in xRange {
            let offset = (y * imageWidth + x) * 4
            pixels[offset] = color.red
            pixels[offset + 1] = color.green
            pixels[offset + 2] = color.blue
            pixels[offset + 3] = color.alpha
        }
    }
}

private func flipPixelImage(
    _ image: PixelImage,
    horizontally: Bool,
    vertically: Bool) -> PixelImage {
    var pixels = [UInt8](repeating: 0, count: image.pixels.count)
    for y in 0..<image.height {
        for x in 0..<image.width {
            let sourceX = horizontally ? image.width - 1 - x : x
            let sourceY = vertically ? image.height - 1 - y : y
            let sourceOffset = (sourceY * image.width + sourceX) * 4
            let destinationOffset = (y * image.width + x) * 4
            for channel in 0..<4 {
                pixels[destinationOffset + channel] = image.pixels[sourceOffset + channel]
            }
        }
    }

    return PixelImage(
        width: image.width,
        height: image.height,
        pixels: pixels,
        sourceColorSpace: image.sourceColorSpace)
}

private func removingPixels(matching sentinel: Sentinel, from image: PixelImage) -> PixelImage {
    var pixels = image.pixels
    for y in 0..<image.height {
        for x in 0..<image.width where sentinel.matches(image.pixel(x: x, y: y)) {
            let offset = (y * image.width + x) * 4
            pixels[offset] = 0
            pixels[offset + 1] = 0
            pixels[offset + 2] = 0
            pixels[offset + 3] = 255
        }
    }

    return PixelImage(
        width: image.width,
        height: image.height,
        pixels: pixels,
        sourceColorSpace: image.sourceColorSpace)
}

private func syntheticAmbiguousSceneLocatorImage() -> PixelImage {
    let width = 300
    let height = 120
    var pixels = [UInt8](repeating: 0, count: width * height * 4)
    for alphaOffset in stride(from: 3, to: pixels.count, by: 4) {
        pixels[alphaOffset] = 255
    }

    func paint(x: Range<Int>, y: Range<Int>, color: Rgba) {
        for pixelY in y {
            for pixelX in x {
                let offset = (pixelY * width + pixelX) * 4
                pixels[offset] = color.red
                pixels[offset + 1] = color.green
                pixels[offset + 2] = color.blue
                pixels[offset + 3] = color.alpha
            }
        }
    }

    for originX in [10, 160] {
        paint(
            x: originX..<(originX + 8),
            y: 20..<28,
            color: Rgba(red: 0, green: 255, blue: 255, alpha: 255))
        paint(
            x: originX..<(originX + 8),
            y: 92..<100,
            color: Rgba(red: 255, green: 0, blue: 255, alpha: 255))
        paint(
            x: (originX + 112)..<(originX + 120),
            y: 92..<100,
            color: Rgba(red: 255, green: 255, blue: 0, alpha: 255))
    }

    return PixelImage(
        width: width,
        height: height,
        pixels: pixels,
        sourceColorSpace: "synthetic-sRGB")
}

private func rejectsSceneFixture(_ image: PixelImage) -> Bool {
    do {
        _ = try locateSceneFixtureContent(in: image)
        return false
    } catch {
        return true
    }
}

private func syntheticSolidImage(width: Int, height: Int, channelValue: UInt8) -> PixelImage {
    var pixels = [UInt8](repeating: channelValue, count: width * height * 4)
    for alphaOffset in stride(from: 3, to: pixels.count, by: 4) {
        pixels[alphaOffset] = 255
    }

    return PixelImage(
        width: width,
        height: height,
        pixels: pixels,
        sourceColorSpace: "synthetic-sRGB")
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

    try requireFrontmostApplication(processId: processId)

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
    try requireFrontmostApplication(processId: processId)

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
        applicationFrontmost: true,
        capturedAtUtc: iso8601Now())
    try writeJson(metadata, path: metadataPath)
}

private func requireFrontmostApplication(processId: Int32) throws {
    guard NSWorkspace.shared.frontmostApplication?.processIdentifier == processId else {
        throw OracleError.failed(
            "The Unity fixture application is not frontmost; its presentation focus state is not stable yet.")
    }
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
    let artifactFullImage = try PixelImage.load(
        path: artifactPath,
        destinationColorSpaceName: fullImageComparisonColorSpaceName)
    let referenceBeforeFullImage = try PixelImage.load(
        path: referenceBeforePath,
        destinationColorSpaceName: fullImageComparisonColorSpaceName)
    let referenceAfterFullImage = try PixelImage.load(
        path: referenceAfterPath,
        destinationColorSpaceName: fullImageComparisonColorSpaceName)
    let artifactRect = try locateFixtureContent(in: artifact, target: target)
    let referenceBeforeRect = try locateFixtureContent(in: referenceBefore, target: target)
    let referenceAfterRect = try locateFixtureContent(in: referenceAfter, target: target)

    let artifactSamples = sampleColors(image: artifact, contentRect: artifactRect)
    let referenceBeforeSamples = sampleColors(image: referenceBefore, contentRect: referenceBeforeRect)
    let referenceAfterSamples = sampleColors(image: referenceAfter, contentRect: referenceAfterRect)
    let artifactFixtureValidity = validateFixtureSamples(artifactSamples)
    let referenceBeforeFixtureValidity = validateFixtureSamples(referenceBeforeSamples)
    let referenceAfterFixtureValidity = validateFixtureSamples(referenceAfterSamples)
    let artifactPresentationRect = target == "game"
        ? gamePresentationRect(image: artifact, fixtureRect: artifactRect)
        : IntRect(x: 0, y: 0, width: artifact.width, height: artifact.height)
    let referenceBeforePresentationRect = target == "game"
        ? gamePresentationRect(image: referenceBefore, fixtureRect: referenceBeforeRect)
        : referenceBeforeRect
    let referenceAfterPresentationRect = target == "game"
        ? gamePresentationRect(image: referenceAfter, fixtureRect: referenceAfterRect)
        : referenceAfterRect
    let artifactRouteValidity = target == "game"
        ? evaluateGameRouteValidity(
            image: artifact,
            presentationRect: artifactPresentationRect,
            worldPatternValidity: artifactFixtureValidity)
        : nil
    let referenceBeforeRouteValidity = target == "game"
        ? evaluateGameRouteValidity(
            image: referenceBefore,
            presentationRect: referenceBeforePresentationRect,
            worldPatternValidity: referenceBeforeFixtureValidity)
        : nil
    let referenceAfterRouteValidity = target == "game"
        ? evaluateGameRouteValidity(
            image: referenceAfter,
            presentationRect: referenceAfterPresentationRect,
            worldPatternValidity: referenceAfterFixtureValidity)
        : nil
    let stability = compareSamples(referenceBeforeSamples, referenceAfterSamples)
    let fidelity = compareSamples(artifactSamples, referenceAfterSamples)
    let referenceFullImageStability = try compareFullImageRgb(
        left: referenceBeforeFullImage,
        leftRect: referenceBeforePresentationRect,
        right: referenceAfterFullImage,
        rightRect: referenceAfterPresentationRect,
        alphaMaskPolicy: .matchingWindowMasks)
    let artifactFullImageFidelity: FullImageComparisonMetrics?
    if target == "scene" || (expectedWidth == nil && expectedHeight == nil) {
        artifactFullImageFidelity = try compareFullImageRgb(
            left: artifactFullImage,
            leftRect: artifactPresentationRect,
            right: referenceAfterFullImage,
            rightRect: referenceAfterPresentationRect,
            alphaMaskPolicy: .opaqueArtifactAndWindowMask)
    } else {
        artifactFullImageFidelity = nil
    }

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

    if let artifactRouteValidity {
        failures.append(contentsOf: routeFailures(
            validity: artifactRouteValidity,
            imageName: "Artifact"))
    }

    if let referenceBeforeRouteValidity {
        failures.append(contentsOf: routeFailures(
            validity: referenceBeforeRouteValidity,
            imageName: "Before-reference"))
    }

    if let referenceAfterRouteValidity {
        failures.append(contentsOf: routeFailures(
            validity: referenceAfterRouteValidity,
            imageName: "After-reference"))
    }

    let edgeTolerance = 2
    if artifactRect.x > edgeTolerance
        || artifactRect.y > edgeTolerance
        || artifact.width - artifactRect.maxX - 1 > edgeTolerance
        || artifact.height - artifactRect.maxY - 1 > edgeTolerance {
        failures.append("Artifact contains crop padding or content outside the fixture presentation border.")
    }

    if target == "scene" {
        let fullArtifactRect = IntRect(x: 0, y: 0, width: artifact.width, height: artifact.height)
        if artifactRect != fullArtifactRect {
            failures.append(
                "Scene sentinel rectangle does not equal the complete artifact bounds: "
                    + "sentinel \(artifactRect.width)x\(artifactRect.height) "
                    + "at \(artifactRect.x),\(artifactRect.y), "
                    + "artifact \(artifact.width)x\(artifact.height).")
        }

    }

    failures.append(contentsOf: fullImageFailures(
        metrics: referenceFullImageStability,
        comparisonName: "Window presentation before/after stability"))

    if let artifactFullImageFidelity {
        failures.append(contentsOf: fullImageFailures(
            metrics: artifactFullImageFidelity,
            comparisonName: "\(target == "scene" ? "Scene" : "Game") artifact/window fidelity"))
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

    let decodedWidth = artifactRouteValidity?.decodedResolutionWidth
    let decodedHeight = artifactRouteValidity?.decodedResolutionHeight

    let result = AnalysisResult(
        target: target,
        passed: failures.isEmpty,
        artifactWidth: artifact.width,
        artifactHeight: artifact.height,
        artifactColorSpace: artifact.sourceColorSpace,
        referenceBeforeColorSpace: referenceBefore.sourceColorSpace,
        referenceAfterColorSpace: referenceAfter.sourceColorSpace,
        fullImageComparisonColorSpace: fullImageComparisonColorSpaceName as String,
        artifactContentRect: artifactRect,
        referenceBeforeContentRect: referenceBeforeRect,
        referenceAfterContentRect: referenceAfterRect,
        artifactAlphaOpaque: alphaOpaque,
        decodedResolutionWidth: decodedWidth,
        decodedResolutionHeight: decodedHeight,
        artifactFixtureValidity: artifactFixtureValidity,
        referenceBeforeFixtureValidity: referenceBeforeFixtureValidity,
        referenceAfterFixtureValidity: referenceAfterFixtureValidity,
        artifactRouteValidity: artifactRouteValidity,
        referenceBeforeRouteValidity: referenceBeforeRouteValidity,
        referenceAfterRouteValidity: referenceAfterRouteValidity,
        referenceStability: stability,
        artifactFidelity: fidelity,
        referenceFullImageStability: referenceFullImageStability,
        artifactFullImageFidelity: artifactFullImageFidelity,
        failures: failures,
        analyzedAtUtc: iso8601Now())
    try writeJson(result, path: outputPath)
    if !failures.isEmpty {
        throw OracleError.failed("Screenshot fidelity analysis failed; see \(outputPath)")
    }
}

private func routeFailures(
    validity: RouteValidity,
    imageName: String) -> [String] {
    var failures: [String] = []
    if !validity.worldPatternValid {
        failures.append("\(imageName) GameView world-pattern route is missing or invalid.")
    }

    if !validity.cameraStackMarkerPresent {
        failures.append("\(imageName) GameView camera-stack marker is missing.")
    }

    if !validity.postProcessProbePresent {
        failures.append(
            "\(imageName) GameView post-process probe is missing or did not receive the required warm color shift.")
    }

    if !validity.runtimeImguiPresent {
        failures.append("\(imageName) GameView runtime IMGUI signature is missing or invalid.")
    }

    if !validity.resolutionMarkerValid {
        let decodedResolution: String
        if let decodedWidth = validity.decodedResolutionWidth,
           let decodedHeight = validity.decodedResolutionHeight {
            decodedResolution = "\(decodedWidth)x\(decodedHeight)"
        } else {
            decodedResolution = "unavailable"
        }

        failures.append(
            "\(imageName) GameView resolution marker decoded \(decodedResolution); "
                + "expected \(validity.expectedResolutionWidth)x\(validity.expectedResolutionHeight).")
    }

    return failures
}

private func locateFixtureContent(in image: PixelImage, target: String) throws -> IntRect {
    target == "scene"
        ? try locateSceneFixtureContent(in: image)
        : try locateGameFixtureContent(in: image)
}

private func locateGameFixtureContent(in image: PixelImage) throws -> IntRect {
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

private struct SceneSentinelCandidate {
    let cyan: Component
    let magenta: Component
    let yellow: Component
    let rect: IntRect
}

private func locateSceneFixtureContent(in image: PixelImage) throws -> IntRect {
    // Only the outer-edge coordinates are contractual. A sentinel may join a
    // same-colored fixture edge, so component size and fill are not crop inputs.
    let cyanComponents = connectedComponents(in: image, matching: .cyan).filter {
        $0.area >= 16 && $0.maxY - $0.minY + 1 >= 4
    }
    let magentaComponents = connectedComponents(in: image, matching: .magenta).filter {
        $0.area >= 16 && $0.maxY - $0.minY + 1 >= 4
    }
    let yellowComponents = connectedComponents(in: image, matching: .yellow).filter { $0.area >= 16 }
    var candidates: [SceneSentinelCandidate] = []

    for cyan in cyanComponents {
        for magenta in magentaComponents {
            for yellow in yellowComponents {
                let edgeAlignmentTolerance = 2
                guard abs(cyan.minX - magenta.minX) <= edgeAlignmentTolerance,
                      abs(magenta.maxY - yellow.maxY) <= edgeAlignmentTolerance,
                      cyan.minY < magenta.minY,
                      magenta.minX < yellow.minX else {
                    continue
                }

                let x = min(cyan.minX, magenta.minX)
                let y = cyan.minY
                let maxX = yellow.maxX
                let maxY = max(magenta.maxY, yellow.maxY)
                let rect = IntRect(x: x, y: y, width: maxX - x + 1, height: maxY - y + 1)
                guard rect.width >= 100,
                      rect.height >= 80 else {
                    continue
                }

                candidates.append(SceneSentinelCandidate(
                    cyan: cyan,
                    magenta: magenta,
                    yellow: yellow,
                    rect: rect))
            }
        }
    }

    let outermostCandidates: [SceneSentinelCandidate]
    if let minimumY = candidates.map(\.rect.y).min() {
        outermostCandidates = candidates.filter { $0.rect.y == minimumY }
    } else {
        outermostCandidates = []
    }

    guard outermostCandidates.count == 1, let candidate = outermostCandidates.first else {
        let details = candidates.map { describe($0) }.joined(separator: "; ")
        let candidateDetails = details.isEmpty ? "none" : details
        throw OracleError.failed(
            "Expected exactly one outermost Scene fixture rectangle from the outer-edge "
                + "top-left cyan, bottom-left magenta, and bottom-right yellow sentinels; "
                + "found \(outermostCandidates.count) among \(candidates.count) aligned candidates. "
                + "Candidates: \(candidateDetails).")
    }

    return candidate.rect
}

private func describe(_ candidate: SceneSentinelCandidate) -> String {
    "rect=\(candidate.rect.x),\(candidate.rect.y),\(candidate.rect.width)x\(candidate.rect.height); "
        + "cyan=\(describe(candidate.cyan)); magenta=\(describe(candidate.magenta)); "
        + "yellow=\(describe(candidate.yellow))"
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

private func evaluateGameRouteValidity(
    image: PixelImage,
    presentationRect: IntRect,
    worldPatternValidity: FixtureValidity) -> RouteValidity {
    let cameraStackSample = sampleMedianColor(
        image: image,
        contentRect: presentationRect,
        name: "camera-stack-marker",
        normalizedX: 0.5,
        normalizedY: 0.145,
        normalizedRadiusX: 0.01,
        normalizedRadiusY: 0.004)
    let cameraStackMarkerPresent = sampleMatches(
        cameraStackSample,
        sentinel: .magenta)

    let postProcessSample = sampleMedianColor(
        image: image,
        contentRect: presentationRect,
        name: "post-process-probe",
        normalizedX: 0.86,
        normalizedY: 0.24,
        normalizedRadiusX: 0.01,
        normalizedRadiusY: 0.01)
    let postProcessProbePresent = postProcessSample.red - postProcessSample.blue >= 0.03
        && luminance(linearRgb(postProcessSample)) >= minimumColorPatchLuminance

    let imguiWhiteSample = sampleMedianColor(
        image: image,
        contentRect: presentationRect,
        name: "runtime-imgui-white",
        normalizedX: 0.0375,
        normalizedY: 0.0775,
        normalizedRadiusX: 0.004,
        normalizedRadiusY: 0.003)
    let imguiBlackSample = sampleMedianColor(
        image: image,
        contentRect: presentationRect,
        name: "runtime-imgui-black",
        normalizedX: 0.03,
        normalizedY: 0.0925,
        normalizedRadiusX: 0.004,
        normalizedRadiusY: 0.003)
    let imguiCyanSample = sampleMedianColor(
        image: image,
        contentRect: presentationRect,
        name: "runtime-imgui-cyan",
        normalizedX: 0.0475,
        normalizedY: 0.0925,
        normalizedRadiusX: 0.003,
        normalizedRadiusY: 0.003)
    let runtimeImguiPresent = sampleIsWhite(imguiWhiteSample)
        && sampleIsBlack(imguiBlackSample)
        && sampleMatches(imguiCyanSample, sentinel: .cyan)

    let decodedResolution = try? decodeResolutionMarker(
        image: image,
        presentationRect: presentationRect)
    let resolutionMarkerValid = decodedResolution?.width == presentationRect.width
        && decodedResolution?.height == presentationRect.height
    let isValid = worldPatternValidity.isValid
        && cameraStackMarkerPresent
        && postProcessProbePresent
        && runtimeImguiPresent
        && resolutionMarkerValid
    return RouteValidity(
        isValid: isValid,
        worldPatternValid: worldPatternValidity.isValid,
        cameraStackMarkerPresent: cameraStackMarkerPresent,
        cameraStackMarkerSample: cameraStackSample,
        postProcessProbePresent: postProcessProbePresent,
        postProcessProbeSample: postProcessSample,
        runtimeImguiPresent: runtimeImguiPresent,
        runtimeImguiWhiteSample: imguiWhiteSample,
        runtimeImguiBlackSample: imguiBlackSample,
        runtimeImguiCyanSample: imguiCyanSample,
        resolutionMarkerValid: resolutionMarkerValid,
        decodedResolutionWidth: decodedResolution?.width,
        decodedResolutionHeight: decodedResolution?.height,
        expectedResolutionWidth: presentationRect.width,
        expectedResolutionHeight: presentationRect.height)
}

private func sampleMedianColor(
    image: PixelImage,
    contentRect: IntRect,
    name: String,
    normalizedX: Double,
    normalizedY: Double,
    normalizedRadiusX: Double,
    normalizedRadiusY: Double) -> ColorSample {
    let centerX = contentRect.x
        + Int((Double(contentRect.width - 1) * normalizedX).rounded())
    let centerY = contentRect.y
        + Int((Double(contentRect.height - 1) * normalizedY).rounded())
    let radiusX = max(1, Int((Double(contentRect.width) * normalizedRadiusX).rounded()))
    let radiusY = max(1, Int((Double(contentRect.height) * normalizedRadiusY).rounded()))
    let minX = max(contentRect.x, centerX - radiusX)
    let maxX = min(contentRect.maxX, centerX + radiusX)
    let minY = max(contentRect.y, centerY - radiusY)
    let maxY = min(contentRect.maxY, centerY + radiusY)
    var red: [UInt8] = []
    var green: [UInt8] = []
    var blue: [UInt8] = []
    for y in minY...maxY {
        for x in minX...maxX {
            let pixel = image.pixel(x: x, y: y)
            red.append(pixel.red)
            green.append(pixel.green)
            blue.append(pixel.blue)
        }
    }

    return ColorSample(
        name: name,
        red: Double(median(red)) / 255.0,
        green: Double(median(green)) / 255.0,
        blue: Double(median(blue)) / 255.0)
}

private func sampleMatches(_ sample: ColorSample, sentinel: Sentinel) -> Bool {
    sentinel.matches(Rgba(
        red: UInt8((sample.red * 255.0).rounded()),
        green: UInt8((sample.green * 255.0).rounded()),
        blue: UInt8((sample.blue * 255.0).rounded()),
        alpha: 255))
}

private func sampleIsWhite(_ sample: ColorSample) -> Bool {
    min(sample.red, sample.green, sample.blue) >= 0.8
}

private func sampleIsBlack(_ sample: ColorSample) -> Bool {
    max(sample.red, sample.green, sample.blue) <= 0.2
}

private func gamePresentationRect(
    image: PixelImage,
    fixtureRect: IntRect) -> IntRect {
    let presentationY = max(0, fixtureRect.y - 1)
    return IntRect(
        x: 0,
        y: presentationY,
        width: image.width,
        height: image.height - presentationY)
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

// The full-image benchmark compares corresponding physical pixels after ImageIO
// has color-managed both inputs into decoded sRGB8. Each observation is one
// absolute R, G, or B channel delta divided by 255; alpha has its own opaque
// artifact contract. A WindowServer non-opaque pixel is excluded only when its
// binary alpha mask is confined to corner-connected components inside the fixed
// coverage-derived corner envelope. Before/after WindowServer masks must match.
// p95 is the nearest-rank 95th percentile over all compared channel observations.
private func compareFullImageRgb(
    left: PixelImage,
    leftRect: IntRect,
    right: PixelImage,
    rightRect: IntRect,
    alphaMaskPolicy: FullImageAlphaMaskPolicy) throws -> FullImageComparisonMetrics {
    let dimensionsMatch = leftRect.width == rightRect.width
        && leftRect.height == rightRect.height
    guard dimensionsMatch else {
        return FullImageComparisonMetrics(
            dimensionsMatch: false,
            leftWidth: leftRect.width,
            leftHeight: leftRect.height,
            rightWidth: rightRect.width,
            rightHeight: rightRect.height,
            totalPixelCount: 0,
            comparedPixelCount: 0,
            excludedNonOpaquePixelCount: 0,
            leftNonOpaquePixelCount: 0,
            rightNonOpaquePixelCount: 0,
            alphaMasksMatch: false,
            alphaMaskTopologyValid: false,
            matchingAlphaMasksRequired: alphaMaskPolicy == .matchingWindowMasks,
            comparedRgbChannelCount: 0,
            meanAbsoluteRgbChannelErrorNormalized: nil,
            percentile95AbsoluteRgbChannelErrorNormalized: nil,
            maximumAbsoluteRgbChannelErrorNormalized: nil)
    }

    guard leftRect.x >= 0,
          leftRect.y >= 0,
          leftRect.maxX < left.width,
          leftRect.maxY < left.height,
          rightRect.x >= 0,
          rightRect.y >= 0,
          rightRect.maxX < right.width,
          rightRect.maxY < right.height else {
        throw OracleError.failed("Full-image comparison rectangle exceeds a decoded image boundary.")
    }

    let totalPixelCount = try checkedMultiply(leftRect.width, leftRect.height)
    let leftNonOpaqueMask = nonOpaqueMask(image: left, rect: leftRect)
    let rightNonOpaqueMask = nonOpaqueMask(image: right, rect: rightRect)
    let leftNonOpaquePixelCount = leftNonOpaqueMask.reduce(0) { $0 + ($1 ? 1 : 0) }
    let rightNonOpaquePixelCount = rightNonOpaqueMask.reduce(0) { $0 + ($1 ? 1 : 0) }
    let alphaMasksMatch = leftNonOpaqueMask == rightNonOpaqueMask
    let leftMaskTopologyValid = cornerMaskIsValid(
        leftNonOpaqueMask,
        width: leftRect.width,
        height: leftRect.height)
    let rightMaskTopologyValid = cornerMaskIsValid(
        rightNonOpaqueMask,
        width: rightRect.width,
        height: rightRect.height)
    let alphaMaskTopologyValid: Bool
    switch alphaMaskPolicy {
    case .matchingWindowMasks:
        alphaMaskTopologyValid = leftMaskTopologyValid && rightMaskTopologyValid
    case .opaqueArtifactAndWindowMask:
        alphaMaskTopologyValid = leftNonOpaquePixelCount == 0 && rightMaskTopologyValid
    }

    var histogram = [Int](repeating: 0, count: 256)
    var absoluteErrorSum: UInt64 = 0
    var maximumAbsoluteError = 0
    var comparedPixelCount = 0
    var excludedNonOpaquePixelCount = 0
    for relativeY in 0..<leftRect.height {
        for relativeX in 0..<leftRect.width {
            let relativeIndex = relativeY * leftRect.width + relativeX
            let leftPixel = left.pixel(
                x: leftRect.x + relativeX,
                y: leftRect.y + relativeY)
            let rightPixel = right.pixel(
                x: rightRect.x + relativeX,
                y: rightRect.y + relativeY)
            if leftNonOpaqueMask[relativeIndex] || rightNonOpaqueMask[relativeIndex] {
                excludedNonOpaquePixelCount += 1
                continue
            }

            comparedPixelCount += 1
            let channelErrors = [
                abs(Int(leftPixel.red) - Int(rightPixel.red)),
                abs(Int(leftPixel.green) - Int(rightPixel.green)),
                abs(Int(leftPixel.blue) - Int(rightPixel.blue)),
            ]
            for error in channelErrors {
                histogram[error] += 1
                absoluteErrorSum += UInt64(error)
                maximumAbsoluteError = max(maximumAbsoluteError, error)
            }
        }
    }

    let channelCount = try checkedMultiply(comparedPixelCount, 3)
    guard channelCount > 0 else {
        return FullImageComparisonMetrics(
            dimensionsMatch: true,
            leftWidth: leftRect.width,
            leftHeight: leftRect.height,
            rightWidth: rightRect.width,
            rightHeight: rightRect.height,
            totalPixelCount: totalPixelCount,
            comparedPixelCount: 0,
            excludedNonOpaquePixelCount: excludedNonOpaquePixelCount,
            leftNonOpaquePixelCount: leftNonOpaquePixelCount,
            rightNonOpaquePixelCount: rightNonOpaquePixelCount,
            alphaMasksMatch: alphaMasksMatch,
            alphaMaskTopologyValid: alphaMaskTopologyValid,
            matchingAlphaMasksRequired: alphaMaskPolicy == .matchingWindowMasks,
            comparedRgbChannelCount: 0,
            meanAbsoluteRgbChannelErrorNormalized: nil,
            percentile95AbsoluteRgbChannelErrorNormalized: nil,
            maximumAbsoluteRgbChannelErrorNormalized: nil)
    }

    let percentile95Rank = Int(ceil(Double(channelCount) * 0.95))
    var cumulativeCount = 0
    var percentile95AbsoluteError = 0
    for error in 0..<histogram.count {
        cumulativeCount += histogram[error]
        if cumulativeCount >= percentile95Rank {
            percentile95AbsoluteError = error
            break
        }
    }

    return FullImageComparisonMetrics(
        dimensionsMatch: true,
        leftWidth: leftRect.width,
        leftHeight: leftRect.height,
        rightWidth: rightRect.width,
        rightHeight: rightRect.height,
        totalPixelCount: totalPixelCount,
        comparedPixelCount: comparedPixelCount,
        excludedNonOpaquePixelCount: excludedNonOpaquePixelCount,
        leftNonOpaquePixelCount: leftNonOpaquePixelCount,
        rightNonOpaquePixelCount: rightNonOpaquePixelCount,
        alphaMasksMatch: alphaMasksMatch,
        alphaMaskTopologyValid: alphaMaskTopologyValid,
        matchingAlphaMasksRequired: alphaMaskPolicy == .matchingWindowMasks,
        comparedRgbChannelCount: channelCount,
        meanAbsoluteRgbChannelErrorNormalized: Double(absoluteErrorSum) / Double(channelCount) / 255.0,
        percentile95AbsoluteRgbChannelErrorNormalized: Double(percentile95AbsoluteError) / 255.0,
        maximumAbsoluteRgbChannelErrorNormalized: Double(maximumAbsoluteError) / 255.0)
}

private func nonOpaqueMask(image: PixelImage, rect: IntRect) -> [Bool] {
    var mask = [Bool](repeating: false, count: rect.width * rect.height)
    for relativeY in 0..<rect.height {
        for relativeX in 0..<rect.width {
            mask[relativeY * rect.width + relativeX] = image.pixel(
                x: rect.x + relativeX,
                y: rect.y + relativeY).alpha != 255
        }
    }

    return mask
}

private func cornerMaskIsValid(_ mask: [Bool], width: Int, height: Int) -> Bool {
    let maskedPixelCount = mask.reduce(0) { $0 + ($1 ? 1 : 0) }
    if maskedPixelCount == 0 {
        return true
    }

    let totalPixelCount = width * height
    let maximumExcludedPixelCount = Double(totalPixelCount) * (1.0 - minimumFullImageOpaquePixelCoverage)
    let cornerInsetLimit = max(1, Int(ceil(sqrt(maximumExcludedPixelCount))))
    for index in mask.indices where mask[index] {
        let x = index % width
        let y = index / width
        let nearHorizontalEdge = x < cornerInsetLimit || width - 1 - x < cornerInsetLimit
        let nearVerticalEdge = y < cornerInsetLimit || height - 1 - y < cornerInsetLimit
        if !nearHorizontalEdge || !nearVerticalEdge {
            return false
        }
    }

    var visited = [Bool](repeating: false, count: mask.count)
    for startIndex in mask.indices where mask[startIndex] && !visited[startIndex] {
        var queue = [startIndex]
        var queueIndex = 0
        var touchesCorner = false
        visited[startIndex] = true
        while queueIndex < queue.count {
            let index = queue[queueIndex]
            queueIndex += 1
            let x = index % width
            let y = index / width
            if (x == 0 || x == width - 1) && (y == 0 || y == height - 1) {
                touchesCorner = true
            }

            let neighbors = [
                (x - 1, y),
                (x + 1, y),
                (x, y - 1),
                (x, y + 1),
            ]
            for (neighborX, neighborY) in neighbors {
                if neighborX < 0 || neighborY < 0 || neighborX >= width || neighborY >= height {
                    continue
                }

                let neighborIndex = neighborY * width + neighborX
                if mask[neighborIndex] && !visited[neighborIndex] {
                    visited[neighborIndex] = true
                    queue.append(neighborIndex)
                }
            }
        }

        if !touchesCorner {
            return false
        }
    }

    return true
}

private func fullImageFailures(
    metrics: FullImageComparisonMetrics,
    comparisonName: String) -> [String] {
    guard metrics.dimensionsMatch else {
        return [
            "\(comparisonName) physical dimensions differ: "
                + "\(metrics.leftWidth)x\(metrics.leftHeight) versus "
                + "\(metrics.rightWidth)x\(metrics.rightHeight).",
        ]
    }

    guard let meanError = metrics.meanAbsoluteRgbChannelErrorNormalized,
          let percentile95Error = metrics.percentile95AbsoluteRgbChannelErrorNormalized,
          let maximumError = metrics.maximumAbsoluteRgbChannelErrorNormalized else {
        return ["\(comparisonName) full-image RGB metrics are unavailable."]
    }

    var failures: [String] = []
    if !metrics.alphaMaskTopologyValid {
        failures.append(
            "\(comparisonName) contains a non-opaque mask outside a corner-connected WindowServer mask envelope.")
    }

    if metrics.matchingAlphaMasksRequired && !metrics.alphaMasksMatch {
        failures.append(
            "\(comparisonName) uses different non-opaque WindowServer masks before and after capture.")
    }

    let opaqueCoverage = metrics.totalPixelCount == 0
        ? 0
        : Double(metrics.comparedPixelCount) / Double(metrics.totalPixelCount)
    if opaqueCoverage < minimumFullImageOpaquePixelCoverage {
        failures.append(String(
            format: "%@ color-managed WindowServer coverage %.5f is below the fixed %.5f minimum; %d pixels were excluded because the OS window mask made them non-opaque.",
            comparisonName,
            opaqueCoverage,
            minimumFullImageOpaquePixelCoverage,
            metrics.excludedNonOpaquePixelCount))
    }

    if meanError > maximumFullImageMeanAbsoluteRgbChannelErrorNormalized {
        failures.append(String(
            format: "%@ mean absolute RGB channel error %.3f/255 exceeds the fixed 0.500/255 limit.",
            comparisonName,
            meanError * 255.0))
    }

    if percentile95Error > maximumFullImagePercentile95AbsoluteRgbChannelErrorNormalized {
        failures.append(String(
            format: "%@ p95 absolute RGB channel error %.3f/255 exceeds the fixed 2.000/255 limit.",
            comparisonName,
            percentile95Error * 255.0))
    }

    if maximumError > maximumFullImageAbsoluteRgbChannelErrorNormalized {
        failures.append(String(
            format: "%@ maximum absolute RGB channel error %.3f/255 exceeds the fixed 4.000/255 limit.",
            comparisonName,
            maximumError * 255.0))
    }

    return failures
}

private func decodeResolutionMarker(
    image: PixelImage,
    presentationRect: IntRect) throws -> (width: Int, height: Int) {
    var bits: [Bool] = []
    let sampleY = presentationRect.maxY - 23
    guard sampleY >= presentationRect.y else {
        throw OracleError.failed("Resolution marker does not fit inside the GameView presentation.")
    }

    for index in 0..<24 {
        let cellIndex = index + (index >= 12 ? 1 : 0)
        let sampleX = presentationRect.x + 42 + cellIndex * 8
        guard sampleX <= presentationRect.maxX else {
            throw OracleError.failed("Resolution marker does not fit inside the GameView presentation.")
        }

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

    return (width: decode(0..<12), height: decode(12..<24))
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
