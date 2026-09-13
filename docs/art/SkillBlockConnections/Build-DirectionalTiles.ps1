$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$sheet = [Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'directional16-transparent.png'))
$tiles = @{}
$template = Get-Content (Join-Path $root 'Assets/10.Datas/Resources/UI/OwnerPowerUp/skill_tile_star_v3.png.meta') -Raw
for ($mask=0; $mask -lt 16; $mask++) {
    $x0=[int][Math]::Round(($mask%4)*$sheet.Width/4.0)
    $x1=[int][Math]::Round((($mask%4)+1)*$sheet.Width/4.0)
    $y0=[int][Math]::Round([Math]::Floor($mask/4)*$sheet.Height/4.0)
    $y1=[int][Math]::Round(([Math]::Floor($mask/4)+1)*$sheet.Height/4.0)
    $tile = [Drawing.Bitmap]::new(256,256)
    $g = [Drawing.Graphics]::FromImage($tile)
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($sheet,[Drawing.Rectangle]::new(0,0,256,256),$x0,$y0,$x1-$x0,$y1-$y0,[Drawing.GraphicsUnit]::Pixel)
    $g.Dispose()
    $path = Join-Path $root ('Assets/10.Datas/Resources/UI/OwnerPowerUp/skill_direction_' + $mask + '.png')
    $tile.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    if (-not (Test-Path ($path+'.meta'))) {
        [IO.File]::WriteAllText(($path+'.meta'),($template -replace 'guid: [a-f0-9]+',('guid: '+[guid]::NewGuid().ToString('N'))))
    }
    $tiles[$mask]=$tile
}
if ($tiles.Count -ne 16) { throw '방향 16종이 모두 생성되지 않았습니다.' }
$preview = [Drawing.Bitmap]::new(720,420)
$g = [Drawing.Graphics]::FromImage($preview); $g.Clear([Drawing.Color]::FromArgb(20,29,42))
for ($mask=0; $mask -lt 16; $mask++) { $g.DrawImage($tiles[$mask], 12+($mask%8)*54, 12+[Math]::Floor($mask/8)*54, 52,52) }
$groups = @(@(@(0,0),@(0,1),@(1,1),@(2,1)), @(@(1,0),@(2,0),@(3,0),@(3,1)))
for ($panel=0; $panel -lt 3; $panel++) {
    $color = @([Drawing.Color]::FromArgb(224,160,44), [Drawing.Color]::FromArgb(177,83,185), [Drawing.Color]::FromArgb(61,139,210))[$panel]
    $matrix = [Drawing.Imaging.ColorMatrix]::new()
    $matrix.Matrix00=$color.R/255.0; $matrix.Matrix11=$color.G/255.0; $matrix.Matrix22=$color.B/255.0
    $attributes = [Drawing.Imaging.ImageAttributes]::new(); $attributes.SetColorMatrix($matrix)
    foreach ($group in $groups) {
        foreach ($cell in $group) {
            $mask=0
            foreach ($other in $group) {
                $dx=$other[0]-$cell[0]; $dy=$other[1]-$cell[1]
                if ($dy -eq 0 -and $dx -eq 1) { $mask=$mask -bor 1 }
                if ($dx -eq 0 -and $dy -eq 1) { $mask=$mask -bor 2 }
                if ($dy -eq 0 -and $dx -eq -1) { $mask=$mask -bor 4 }
                if ($dx -eq 0 -and $dy -eq -1) { $mask=$mask -bor 8 }
            }
            $g.DrawImage($tiles[$mask], [Drawing.Rectangle]::new(12+$panel*235+$cell[0]*52,160+$cell[1]*52,52,52),0,0,256,256,[Drawing.GraphicsUnit]::Pixel,$attributes)
        }
    }
    $attributes.Dispose()
}
$g.FillRectangle([Drawing.Brushes]::WhiteSmoke,12,290,330,118)
$g.DrawImage($tiles[9],30,300,96,96); $g.DrawImage($tiles[9],380,300,96,96)
$preview.Save((Join-Path $PSScriptRoot 'directional-review.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $preview.Dispose(); $sheet.Dispose()
foreach ($tile in $tiles.Values) { $tile.Dispose() }
Write-Output '방향 마스크 16종 및 배치·알파 합성 생성 완료'
