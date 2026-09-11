param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$variants = @(
    'Normal',
    'Rare',
    'AllStar',
    'GoldenGlove',
    'MVP',
    'Ex',
    'Legend',
    'CareerHigh'
)

$source = [System.Drawing.Bitmap]::FromFile((Resolve-Path -LiteralPath $InputPath))
try {
    $cellSize = [Math]::Min(
        [int][Math]::Floor($source.Width / 4),
        [int][Math]::Floor($source.Height / 2))
    $sheetWidth = $cellSize * 4
    $sheetHeight = $cellSize * 2
    $sheetOffsetX = [int][Math]::Floor(($source.Width - $sheetWidth) / 2)
    $sheetOffsetY = [int][Math]::Floor(($source.Height - $sheetHeight) / 2)

    [System.IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
    for ($index = 0; $index -lt $variants.Count; $index++) {
        $column = $index % 4
        $row = [int][Math]::Floor($index / 4)
        $target = New-Object System.Drawing.Bitmap(
            $cellSize,
            $cellSize,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($target)
            try {
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $sourceRect = New-Object System.Drawing.Rectangle(
                    ($sheetOffsetX + $column * $cellSize),
                    ($sheetOffsetY + $row * $cellSize),
                    $cellSize,
                    $cellSize)
                $targetRect = New-Object System.Drawing.Rectangle(0, 0, $cellSize, $cellSize)
                $graphics.DrawImage($source, $targetRect, $sourceRect, [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally {
                $graphics.Dispose()
            }

            $outputPath = Join-Path $OutputDirectory ("PlayerCard_CostStar_{0}_v4.png" -f $variants[$index])
            if (Test-Path -LiteralPath $outputPath) {
                throw "기존 출력 파일을 덮어쓰지 않습니다: $outputPath"
            }
            $target.Save($outputPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $target.Dispose()
        }
    }
}
finally {
    $source.Dispose()
}
