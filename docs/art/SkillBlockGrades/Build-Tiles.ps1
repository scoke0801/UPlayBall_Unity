$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=(Resolve-Path (Join-Path $PSScriptRoot '../../..')).Path
$target=Join-Path $root 'Assets/10.Datas/Resources/UI/OwnerPowerUp/SkillGrades'
New-Item -ItemType Directory -Force $target | Out-Null
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class SkillGradeAtlas {
 public static Bitmap Crop(Bitmap sheet, Rectangle region, bool preserveAspect = false) {
  int minX=region.Right,minY=region.Bottom,maxX=region.X,maxY=region.Y;
  for(int y=region.Y;y<Math.Min(sheet.Height,region.Bottom);y++) for(int x=region.X;x<Math.Min(sheet.Width,region.Right);x++) {
   if(sheet.GetPixel(x,y).A<200)continue;
   minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
  }
  var result=new Bitmap(256,256,PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(result)) {
   g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
   int width=maxX-minX+1,height=maxY-minY+1;
   double scale=252d/Math.Max(width,height);
   int dw=preserveAspect?(int)Math.Round(width*scale):252,dh=preserveAspect?(int)Math.Round(height*scale):252;
   g.DrawImage(sheet,new Rectangle((256-dw)/2,(256-dh)/2,dw,dh),new Rectangle(minX,minY,width,height),GraphicsUnit.Pixel);
  }
  return result;
 }
 public static string Alpha(Bitmap image) {
  int transparent=0,partial=0,opaque=0;
  for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++) {int a=image.GetPixel(x,y).A;if(a==0)transparent++;else if(a==255)opaque++;else partial++;}
  return string.Format("transparent={0}, partial={1}, opaque={2}",transparent,partial,opaque);
 }
}
"@
$sheet=[Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'Tiles-Source.png'))
Write-Output ([SkillGradeAtlas]::Alpha($sheet))
$sheet.Save((Join-Path $PSScriptRoot 'Tiles-Transparent.png'),[Drawing.Imaging.ImageFormat]::Png)
$labels=@('C','B','A','S','SS','SSS')
$template=[IO.File]::ReadAllText((Join-Path $root 'Assets/10.Datas/Resources/UI/OwnerPowerUp/skill_direction_0.png.meta'))
$tiles=@{}
for($grade=0;$grade -lt 6;$grade++) {
 $bases=@{}
 for($row=0;$row -lt 5;$row++) {
  $region=[Drawing.Rectangle]::new(15+$grade*230,22+$row*201,205,192)
  $bases[$row]=[SkillGradeAtlas]::Crop($sheet,$region)
 }
 foreach($entry in @(@(0,0),@(1,1),@(3,2),@(5,3),@(7,4))) {
  $baseMask=$entry[0];$row=$entry[1];$mask=$baseMask
  for($rotation=0;$rotation -lt 4;$rotation++) {
   $key=$labels[$grade]+'_'+$mask
   if(-not $tiles.ContainsKey($key)) {
    $tile=[Drawing.Bitmap]$bases[$row].Clone()
    $tile.RotateFlip([Drawing.RotateFlipType]$rotation)
    $path=Join-Path $target ('skill_'+$key+'.png')
    $tile.Save($path,[Drawing.Imaging.ImageFormat]::Png)
    if(-not(Test-Path ($path+'.meta'))){[IO.File]::WriteAllText($path+'.meta',($template -replace 'guid: [a-f0-9]+',('guid: '+[guid]::NewGuid().ToString('N'))))}
    $tiles[$key]=$tile
   }
   $mask=(($mask -shl 1) -band 15) -bor (($mask -shr 3) -band 1)
  }
 }
 foreach($tile in $bases.Values){$tile.Dispose()}
}
$preview=[Drawing.Bitmap]::new(1180,720)
$g=[Drawing.Graphics]::FromImage($preview);$g.Clear([Drawing.Color]::FromArgb(19,27,43))
$font=[Drawing.Font]::new('Arial',16,[Drawing.FontStyle]::Bold)
for($grade=0;$grade -lt 6;$grade++) {
 $x=15+$grade*194;$label=$labels[$grade]
 $g.DrawString($label,$font,[Drawing.Brushes]::White,$x+70,12)
 $g.DrawImage($tiles[$label+'_0'],$x+20,45,144,144)
 $g.FillRectangle([Drawing.Brushes]::WhiteSmoke,$x,205,182,135)
 # T와 L 모양으로 연결 방향과 서로 다른 블록 경계를 확인한다.
 foreach($sample in @(@(1,0,2),@(0,1,1),@(1,1,13),@(2,1,4))) {
  $g.DrawImage($tiles[$label+'_'+$sample[2]],$x+12+$sample[0]*52,215+$sample[1]*52,52,52)
  $g.DrawImage($tiles[$label+'_'+$sample[2]],$x+12+$sample[0]*52,365+$sample[1]*52,52,52)
 }
 for($mask=0;$mask -lt 15;$mask++) {$g.DrawImage($tiles[$label+'_'+$mask],$x+($mask%4)*44,510+[Math]::Floor($mask/4)*44,42,42)}
}
$preview.Save((Join-Path $PSScriptRoot 'Tiles-Review.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$font.Dispose();$preview.Dispose();$sheet.Dispose()
foreach($tile in $tiles.Values){$tile.Dispose()}
Write-Output ('등급별 방향 타일 '+$tiles.Count+'종 생성')
$sheet=[Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot 'Badges-Source.png'))
Write-Output ('배지 원본 '+[SkillGradeAtlas]::Alpha($sheet))
$sheet.Save((Join-Path $PSScriptRoot 'Badges-Transparent.png'),[Drawing.Imaging.ImageFormat]::Png)
$template=[IO.File]::ReadAllText((Join-Path $root 'Assets/10.Datas/Resources/UI/PlayerGrowthBadges/BoardRank_N.png.meta'))
$review=[Drawing.Bitmap]::new(1100,410)
$g=[Drawing.Graphics]::FromImage($review);$g.Clear([Drawing.Color]::FromArgb(19,27,43))
$g.FillRectangle([Drawing.Brushes]::WhiteSmoke,0,210,1100,200)
$metrics=@()
for($index=0;$index -lt 6;$index++) {
 $x0=[int][Math]::Floor($index*$sheet.Width/6.0);$x1=[int][Math]::Floor(($index+1)*$sheet.Width/6.0)
 $badge=[SkillGradeAtlas]::Crop($sheet,[Drawing.Rectangle]::new($x0,80,$x1-$x0,$sheet.Height-160),$true)
 $path=Join-Path $root ('Assets/10.Datas/Resources/UI/PlayerGrowthBadges/SkillGrade_'+$labels[$index]+'.png')
 $badge.Save($path,[Drawing.Imaging.ImageFormat]::Png)
 if(-not(Test-Path ($path+'.meta'))){[IO.File]::WriteAllText($path+'.meta',($template -replace 'guid: [a-f0-9]+',('guid: '+[guid]::NewGuid().ToString('N'))))}
 $metrics+=($labels[$index]+': '+[SkillGradeAtlas]::Alpha($badge))
 foreach($y in @(8,218)) {
  $g.DrawImage($badge,24+$index*182,$y,128,128)
  $g.DrawImage($badge,30+$index*182,$y+132,48,48)
  $g.DrawImage($badge,102+$index*182,$y+144,24,24)
 }
 $badge.Dispose()
}
$review.Save((Join-Path $PSScriptRoot 'Badges-Review.png'),[Drawing.Imaging.ImageFormat]::Png)
[IO.File]::WriteAllLines((Join-Path $PSScriptRoot 'Alpha-Validation.txt'),$metrics)
$g.Dispose();$review.Dispose();$sheet.Dispose()
