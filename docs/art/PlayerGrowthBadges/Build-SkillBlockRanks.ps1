$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = (Get-Location).Path
$source = [Drawing.Bitmap]::FromFile("$root/docs/art/PlayerGrowthBadges/SkillBlockRanks_Source.png")
Write-Output "Source $($source.Width)x$($source.Height), corner alpha $($source.GetPixel(0,0).A)"
$names = @('N','R','E','U','L')
$template = Get-Content "$root/Assets/10.Datas/Resources/UI/PlayerGrowthBadges/BoardRank_S.png.meta" -Raw
for ($i=0; $i -lt 5; $i++) {
    $left = [int][Math]::Floor($i*$source.Width/5)
    $right = [int][Math]::Floor(($i+1)*$source.Width/5)
    $minX=$right; $minY=$source.Height; $maxX=0; $maxY=0
    for ($x=$left;$x -lt $right;$x++) { for ($y=0;$y -lt $source.Height;$y++) {
        if ($source.GetPixel($x,$y).A -gt 32) { $minX=[Math]::Min($minX,$x);$maxX=[Math]::Max($maxX,$x);$minY=[Math]::Min($minY,$y);$maxY=[Math]::Max($maxY,$y) }
    }}
    $w=$maxX-$minX+1;$h=$maxY-$minY+1
    $scale=240/[Math]::Max($w,$h)
    $bitmap=New-Object Drawing.Bitmap 256,256
    $g=[Drawing.Graphics]::FromImage($bitmap)
    $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($source,[Drawing.RectangleF]::new((256-$w*$scale)/2,(256-$h*$scale)/2,$w*$scale,$h*$scale),[Drawing.RectangleF]::new($minX,$minY,$w,$h),[Drawing.GraphicsUnit]::Pixel)
    $path="$root/Assets/10.Datas/Resources/UI/PlayerGrowthBadges/BoardRank_$($names[$i]).png"
    $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose();$bitmap.Dispose()
    [IO.File]::WriteAllText("$path.meta",($template -replace 'guid: [a-f0-9]+',('guid: '+[Guid]::NewGuid().ToString('N'))))
    Write-Output "$($names[$i]): bounds $minX,$minY,$w,$h"
}
$source.Dispose()
& Tools/ImageBackground/Review-ImageBackground.ps1 -InputPaths ($names | ForEach-Object { "Assets/10.Datas/Resources/UI/PlayerGrowthBadges/BoardRank_$_.png" }) -OutputPath docs/art/PlayerGrowthBadges/SkillBlockRanks_AlphaReview.png
