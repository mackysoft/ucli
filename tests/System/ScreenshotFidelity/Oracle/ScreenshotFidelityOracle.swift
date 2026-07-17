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
            throw OracleError.failed(
                "Decoded image is not RGB: \(path), colorSpace=\(sourceColorSpaceName)")
        }

        guard let destinationColorSpace = CGColorSpace(name: destinationColorSpaceName) else {
            throw OracleError.failed(
                "CoreGraphics could not create comparison color space: \(destinationColorSpaceName)")
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
                "CoreGraphics could not create an RGBA8 decode context for \(path).")
        }

        context.interpolationQuality = .none
        context.draw(image, in: CGRect(x: 0, y: 0, width: image.width, height: image.height))
        return PixelImage(
            width: image.width,
            height: image.height,
            pixels: pixels,
            sourceColorSpace: sourceColorSpaceName)
    }
}

private struct IntRect: Codable {
    let x: Int
    let y: Int
    let width: Int
    let height: Int

    var maxX: Int { x + width - 1 }
    var maxY: Int { y + height - 1 }
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
    let alphaMaskTopologyValid: Bool
    let comparedRgbChannelCount: Int
    let meanAbsoluteRgbChannelErrorNormalized: Double?
    let percentile95AbsoluteRgbChannelErrorNormalized: Double?
    let maximumAbsoluteRgbChannelErrorNormalized: Double?
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
    let artifactAlphaOpaque: Bool
    let referenceWidth: Int?
    let referenceHeight: Int?
    let referenceColorSpace: String?
    let comparisonColorSpace: String?
    let artifactContentRect: IntRect
    let referenceContentRect: IntRect?
    let fullImageFidelity: FullImageComparisonMetrics?
    let failures: [String]
    let analyzedAtUtc: String
}

private struct SelfCheckResult: Codable {
    let passed: Bool
    let identicalImageAccepted: Bool
    let changedImageRejected: Bool
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
            throw OracleError.invalidArguments(
                "Expected command: environment, self-check, capture, or analyze.")
        }

        var values: [String: String] = [:]
        var index = 1
        while index < arguments.count {
            let name = arguments[index]
            guard name.hasPrefix("--"), index + 1 < arguments.count else {
                throw OracleError.invalidArguments(
                    "Expected --name value argument pair. Actual: \(name)")
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

    func optional(_ name: String) -> String? {
        guard let value = values[name], !value.isEmpty else {
            return nil
        }

        return value
    }

    func optionalInt(_ name: String) throws -> Int? {
        guard let value = optional(name) else {
            return nil
        }

        guard let parsed = Int(value) else {
            throw OracleError.invalidArguments(
                "Argument --\(name) must be an integer. Actual: \(value)")
        }

        return parsed
    }
}

private let comparisonColorSpaceName = CGColorSpace.displayP3
private let maximumMeanAbsoluteRgbChannelErrorNormalized = 0.5 / 255.0
private let maximumPercentile95AbsoluteRgbChannelErrorNormalized = 2.0 / 255.0
private let maximumAbsoluteRgbChannelErrorNormalized = 4.0 / 255.0
private let minimumOpaquePixelCoverage = 0.999

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
            referencePath: arguments.optional("reference"),
            expectedWidth: arguments.optionalInt("expected-width"),
            expectedHeight: arguments.optionalInt("expected-height"),
            outputPath: arguments.required("output"))
    default:
        throw OracleError.invalidArguments("Unknown command: \(arguments.command)")
    }
}

