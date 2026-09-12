param()
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$sourceRoot = Join-Path $repoRoot 'output/imagegen/player-growth-badges'
$assetRoot = Join-Path $repoRoot 'Assets/10.Datas/Resources/UI/PlayerGrowthBadges'
foreach ($name in @('TraitRank_C', 'TraitRank_B', 'TraitRank_A', 'TraitRank_S', 'Study')) {
    $transparent = Join-Path $sourceRoot "$name-transparent.png"
    $source = Join-Path $sourceRoot "$name-source.png"
    $inputPath = if (Test-Path $transparent) { $transparent } else { $source }
    $input = [Drawing.Bitmap]::new($inputPath)
    $output = [Drawing.Bitmap]::new(256, 256, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        # 생성기가 실제 알파를 반환한 경우에도 전경 경계를 확인하고 동일한 캔버스로 맞춘다.
        $left = $input.Width; $top = $input.Height; $right = 0; $bottom = 0
        for ($y = 0; $y -lt $input.Height; $y++) {
            for ($x = 0; $x -lt $input.Width; $x++) {
                if ($input.GetPixel($x, $y).A -gt 16) {
                    $left = [Math]::Min($left, $x); $right = [Math]::Max($right, $x)
                    $top = [Math]::Min($top, $y); $bottom = [Math]::Max($bottom, $y)
                }
            }
        }
        if ($input.GetPixel(0, 0).A -ne 0) { throw "$name 배경 제거가 필요합니다." }
        $width = $right - $left + 1; $height = $bottom - $top + 1
        $ratio = 240.0 / [Math]::Max($width, $height)
        $destination = [Drawing.RectangleF]::new([single]((256-$width*$ratio)/2), [single]((256-$height*$ratio)/2), [single]($width*$ratio), [single]($height*$ratio))
        $graphics = [Drawing.Graphics]::FromImage($output)
        try {
            $graphics.Clear([Drawing.Color]::Transparent)
            $graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.DrawImage($input, $destination, [Drawing.RectangleF]::new($left, $top, $width, $height), [Drawing.GraphicsUnit]::Pixel)
        } finally { $graphics.Dispose() }
        $output.Save((Join-Path $assetRoot "$name.png"), [Drawing.Imaging.ImageFormat]::Png)
    } finally { $input.Dispose(); $output.Dispose() }
}
$paths = @('TraitRank_C','TraitRank_B','TraitRank_A','TraitRank_S','Study') | ForEach-Object { Join-Path $assetRoot "$_.png" }
& (Join-Path $PSScriptRoot 'Review-ImageBackground.ps1') -InputPaths $paths -OutputPath (Join-Path $sourceRoot 'alpha-review.png')
