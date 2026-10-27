$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase

$assetDirectory = Join-Path $PSScriptRoot '..\src\TaskbarMonitor.App\Assets'
New-Item -ItemType Directory -Force -Path $assetDirectory | Out-Null

function New-LogoPng([int]$size) {
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $context = $visual.RenderOpen()
    $context.PushTransform([System.Windows.Media.ScaleTransform]::new($size / 256.0, $size / 256.0))

    $navy = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0x0F, 0x17, 0x2A))
    $screen = [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0x11, 0x18, 0x27))
    $border = [System.Windows.Media.Pen]::new([System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0x33, 0x41, 0x55)), 3)
    $context.DrawRoundedRectangle($navy, $null, [System.Windows.Rect]::new(34, 43, 188, 139), 28, 28)
    $context.DrawRoundedRectangle($screen, $border, [System.Windows.Rect]::new(45, 54, 166, 117), 19, 19)

    $pulse = [System.Windows.Media.StreamGeometry]::new()
    $path = $pulse.Open()
    $path.BeginFigure([System.Windows.Point]::new(66, 115), $false, $false)
    foreach ($point in @(
        [System.Windows.Point]::new(94, 115),
        [System.Windows.Point]::new(107, 88),
        [System.Windows.Point]::new(126, 141),
        [System.Windows.Point]::new(145, 98),
        [System.Windows.Point]::new(158, 115),
        [System.Windows.Point]::new(190, 115)
    )) { $path.LineTo($point, $true, $false) }
    $path.Close()
    $gradient = [System.Windows.Media.LinearGradientBrush]::new(
        [System.Windows.Media.Color]::FromRgb(0x06, 0xB6, 0xD4),
        [System.Windows.Media.Color]::FromRgb(0x10, 0xB9, 0x81),
        [System.Windows.Point]::new(55, 128), [System.Windows.Point]::new(201, 128))
    $pulsePen = [System.Windows.Media.Pen]::new($gradient, 10)
    $pulsePen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $pulsePen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $pulsePen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
    $context.DrawGeometry($null, $pulsePen, $pulse)

    $context.DrawEllipse([System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0xA7, 0xF3, 0xD0)), $null, [System.Windows.Point]::new(190, 115), 7, 7)
    $stand = [System.Windows.Media.StreamGeometry]::new()
    $standPath = $stand.Open()
    $standPath.BeginFigure([System.Windows.Point]::new(104, 194), $false, $false)
    $standPath.LineTo([System.Windows.Point]::new(104, 205), $true, $false)
    $standPath.BeginFigure([System.Windows.Point]::new(152, 194), $false, $false)
    $standPath.LineTo([System.Windows.Point]::new(152, 205), $true, $false)
    $standPath.BeginFigure([System.Windows.Point]::new(86, 211), $false, $false)
    $standPath.LineTo([System.Windows.Point]::new(170, 211), $true, $false)
    $standPath.Close()
    $standPen = [System.Windows.Media.Pen]::new($navy, 11)
    $standPen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $standPen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $context.DrawGeometry($null, $standPen, $stand)

    $standHighlight = [System.Windows.Media.StreamGeometry]::new()
    $highlightPath = $standHighlight.Open()
    $highlightPath.BeginFigure([System.Windows.Point]::new(89, 190), $false, $false)
    $highlightPath.LineTo([System.Windows.Point]::new(167, 190), $true, $false)
    $highlightPath.Close()
    $highlightPen = [System.Windows.Media.Pen]::new([System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(0x64, 0x74, 0x8B)), 8)
    $highlightPen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
    $highlightPen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
    $context.DrawGeometry($null, $highlightPen, $standHighlight)

    $context.Pop()
    $context.Close()
    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.MemoryStream]::new()
    $encoder.Save($stream)
    $bytes = $stream.ToArray()
    $stream.Dispose()
    return ,$bytes
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @($sizes | ForEach-Object { [pscustomobject]@{ Size = $_; Bytes = (New-LogoPng $_) } })
[System.IO.File]::WriteAllBytes((Join-Path $assetDirectory 'taskbar-monitor-logo.png'), $images[-1].Bytes)

$iconStream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($iconStream)
$writer.Write([UInt16]0)
$writer.Write([UInt16]1)
$writer.Write([UInt16]$images.Count)
$offset = 6 + (16 * $images.Count)
foreach ($image in $images) {
    $dimension = if ($image.Size -eq 256) { [byte]0 } else { [byte]$image.Size }
    $writer.Write($dimension)
    $writer.Write($dimension)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]32)
    $writer.Write([UInt32]$image.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $image.Bytes.Length
}
foreach ($image in $images) { $writer.Write($image.Bytes) }
$writer.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $assetDirectory 'taskbar-monitor-logo.ico'), $iconStream.ToArray())
$writer.Dispose()
$iconStream.Dispose()
Write-Output "Generated multi-size app icon in $assetDirectory"