private func runSelfCheck(outputPath: String) throws {
    let baseline = syntheticImage(width: 100, height: 100, channelValue: 96)
    let rect = IntRect(x: 0, y: 0, width: baseline.width, height: baseline.height)
    let identicalMetrics = try compareFullImageRgb(
        left: baseline,
        leftRect: rect,
        right: baseline,
        rightRect: rect)
    let identicalImageAccepted = fullImageFailures(
        metrics: identicalMetrics,
        comparisonName: "self-check identical image").isEmpty

    var changedPixels = baseline.pixels
    changedPixels[(50 * baseline.width + 50) * 4] = 255
    let changed = PixelImage(
        width: baseline.width,
        height: baseline.height,
        pixels: changedPixels,
        sourceColorSpace: baseline.sourceColorSpace)
    let changedMetrics = try compareFullImageRgb(
        left: baseline,
        leftRect: rect,
        right: changed,
        rightRect: rect)
    let changedImageRejected = !fullImageFailures(
        metrics: changedMetrics,
        comparisonName: "self-check changed image").isEmpty
    let passed = identicalImageAccepted && changedImageRejected
    try writeJson(
        SelfCheckResult(
            passed: passed,
            identicalImageAccepted: identicalImageAccepted,
            changedImageRejected: changedImageRejected,
            checkedAtUtc: iso8601Now()),
        path: outputPath)
    if !passed {
        throw OracleError.failed("Screenshot surface oracle self-check failed; see \(outputPath)")
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

    try writeJson(
        EnvironmentMetadata(
            observedAtUtc: iso8601Now(),
            operatingSystem: ProcessInfo.processInfo.operatingSystemVersionString,
            architecture: architectureName(),
            screenCapturePermissionGranted: CGPreflightScreenCaptureAccess(),
            screenLocked: isScreenLocked(),
            displays: displays),
        path: outputPath)
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
        throw OracleError.failed(
            "screencapture failed with exit \(process.terminationStatus): \(error)")
    }

    let image = try PixelImage.load(path: outputPath)
    let visiblePixelCount = stride(from: 0, to: image.pixels.count, by: 4).reduce(into: 0) {
        count,
        offset in
        if image.pixels[offset + 3] != 0
            && (image.pixels[offset] != 0
                || image.pixels[offset + 1] != 0
                || image.pixels[offset + 2] != 0) {
            count += 1
        }
    }
    guard visiblePixelCount > max(64, image.width * image.height / 100) else {
        throw OracleError.failed(
            "WindowServer capture is empty, transparent, or blocked by privacy controls.")
    }

    try writeJson(
        WindowMetadata(
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
            capturedAtUtc: iso8601Now()),
        path: metadataPath)
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
    referencePath: String?,
    expectedWidth: Int?,
    expectedHeight: Int?,
    outputPath: String) throws {
    guard target == "game" || target == "scene" else {
        throw OracleError.invalidArguments("--target must be game or scene.")
    }

    guard (expectedWidth == nil) == (expectedHeight == nil) else {
        throw OracleError.invalidArguments(
            "--expected-width and --expected-height must be specified together.")
    }

    let artifact = try PixelImage.load(
        path: artifactPath,
        destinationColorSpaceName: comparisonColorSpaceName)
    let artifactRect = IntRect(
        x: 0,
        y: 0,
        width: artifact.width,
        height: artifact.height)
    let artifactAlphaOpaque = stride(from: 3, to: artifact.pixels.count, by: 4)
        .allSatisfy { artifact.pixels[$0] == 255 }
    var failures: [String] = []
    if !artifactAlphaOpaque {
        failures.append("Screenshot artifact contains non-opaque pixels.")
    }
    if try !pngContainsSrgbChunk(path: artifactPath) {
        failures.append("Screenshot artifact PNG does not contain an sRGB chunk before IDAT.")
    }

    var reference: PixelImage?
    var referenceRect: IntRect?
    var fullImageFidelity: FullImageComparisonMetrics?
    if let expectedWidth, let expectedHeight {
        if artifact.width != expectedWidth || artifact.height != expectedHeight {
            failures.append(
                "Artifact dimensions \(artifact.width)x\(artifact.height) do not equal requested dimensions \(expectedWidth)x\(expectedHeight).")
        }
    } else {
        guard let referencePath else {
            throw OracleError.invalidArguments(
                "Current-surface analysis requires --reference.")
        }

        let decodedReference = try PixelImage.load(
            path: referencePath,
            destinationColorSpaceName: comparisonColorSpaceName)
        reference = decodedReference
        if decodedReference.width != artifact.width
            || decodedReference.height < artifact.height {
            failures.append(
                "WindowServer image \(decodedReference.width)x\(decodedReference.height) cannot contain the artifact surface \(artifact.width)x\(artifact.height).")
        } else {
            let resolvedReferenceRect = IntRect(
                x: 0,
                y: decodedReference.height - artifact.height,
                width: artifact.width,
                height: artifact.height)
            referenceRect = resolvedReferenceRect
            let metrics = try compareFullImageRgb(
                left: artifact,
                leftRect: artifactRect,
                right: decodedReference,
                rightRect: resolvedReferenceRect)
            fullImageFidelity = metrics
            failures.append(contentsOf: fullImageFailures(
                metrics: metrics,
                comparisonName: "\(target) artifact and WindowServer surface"))
        }
    }

    let result = AnalysisResult(
        target: target,
        passed: failures.isEmpty,
        artifactWidth: artifact.width,
        artifactHeight: artifact.height,
        artifactColorSpace: artifact.sourceColorSpace,
        artifactAlphaOpaque: artifactAlphaOpaque,
        referenceWidth: reference?.width,
        referenceHeight: reference?.height,
        referenceColorSpace: reference?.sourceColorSpace,
        comparisonColorSpace: reference == nil
            ? nil
            : CGColorSpace(name: comparisonColorSpaceName)?.name as String?,
        artifactContentRect: artifactRect,
        referenceContentRect: referenceRect,
        fullImageFidelity: fullImageFidelity,
        failures: failures,
        analyzedAtUtc: iso8601Now())
    try writeJson(result, path: outputPath)
    if !result.passed {
        throw OracleError.failed("Screenshot surface analysis failed; see \(outputPath)")
    }
}

