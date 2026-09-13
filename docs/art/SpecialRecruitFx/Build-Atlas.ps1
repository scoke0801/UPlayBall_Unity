param([ValidateSet('Legend','CareerHigh')][string]$Kind)
Add-Type -AssemblyName System.Drawing
$inputImage = [Drawing.Bitmap]::new((Join-Path $PWD ("docs/art/SpecialRecruitFx/"+$Kind+"-source.png")))
$atlas = [Drawing.Bitmap]::new(1024,1024,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
for($row=0;$row -lt 4;$row++) { for($col=0;$col -lt 4;$col++) {
 for($y=0;$y -lt 256;$y++) { for($x=0;$x -lt 256;$x++) {
  $sx=[Math]::Min($inputImage.Width-1,[int](($col+($x+.5)/256)*$inputImage.Width/4))
  $sy=[Math]::Min($inputImage.Height-1,[int](($row+($y+.5)/256)*$inputImage.Height/4))
  $c=$inputImage.GetPixel($sx,$sy)
  $edge=[Math]::Min([Math]::Min($x,255-$x),[Math]::Min($y,255-$y))
    # 단색 초록과 발광색의 혼합을 역산해 어두운 매트 경계를 남기지 않는다.
  $opacity=[Math]::Min(1.0,[Math]::Max(0.0,(255.0-$c.G+[Math]::Max($c.R,$c.B))/255.0))
  if($opacity -lt .19) { $opacity=0 }
  $r=0; $g=0; $b=0
  if($opacity -gt 0) {
   $r=[int][Math]::Min(255,$c.R/$opacity)
   $g=[int][Math]::Max(0,[Math]::Min(255,($c.G-255*(1-$opacity))/$opacity))
   $b=[int][Math]::Min(255,$c.B/$opacity)
  }
  $a=[int](255*$opacity*[Math]::Min(1,$edge/14.0))
  $atlas.SetPixel($col*256+$x,$row*256+$y,[Drawing.Color]::FromArgb($a,$r,$g,$b))
 } }
} }
$atlas.Save((Join-Path $PWD ("Assets/10.Datas/Resources/UI/SpecialRecruitFx/"+$Kind+"Atlas.png")),[Drawing.Imaging.ImageFormat]::Png)
$atlas.Dispose(); $inputImage.Dispose()

