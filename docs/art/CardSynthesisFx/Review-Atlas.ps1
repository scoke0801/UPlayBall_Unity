Add-Type -AssemblyName System.Drawing
$atlas=[Drawing.Bitmap]::new((Join-Path $PWD 'Assets/10.Datas/Resources/UI/CardSynthesisFx/SynthesisAtlas.png'))
$review=[Drawing.Bitmap]::new(1024,512)
$g=[Drawing.Graphics]::FromImage($review)
$g.Clear([Drawing.Color]::FromArgb(240,240,240))
$g.FillRectangle([Drawing.Brushes]::Black,512,0,512,512)
$g.DrawImage($atlas,0,0,512,512)
$g.DrawImage($atlas,512,0,512,512)
$review.Save((Join-Path $PWD 'docs/art/CardSynthesisFx/AlphaReview.png'))
$max=0;$zero=0
for($y=0;$y -lt 1024;$y++){for($x=0;$x -lt 1024;$x++){ $a=$atlas.GetPixel($x,$y).A; $max=[Math]::Max($max,$a);if($a -eq 0){$zero++}}}
Write-Output "Alpha maximum=$max transparent=$zero / 1048576"
$g.Dispose();$review.Dispose();$atlas.Dispose()