private func compareFullImageRgb(
    left: PixelImage,
    leftRect: IntRect,
    right: PixelImage,
    rightRect: IntRect) throws -> FullImageComparisonMetrics {
    let dimensionsMatch = leftRect.width == rightRect.width
        && leftRect.height == rightRect.height
    guard dimensionsMatch else {
        return emptyComparisonMetrics(leftRect: leftRect, rightRect: rightRect)
    }

    guard rectIsInside(leftRect, image: left), rectIsInside(rightRect, image: right) else {
        throw OracleError.failed(
            "Full-image comparison rectangle exceeds a decoded image boundary.")
    }

    let totalPixelCount = try checkedMultiply(leftRect.width, leftRect.height)
    let leftNonOpaqueMask = nonOpaqueMask(image: left, rect: leftRect)
    let rightNonOpaqueMask = nonOpaqueMask(image: right, rect: rightRect)
    let leftNonOpaquePixelCount = leftNonOpaqueMask.reduce(0) { $0 + ($1 ? 1 : 0) }
    let rightNonOpaquePixelCount = rightNonOpaqueMask.reduce(0) { $0 + ($1 ? 1 : 0) }
    let alphaMaskTopologyValid = leftNonOpaquePixelCount == 0
        && cornerMaskIsValid(
            rightNonOpaqueMask,
            width: rightRect.width,
            height: rightRect.height)

    var histogram = [Int](repeating: 0, count: 256)
    var absoluteErrorSum: UInt64 = 0
    var maximumAbsoluteError = 0
    var comparedPixelCount = 0
    var excludedNonOpaquePixelCount = 0
    for relativeY in 0..<leftRect.height {
        for relativeX in 0..<leftRect.width {
            let relativeIndex = relativeY * leftRect.width + relativeX
            if leftNonOpaqueMask[relativeIndex] || rightNonOpaqueMask[relativeIndex] {
                excludedNonOpaquePixelCount += 1
                continue
            }

            comparedPixelCount += 1
            let leftOffset = ((leftRect.y + relativeY) * left.width
                + leftRect.x + relativeX) * 4
            let rightOffset = ((rightRect.y + relativeY) * right.width
                + rightRect.x + relativeX) * 4
            for channel in 0..<3 {
                let error = abs(
                    Int(left.pixels[leftOffset + channel])
                        - Int(right.pixels[rightOffset + channel]))
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
            alphaMaskTopologyValid: alphaMaskTopologyValid,
            comparedRgbChannelCount: 0,
            meanAbsoluteRgbChannelErrorNormalized: nil,
            percentile95AbsoluteRgbChannelErrorNormalized: nil,
            maximumAbsoluteRgbChannelErrorNormalized: nil)
    }

    let percentile95Rank = Int(ceil(Double(channelCount) * 0.95))
    var cumulativeCount = 0
    var percentile95AbsoluteError = 0
    for error in histogram.indices {
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
        alphaMaskTopologyValid: alphaMaskTopologyValid,
        comparedRgbChannelCount: channelCount,
        meanAbsoluteRgbChannelErrorNormalized:
            Double(absoluteErrorSum) / Double(channelCount) / 255.0,
        percentile95AbsoluteRgbChannelErrorNormalized:
            Double(percentile95AbsoluteError) / 255.0,
        maximumAbsoluteRgbChannelErrorNormalized:
            Double(maximumAbsoluteError) / 255.0)
}

private func emptyComparisonMetrics(
    leftRect: IntRect,
    rightRect: IntRect) -> FullImageComparisonMetrics {
    FullImageComparisonMetrics(
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
        alphaMaskTopologyValid: false,
        comparedRgbChannelCount: 0,
        meanAbsoluteRgbChannelErrorNormalized: nil,
        percentile95AbsoluteRgbChannelErrorNormalized: nil,
        maximumAbsoluteRgbChannelErrorNormalized: nil)
}

private func rectIsInside(_ rect: IntRect, image: PixelImage) -> Bool {
    rect.width > 0
        && rect.height > 0
        && rect.x >= 0
        && rect.y >= 0
        && rect.maxX < image.width
        && rect.maxY < image.height
}

private func nonOpaqueMask(image: PixelImage, rect: IntRect) -> [Bool] {
    var mask = [Bool](repeating: false, count: rect.width * rect.height)
    for relativeY in 0..<rect.height {
        for relativeX in 0..<rect.width {
            let imageOffset = ((rect.y + relativeY) * image.width
                + rect.x + relativeX) * 4
            mask[relativeY * rect.width + relativeX] = image.pixels[imageOffset + 3] != 255
        }
    }

    return mask
}

private func cornerMaskIsValid(_ mask: [Bool], width: Int, height: Int) -> Bool {
    let maskedPixelCount = mask.reduce(0) { $0 + ($1 ? 1 : 0) }
    if maskedPixelCount == 0 {
        return true
    }

    let maximumExcludedPixelCount = Double(width * height) * (1.0 - minimumOpaquePixelCoverage)
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

            for (neighborX, neighborY) in [
                (x - 1, y),
                (x + 1, y),
                (x, y - 1),
                (x, y + 1),
            ] {
                if neighborX < 0 || neighborY < 0
                    || neighborX >= width || neighborY >= height {
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
            "\(comparisonName) has non-opaque artifact pixels or a reference mask outside the window corners.")
    }

    let opaqueCoverage = metrics.totalPixelCount == 0
        ? 0
        : Double(metrics.comparedPixelCount) / Double(metrics.totalPixelCount)
    if opaqueCoverage < minimumOpaquePixelCoverage {
        failures.append(String(
            format: "%@ WindowServer coverage %.5f is below the fixed %.5f minimum.",
            comparisonName,
            opaqueCoverage,
            minimumOpaquePixelCoverage))
    }

    if meanError > maximumMeanAbsoluteRgbChannelErrorNormalized {
        failures.append(String(
            format: "%@ mean absolute RGB error %.3f/255 exceeds 0.500/255.",
            comparisonName,
            meanError * 255.0))
    }

    if percentile95Error > maximumPercentile95AbsoluteRgbChannelErrorNormalized {
        failures.append(String(
            format: "%@ p95 absolute RGB error %.3f/255 exceeds 2.000/255.",
            comparisonName,
            percentile95Error * 255.0))
    }

    if maximumError > maximumAbsoluteRgbChannelErrorNormalized {
        failures.append(String(
            format: "%@ maximum absolute RGB error %.3f/255 exceeds 4.000/255.",
            comparisonName,
            maximumError * 255.0))
    }

    return failures
}

private func syntheticImage(
    width: Int,
    height: Int,
    channelValue: UInt8) -> PixelImage {
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
    data[offset..<(offset + 4)].reduce(UInt32(0)) { value, byte in
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
