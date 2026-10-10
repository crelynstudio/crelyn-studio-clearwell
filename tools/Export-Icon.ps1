param([string]$Assets = (Join-Path $PSScriptRoot '..\src\Clearwell\Assets'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, PresentationFramework, WindowsBase
$assetsPath = (Resolve-Path -LiteralPath $Assets).Path
$inputStream = [IO.File]::OpenRead((Join-Path $assetsPath 'Brand.xaml'))
try { $resources = [Windows.Markup.XamlReader]::Load($inputStream) } finally { $inputStream.Dispose() }
$frames = @()
foreach ($size in @(16, 24, 32, 48, 64, 256)) {
    $visual = New-Object Windows.Media.DrawingVisual
    $context = $visual.RenderOpen()
    $context.DrawImage($resources['ClearwellMark'], (New-Object Windows.Rect(0, 0, $size, $size)))
    $context.Close()
    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, ([Windows.Media.PixelFormats]::Pbgra32))
    $bitmap.Render($visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object IO.MemoryStream
    $encoder.Save($stream)
    $frames += [pscustomobject]@{ Size = $size; Data = $stream.ToArray() }
    $stream.Dispose()
}
$output = [IO.File]::Create((Join-Path $assetsPath 'Clearwell.ico'))
$writer = New-Object IO.BinaryWriter($output)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]$frame.Data.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Data.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
} finally { $writer.Dispose() }
[IO.File]::WriteAllBytes((Join-Path $assetsPath 'Clearwell-mark.png'), $frames[-1].Data)
Write-Output 'Exported 16/24/32/48/64/256px ICO and 256px PNG from Brand.xaml.'
