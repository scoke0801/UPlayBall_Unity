$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$target=Join-Path $root 'Assets/10.Datas/Resources/UI/OwnerPowerUp/SkillBaseball'
New-Item -ItemType Directory -Force $target | Out-Null
$sheet=[Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'Transparent.png'))
$template=Get-Content (Join-Path $root 'Assets/10.Datas/Resources/UI/OwnerPowerUp/SkillGrades/skill_C_0.png.meta') -Raw
foreach($part in @(@('Plate',200,255,480,480),@('Ball',900,290,420,415))) {
 $region=[Drawing.Rectangle]::new($part[1],$part[2],$part[3],$part[4])
 $minX=$region.Right;$minY=$region.Bottom;$maxX=0;$maxY=0
 for($y=$region.Top;$y -lt $region.Bottom;$y++){for($x=$region.Left;$x -lt $region.Right;$x++){
  if($sheet.GetPixel($x,$y).A -gt 0){$minX=[Math]::Min($minX,$x);$minY=[Math]::Min($minY,$y);$maxX=[Math]::Max($maxX,$x);$maxY=[Math]::Max($maxY,$y)}
 }}
 $bitmap=[Drawing.Bitmap]::new(256,256)
 $g=[Drawing.Graphics]::FromImage($bitmap)
 $g.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
 $g.DrawImage($sheet,[Drawing.Rectangle]::new(1,1,254,254),[Drawing.Rectangle]::new($minX,$minY,$maxX-$minX+1,$maxY-$minY+1),[Drawing.GraphicsUnit]::Pixel)
 $path=Join-Path $target ($part[0]+'.png')
 $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
 if(-not(Test-Path ($path+'.meta'))){[IO.File]::WriteAllText($path+'.meta',($template -replace 'guid: [a-f0-9]+',('guid: '+[guid]::NewGuid().ToString('N')) -replace 'isReadable: 0','isReadable: 1'))}
 $g.Dispose();$bitmap.Dispose()
}
$sheet.Dispose()
