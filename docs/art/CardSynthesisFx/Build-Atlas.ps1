Add-Type -AssemblyName System.Drawing
$inputImage = [Drawing.Bitmap]::new((Join-Path $PWD 'docs/art/CardSynthesisFx/Synthesis-keyed.png'))
$atlas = [Drawing.Bitmap]::new(1024,1024,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
for($row=0;$row -lt 4;$row++) { for($col=0;$col -lt 4;$col++) {
 for($y=0;$y -lt 256;$y++) { for($x=0;$x -lt 256;$x++) {
  $sx=[Math]::Min($inputImage.Width-1,[int](($col+($x+.5)/256)*$inputImage.Width/4))
  $sy=[Math]::Min($inputImage.Height-1,[int](($row+($y+.5)/256)*$inputImage.Height/4))
  $c=$inputImage.GetPixel($sx,$sy)
  $edge=[Math]::Min([Math]::Min($x,255-$x),[Math]::Min($y,255-$y))
  $a=[int]($c.A*[Math]::Min(1,$edge/14.0))
  $atlas.SetPixel($col*256+$x,$row*256+$y,[Drawing.Color]::FromArgb($a,$c.R,[Math]::Min($c.G,[int]($c.R*.83)),$c.B))
 } }
} }
$atlas.Save((Join-Path $PWD 'Assets/10.Datas/Resources/UI/CardSynthesisFx/SynthesisAtlas.png'),[Drawing.Imaging.ImageFormat]::Png)
$atlas.Dispose(); $inputImage.Dispose()
