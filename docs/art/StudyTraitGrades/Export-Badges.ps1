$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path "$PSScriptRoot/../../..").Path
$target = Join-Path $root 'Assets/10.Datas/Resources/UI/PlayerGrowthBadges'
$meta = [IO.File]::ReadAllText((Join-Path $target 'TraitRank_S.png.meta'))
function Export-Sheet($file, $columns, $rows, $names) {
    $sheet = [Drawing.Bitmap]::new((Join-Path $PSScriptRoot $file))
    try {
        $w = [int]($sheet.Width / $columns); $h = [int]($sheet.Height / $rows)
        for ($i = 0; $i -lt $names.Count; $i++) {
            $cell = $sheet.Clone([Drawing.Rectangle]::new(($i % $columns)*$w, [int][Math]::Floor($i / $columns)*$h, $w, $h), [Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $left=$w; $top=$h; $right=0; $bottom=0
            for ($y=0; $y -lt $h; $y++) { for ($x=0; $x -lt $w; $x++) {
                if ($cell.GetPixel($x,$y).A -gt 16) { $left=[Math]::Min($left,$x); $right=[Math]::Max($right,$x); $top=[Math]::Min($top,$y); $bottom=[Math]::Max($bottom,$y) }
            } }
            $output = [Drawing.Bitmap]::new(256,256,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
            $g=[Drawing.Graphics]::FromImage($output)
            $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $width=$right-$left+1; $height=$bottom-$top+1; $scale=240/[Math]::Max($width,$height)
            $rect=[Drawing.RectangleF]::new([single]((256-$width*$scale)/2),[single]((256-$height*$scale)/2),[single]($width*$scale),[single]($height*$scale))
            $g.DrawImage($cell,$rect,[Drawing.RectangleF]::new($left,$top,$width,$height),[Drawing.GraphicsUnit]::Pixel)
            $path=Join-Path $target ($names[$i]+'.png')
            $output.Save($path,[Drawing.Imaging.ImageFormat]::Png)
            if (!(Test-Path -LiteralPath ($path+'.meta'))) {
                [IO.File]::WriteAllText($path+'.meta',($meta -replace 'guid: [a-f0-9]+',('guid: '+[Guid]::NewGuid().ToString('N'))))
            }
            $g.Dispose(); $output.Dispose(); $cell.Dispose()
        }
    } finally { $sheet.Dispose() }
}
Export-Sheet 'Grades_Transparent.png' 3 2 @('TraitRank_SS','TraitRank_SSS','StudyRank_S','StudyRank_C','StudyRank_SS','StudyRank_SSS')
Export-Sheet 'StudyBA_Source.png' 2 1 @('StudyRank_B','StudyRank_A')
$paths=@('TraitRank_SS','TraitRank_SSS','StudyRank_C','StudyRank_B','StudyRank_A','StudyRank_S','StudyRank_SS','StudyRank_SSS') | ForEach-Object { Join-Path $target ($_+'.png') }
& "$root/Tools/ImageBackground/Review-ImageBackground.ps1" -InputPaths $paths -OutputPath "$PSScriptRoot/AlphaReview.png"
