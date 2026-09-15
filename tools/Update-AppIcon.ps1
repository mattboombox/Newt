# Rebuild the Windows icon from the same sprite used for the game window.
# Use nearest-neighbor sampling so small icons have no colored filtering halos.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$project = Join-Path $PSScriptRoot '../src/Newt.Game'
$sprite = [System.Drawing.Bitmap]::new((Join-Path $project 'Content/Sprites/Critters/newt.png'))
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
try {
    foreach ($size in $sizes) {
        $stream = [IO.MemoryStream]::new()
        $writer = [IO.BinaryWriter]::new($stream)
        $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
        # ICO DIB: BGRA pixels followed by a legacy transparency mask.
        $writer.Write([int]40)
        $writer.Write([int]$size)
        $writer.Write([int]($size * 2))
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([int]0)
        $writer.Write([int]($size * $size * 4 + $maskStride * $size))
        foreach ($unused in 1..4) { $writer.Write([int]0) }
        $mask = [byte[]]::new($maskStride * $size)
        $scale = [Math]::Min($size / $sprite.Width, $size / $sprite.Height)
        $width = [int][Math]::Round($sprite.Width * $scale)
        $height = [int][Math]::Round($sprite.Height * $scale)
        $left = [int][Math]::Floor(($size - $width) / 2)
        $top = [int][Math]::Floor(($size - $height) / 2)
        for ($y = $size - 1; $y -ge 0; $y--) {
            for ($x = 0; $x -lt $size; $x++) {
                $pixel = [Drawing.Color]::Transparent
                if ($x -ge $left -and $x -lt ($left + $width) -and $y -ge $top -and $y -lt ($top + $height)) {
                    $sx = [Math]::Min($sprite.Width - 1, [int][Math]::Floor(($x - $left + 0.5) / $scale))
                    $sy = [Math]::Min($sprite.Height - 1, [int][Math]::Floor(($y - $top + 0.5) / $scale))
                    $pixel = $sprite.GetPixel($sx, $sy)
                }
                if ($pixel.A -eq 0) {
                    $writer.Write([int]0)
                    $index = ($size - 1 - $y) * $maskStride + [int][Math]::Floor($x / 8)
                    $mask[$index] = $mask[$index] -bor (128 -shr ($x % 8))
                } else {
                    $writer.Write([byte]$pixel.B)
                    $writer.Write([byte]$pixel.G)
                    $writer.Write([byte]$pixel.R)
                    $writer.Write([byte]$pixel.A)
                }
            }
        }
        $writer.Write($mask)
        $frames += ,$stream.ToArray()
        $writer.Dispose()
        $stream.Dispose()
    }
    $output = [IO.File]::Create((Join-Path $project 'Icon.ico'))
    $writer = [IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($i = 0; $i -lt $sizes.Count; $i++) {
            $writer.Write([byte]($sizes[$i] % 256))
            $writer.Write([byte]($sizes[$i] % 256))
            $writer.Write([uint16]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([int]$frames[$i].Length)
            $writer.Write([int]$offset)
            $offset += $frames[$i].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally {
        $writer.Dispose()
        $output.Dispose()
    }
} finally {
    $sprite.Dispose()
}
