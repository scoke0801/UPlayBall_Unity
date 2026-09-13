$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$source = [Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'bridge-transparent.png'))
$left = $source.Width; $top = $source.Height; $right = 0; $bottom = 0
for ($y = 0; $y -lt $source.Height; $y++) {
    for ($x = 0; $x -lt $source.Width; $x++) {
        if ($source.GetPixel($x, $y).A -gt 0) {
            $left = [Math]::Min($left, $x); $right = [Math]::Max($right, $x)
            $top = [Math]::Min($top, $y); $bottom = [Math]::Max($bottom, $y)
        }
    }
}
$crop = $source.Clone([Drawing.Rectangle]::new($left-2, $top-2, $right-$left+5, $bottom-$top+5), [Drawing.Imaging.PixelFormat]::Format32bppArgb)
$asset = Join-Path $root 'Assets/10.Datas/Resources/UI/OwnerPowerUp/skill_bridge_v2.png'
$texture = [Drawing.Bitmap]::new(96, 256)
$graphics = [Drawing.Graphics]::FromImage($texture)
$graphics.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.DrawImage($crop, 0, 0, 96, 256)
$texture.Save($asset, [Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose(); $crop.Dispose(); $source.Dispose()
$tile = [Drawing.Bitmap]::FromFile((Join-Path $root 'Assets/10.Datas/Resources/UI/OwnerPowerUp/skill_tile_star_v3.png'))
function Draw-Tinted($g, $bitmap, $rect, $color) {
    $matrix = [Drawing.Imaging.ColorMatrix]::new()
    $matrix.Matrix00 = $color.R / 255.0; $matrix.Matrix11 = $color.G / 255.0; $matrix.Matrix22 = $color.B / 255.0
    $attributes = [Drawing.Imaging.ImageAttributes]::new()
    $attributes.SetColorMatrix($matrix)
    $g.DrawImage($bitmap, $rect, 0, 0, $bitmap.Width, $bitmap.Height, [Drawing.GraphicsUnit]::Pixel, $attributes)
    $attributes.Dispose()
}
$preview = [Drawing.Bitmap]::new(720, 340)
$g = [Drawing.Graphics]::FromImage($preview)
$g.Clear([Drawing.Color]::FromArgb(24, 30, 39))
$g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$vertical = $texture.Clone(); $vertical.RotateFlip([Drawing.RotateFlipType]::Rotate90FlipNone)
$vertical.Save((Join-Path $root 'Assets/10.Datas/Resources/UI/OwnerPowerUp/skill_bridge_vertical_v2.png'), [Drawing.Imaging.ImageFormat]::Png)
$groups = @(@(@(0,0),@(0,1),@(1,1),@(2,1)), @(@(1,0),@(2,0),@(3,0),@(3,1)))
for ($panel = 0; $panel -lt 3; $panel++) {
    $origin = 20 + $panel*235
    $color = @([Drawing.Color]::FromArgb(224,160,44), [Drawing.Color]::FromArgb(177,83,185), [Drawing.Color]::FromArgb(61,139,210))[$panel]
    foreach ($group in $groups) {
        foreach ($cell in $group) {
            Draw-Tinted $g $tile ([Drawing.Rectangle]::new($origin+$cell[0]*52, 36+$cell[1]*52, 50, 50)) $color
        }
        for ($a=0; $a -lt $group.Count; $a++) {
            for ($b=$a+1; $b -lt $group.Count; $b++) {
                $dx=$group[$b][0]-$group[$a][0]; $dy=$group[$b][1]-$group[$a][1]
                if ([Math]::Abs($dx)+[Math]::Abs($dy) -ne 1) { continue }
                $cx=$origin+25+($group[$a][0]+$group[$b][0])*26
                $cy=61+($group[$a][1]+$group[$b][1])*26
                if ($dx -ne 0) { Draw-Tinted $g $texture ([Drawing.Rectangle]::new($cx-6,$cy-17,12,34)) $color }
                else { Draw-Tinted $g $vertical ([Drawing.Rectangle]::new($cx-17,$cy-6,34,12)) $color }
            }
        }
    }
}
$g.FillRectangle([Drawing.Brushes]::WhiteSmoke, 15, 175, 335, 145)
$g.DrawImage($texture, 45, 215, 40, 108)
$g.DrawImage($texture, 405, 215, 40, 108)
$preview.Save((Join-Path $PSScriptRoot 'bridge-review.png'), [Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $preview.Dispose(); $tile.Dispose(); $texture.Dispose(); $vertical.Dispose()
Write-Output 'Generated 96x256 bridge texture and light/dark + 50px tile composite.'
